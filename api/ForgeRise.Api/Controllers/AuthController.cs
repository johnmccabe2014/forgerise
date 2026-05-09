using System.Security.Cryptography;
using ForgeRise.Api.Auth;
using ForgeRise.Api.Auth.Contracts;
using ForgeRise.Api.Data;
using ForgeRise.Api.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace ForgeRise.Api.Controllers;

[ApiController]
[Route("auth")]
public sealed class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly ITokenService _tokens;
    private readonly ILoginLockout _lockout;
    private readonly ILogger<AuthController> _log;
    private readonly IHostEnvironment _env;

    public AuthController(
        AppDbContext db,
        IPasswordHasher hasher,
        ITokenService tokens,
        ILoginLockout lockout,
        ILogger<AuthController> log,
        IHostEnvironment env)
    {
        _db = db;
        _hasher = hasher;
        _tokens = tokens;
        _lockout = lockout;
        _log = log;
        _env = env;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var email = request.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email == email, ct))
        {
            _log.LogInformation("auth.register.duplicate {Email}", email);
            return Conflict(new { error = "email_in_use" });
        }

        var user = new User
        {
            Email = email,
            DisplayName = request.DisplayName.Trim(),
            PasswordHash = _hasher.Hash(request.Password),
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        var tokens = await _tokens.IssueAsync(user, ClientIp(), ct);
        IssueAuthCookies(tokens);
        _log.LogInformation("auth.register.success {UserId}", user.Id);

        return Created($"/auth/me", new AuthResponse(new AuthUserDto(user.Id, user.Email, user.DisplayName)));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

        if (user is null)
        {
            _log.LogInformation("auth.login.failed reason=unknown_email");
            return Unauthorized(new { error = "invalid_credentials" });
        }

        if (_lockout.IsLockedOut(user))
        {
            _log.LogWarning("auth.login.locked {UserId}", user.Id);
            return StatusCode(StatusCodes.Status423Locked, new { error = "account_locked" });
        }

        var verify = _hasher.Verify(request.Password, user.PasswordHash);
        if (verify == PasswordVerificationResult.Failed)
        {
            await _lockout.RecordFailureAsync(user, ct);
            _log.LogInformation("auth.login.failed {UserId} reason=bad_password", user.Id);
            return Unauthorized(new { error = "invalid_credentials" });
        }

        if (verify == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _hasher.Hash(request.Password);
            await _db.SaveChangesAsync(ct);
        }

        await _lockout.RecordSuccessAsync(user, ct);

        var tokens = await _tokens.IssueAsync(user, ClientIp(), ct);
        IssueAuthCookies(tokens);
        _log.LogInformation("auth.login.success {UserId}", user.Id);

        return Ok(new AuthResponse(new AuthUserDto(user.Id, user.Email, user.DisplayName)));
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("auth-refresh")]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        var presented = Request.Cookies[AuthCookies.RefreshTokenCookie];
        var (tokens, failure) = await _tokens.RotateAsync(presented ?? string.Empty, ClientIp(), ct);

        if (failure is not null)
        {
            if (failure == RefreshFailureReason.ReplayDetected)
                _log.LogWarning("auth.refresh.replay_detected");
            else
                _log.LogInformation("auth.refresh.failed {Reason}", failure);
            AuthCookies.Clear(Response, secure: !(_env.IsDevelopment() || _env.IsEnvironment("Testing")));
            return Unauthorized(new { error = "invalid_refresh" });
        }

        IssueAuthCookies(tokens!);
        return Ok(new { refreshed = true });
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var presented = Request.Cookies[AuthCookies.RefreshTokenCookie];
        if (!string.IsNullOrEmpty(presented))
            await _tokens.RevokeAsync(presented, ct);
        AuthCookies.Clear(Response, secure: !(_env.IsDevelopment() || _env.IsEnvironment("Testing")));
        _log.LogInformation("auth.logout.success");
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var sub = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
                ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(sub, out var userId)) return Unauthorized();

        var user = await _db.Users
            .Where(u => u.Id == userId)
            .Select(u => new AuthUserDto(u.Id, u.Email, u.DisplayName))
            .FirstOrDefaultAsync(ct);

        return user is null ? Unauthorized() : Ok(user);
    }

    private void IssueAuthCookies(IssuedTokens tokens)
    {
        var secure = !IsLocalEnvironment();
        AuthCookies.SetTokens(Response, tokens, secure);
        var csrf = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        AuthCookies.SetCsrf(Response, csrf, secure, tokens.RefreshExpiresAt);
    }

    private bool IsLocalEnvironment() =>
        _env.IsDevelopment() || _env.IsEnvironment("Testing");

    private string? ClientIp() => HttpContext.Connection.RemoteIpAddress?.ToString();
}
