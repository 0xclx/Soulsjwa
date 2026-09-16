using System.Diagnostics;
using System.Text.RegularExpressions;
using Serilog.Context;
using Soulsjwa.Api.Diagnostics;

namespace Soulsjwa.Api.Middleware;

public partial class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    /// <summary>The correlation-id header/HttpContext.Items key, shared with any code that needs to read or set it outside this middleware.</summary>
    public const string HeaderName = "X-Correlation-Id";

    private const int MaxCorrelationIdLength = 128;

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GetCorrelationId(context);
        context.Items[HeaderName] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        var stopwatch = Stopwatch.StartNew();
        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("TraceId", Activity.Current?.TraceId.ToString()))
        using (LogContext.PushProperty("SpanId", Activity.Current?.SpanId.ToString()))
        {
            try
            {
                await next(context);
            }
            finally
            {
                // Overlaps with ASP.NET Core's own http.server.request.duration,
                // kept because it is tagged with the route *template* and the
                // app's meter name, which the dashboards key on; the framework
                // metric is not guaranteed to carry the template for every
                // endpoint type (the SPA fallback, health checks).
                stopwatch.Stop();
                var route = GetRouteName(context);
                DiagnosticsConfig.RecordRequest(
                    context.Request.Method,
                    route,
                    context.Response.StatusCode,
                    stopwatch.Elapsed.TotalMilliseconds);
            }
        }
    }

    private string GetCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(HeaderName, out var values))
        {
            var candidate = values.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                if (IsValidCorrelationId(candidate))
                    return candidate;

                logger.CorrelationIdRejected(candidate.Length);
            }
        }

        return Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");
    }

    private static bool IsValidCorrelationId(string value) =>
        value.Length <= MaxCorrelationIdLength
        && CorrelationIdRegex().IsMatch(value);

    [GeneratedRegex("^[A-Za-z0-9_.:-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex CorrelationIdRegex();

    private static string GetRouteName(HttpContext context) =>
        (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText
        ?? "unmatched";
}
