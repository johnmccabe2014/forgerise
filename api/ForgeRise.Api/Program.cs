using System.Text;
using System.Threading.RateLimiting;
using ForgeRise.Api.Auth;
using ForgeRise.Api.Data;
using ForgeRise.Api.Features.Video.Options;
using ForgeRise.Api.Features.Video.Services;
using ForgeRise.Api.Features.Video.Storage;
using ForgeRise.Api.Observability;
using ForgeRise.Api.Welfare;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

// --- Logging: structured JSON (master prompt §11) ---
builder.Host.UseSerilog((ctx, services, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("service", "forgerise-api")
    .Destructure.With(new WelfareDestructuringPolicy())
    .WriteTo.Console(new CompactJsonFormatter()));

// --- OpenTelemetry tracing (OTLP -> on-cluster collector) ---
var serviceName = builder.Configuration["OTEL_SERVICE_NAME"] ?? "forgerise-api";
var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(serviceName))
    .WithTracing(t =>
    {
        t.AddAspNetCoreInstrumentation();
        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            t.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
        }
    });

// --- Persistence ---
var conn = builder.Configuration.GetConnectionString("Postgres");
if (!string.IsNullOrWhiteSpace(conn))
{
    builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(conn));
}

// --- Auth: JWT bearer ---
var jwtOptions = new JwtOptions
{
    Key = builder.Configuration["Jwt:Key"] ?? string.Empty,
    Issuer = builder.Configuration["Jwt:Issuer"] ?? "forgerise",
    Audience = builder.Configuration["Jwt:Audience"] ?? "forgerise.web",
};

if (!builder.Environment.IsEnvironment("Testing") &&
    (string.IsNullOrWhiteSpace(jwtOptions.Key) || Encoding.UTF8.GetByteCount(jwtOptions.Key) < 32))
{
    throw new InvalidOperationException("Jwt:Key must be configured with at least 32 bytes of entropy.");
}

builder.Services.AddSingleton(jwtOptions);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<ILoginLockout, LoginLockout>();
builder.Services.AddSingleton<ForgeRise.Api.Sessions.ISessionPlanGenerator, ForgeRise.Api.Sessions.HeuristicSessionPlanGenerator>();

// --- Feature flags ---
builder.Services.Configure<VideoFeatureOptions>(
    builder.Configuration.GetSection(VideoFeatureOptions.SectionName));
builder.Services.Configure<VideoSigningOptions>(
    builder.Configuration.GetSection(VideoSigningOptions.SectionName));
builder.Services.Configure<VideoStorageOptions>(
    builder.Configuration.GetSection(VideoStorageOptions.SectionName));

// Validate Video options eagerly when the module is enabled, so a
// misconfigured prod refuses to start instead of failing per-request.
{
    var videoSection = builder.Configuration.GetSection(VideoFeatureOptions.SectionName);
    if (videoSection.GetValue<bool>("Enabled"))
    {
        var secret = videoSection.GetValue<string>("SigningSecret") ?? string.Empty;
        if (Encoding.UTF8.GetByteCount(secret) < 32)
        {
            throw new InvalidOperationException(
                "Features:Video:SigningSecret must be >= 32 bytes when Features:Video:Enabled=true.");
        }
        if (builder.Environment.IsProduction() &&
            VideoSigningOptions.ProductionDenyList.Contains(secret))
        {
            throw new InvalidOperationException(
                "Features:Video:SigningSecret matches a forbidden default; rotate it.");
        }
        var root = videoSection.GetValue<string>("Root") ?? string.Empty;
        if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
        {
            throw new InvalidOperationException(
                "Features:Video:Root must be set and exist when the module is enabled.");
        }
    }
}

builder.Services.AddSingleton<LocalFsObjectStore>();
builder.Services.AddSingleton<IObjectStore>(sp => sp.GetRequiredService<LocalFsObjectStore>());
builder.Services.AddScoped<IUploadService, UploadService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = string.IsNullOrEmpty(jwtOptions.Key)
                ? new SymmetricSecurityKey(new byte[32])
                : new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key)),
            ClockSkew = TimeSpan.FromSeconds(30),
        };

        // Read access token from cookie if no Authorization header is present.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                if (string.IsNullOrEmpty(ctx.Token) &&
                    ctx.Request.Cookies.TryGetValue(AuthCookies.AccessTokenCookie, out var cookie))
                {
                    ctx.Token = cookie;
                }
                return Task.CompletedTask;
            },
        };
    });

builder.Services.AddAuthorization();

// --- Rate limiting (master prompt §10) ---
builder.Services.AddRateLimiter(opts =>
{
    opts.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    opts.AddPolicy("auth-login", ctx => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));

    opts.AddPolicy("auth-register", ctx => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 3,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));

    opts.AddPolicy("auth-refresh", ctx => RateLimitPartition.GetTokenBucketLimiter(
        partitionKey: ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new TokenBucketRateLimiterOptions
        {
            TokenLimit = 30,
            ReplenishmentPeriod = TimeSpan.FromMinutes(1),
            TokensPerPeriod = 30,
            QueueLimit = 0,
            AutoReplenishment = true,
        }));

    opts.AddPolicy("video-upload", ctx => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(5),
            QueueLimit = 0,
        }));
});

// --- App services ---
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// CORS - locked per master prompt §10.
var allowedOrigins = (builder.Configuration["Cors:AllowedOrigins"] ?? string.Empty)
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
{
    if (allowedOrigins.Length > 0) p.WithOrigins(allowedOrigins);
    p.AllowAnyHeader().AllowAnyMethod().AllowCredentials();
}));

var app = builder.Build();

// --- Apply EF migrations on startup (non-Testing only).
// Single-instance deploys, so simple inline migration is fine; for HA
// promote this to a Job/init-container.
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var migLogger = scope.ServiceProvider
        .GetRequiredService<ILoggerFactory>().CreateLogger("Startup.Migrate");
    try
    {
        migLogger.LogInformation("Applying EF Core migrations...");
        await db.Database.MigrateAsync();
        migLogger.LogInformation("EF Core migrations applied.");
    }
    catch (Exception ex)
    {
        migLogger.LogError(ex, "EF Core migration failed at startup");
        throw;
    }
}

// --- Pipeline ---
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
if (!app.Environment.IsEnvironment("Testing"))
{
    app.UseRateLimiter();
}
app.UseMiddleware<CsrfMiddleware>();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/ready", () => Results.Ok(new { status = "ready" }));

app.MapControllers();

app.Run();

// Expose Program for WebApplicationFactory<T> in tests.
public partial class Program { }
