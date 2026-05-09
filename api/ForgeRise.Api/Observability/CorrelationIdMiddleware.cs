using Serilog.Context;

namespace ForgeRise.Api.Observability;

/// <summary>
/// Correlation-ID propagation. Master prompt §11.
/// Reads inbound X-Correlation-Id (validated), otherwise mints a new GUID.
/// Pushes the value into Serilog log context and the HTTP response.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var incoming = context.Request.Headers[HeaderName].FirstOrDefault();
        var correlationId = IsValid(incoming) ? incoming! : Guid.NewGuid().ToString("n");

        context.Items["CorrelationId"] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (LogContext.PushProperty("correlationId", correlationId))
        {
            await _next(context);
        }
    }

    private static bool IsValid(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length is >= 8 and <= 128
        && value.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == '.');
}
