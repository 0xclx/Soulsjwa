using System.Text.RegularExpressions;
using Serilog.Core;
using Serilog.Events;

namespace Soulsjwa.Api.Diagnostics;

/// <summary>
/// Redacts any <c>token=</c> query-string value from every log event before it
/// reaches a sink, whichever logger produced it. The leak is ASP.NET Core's own
/// hosting diagnostics, whose "Request starting/finished" events carry the full
/// query string as a structured <c>QueryString</c> property — not
/// <c>UseSerilogRequestLogging</c>, which logs only <c>RequestPath</c>. Those
/// events are Debug-level in Production (<c>Microsoft.AspNetCore</c> is overridden
/// to Warning) but Information-level in Development, where they reach the
/// console/OTLP sinks with the raw overlay token inline.
/// </summary>
public sealed partial class SensitiveQueryStringRedactor : ILogEventEnricher
{
    [GeneratedRegex(@"(token=)[^&""]*", RegexOptions.IgnoreCase)]
    private static partial Regex TokenQueryValuePattern();

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        Redact(logEvent, propertyFactory, "QueryString");
        Redact(logEvent, propertyFactory, "RequestPath");
    }

    private static void Redact(LogEvent logEvent, ILogEventPropertyFactory propertyFactory, string propertyName)
    {
        if (!logEvent.Properties.TryGetValue(propertyName, out var value)) return;
        if (value is not ScalarValue { Value: string text }) return;
        if (!text.Contains("token=", StringComparison.OrdinalIgnoreCase)) return;

        var redacted = TokenQueryValuePattern().Replace(text, "$1REDACTED");
        logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty(propertyName, redacted));
    }
}
