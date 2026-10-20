using Serilog.Context;

namespace Garbati.StaffIdentityApi.Middleware;

// Gives every request an id that follows it through logs, error bodies and the response
// (ARCHITECTURE.md Section 5: "X-Correlation-ID on every call"). This service makes no downstream calls
// of its own (DESIGN.md Section 1), so unlike OrchestratorApi's middleware of the same name, there is no
// "pass it on to every downstream call" half here. A caller may send its own id (so its logs and ours
// line up); otherwise one is generated.
public class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-ID";
    private const int MaxLength = 64;

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ReadValidIncomingId(context) ?? Guid.NewGuid().ToString("N");

        // TraceIdentifier is ASP.NET's own per-request id; reusing it means everything that already
        // reads it (framework logs, error bodies) gets the same value.
        context.TraceIdentifier = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        // Every log line written while this request runs carries a CorrelationId property.
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }

    // Only accept short ids made of safe characters; anything else is replaced, never logged as-is.
    private static string? ReadValidIncomingId(HttpContext context)
    {
        var value = context.Request.Headers[HeaderName].ToString();

        return value.Length is > 0 and <= MaxLength
            && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.')
            ? value
            : null;
    }
}
