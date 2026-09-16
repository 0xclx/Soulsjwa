using FluentAssertions;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;
using Soulsjwa.Api.Diagnostics;
using Xunit;

namespace Soulsjwa.UnitTests;

public class SensitiveQueryStringRedactorTests
{
    private static readonly SensitiveQueryStringRedactor Redactor = new();

    private sealed class NoopPropertyFactory : ILogEventPropertyFactory
    {
        public LogEventProperty CreateProperty(string name, object? value, bool destructureObjects = false) =>
            new(name, new ScalarValue(value));
    }

    private static LogEvent CreateEvent(string propertyName, string propertyValue)
    {
        var properties = new List<LogEventProperty> { new(propertyName, new ScalarValue(propertyValue)) };
        return new LogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            null,
            new MessageTemplateParser().Parse("test"),
            properties);
    }

    [Fact]
    public void Enrich_QueryStringWithToken_RedactsTheTokenValue()
    {
        var logEvent = CreateEvent("QueryString", "?token=ot_SuperSecretValue123&other=1");

        Redactor.Enrich(logEvent, new NoopPropertyFactory());

        var value = ((ScalarValue)logEvent.Properties["QueryString"]).Value as string;
        value.Should().Be("?token=REDACTED&other=1");
    }

    [Fact]
    public void Enrich_RequestPathWithToken_RedactsTheTokenValue()
    {
        var logEvent = CreateEvent("RequestPath", "/api/v1/events/1/overlay-scoreboard?token=ot_abc123");

        Redactor.Enrich(logEvent, new NoopPropertyFactory());

        var value = ((ScalarValue)logEvent.Properties["RequestPath"]).Value as string;
        value.Should().Be("/api/v1/events/1/overlay-scoreboard?token=REDACTED");
    }

    [Fact]
    public void Enrich_NoTokenInQueryString_LeavesPropertyUnchanged()
    {
        var logEvent = CreateEvent("QueryString", "?from=2026-01-01&to=2026-02-01");

        Redactor.Enrich(logEvent, new NoopPropertyFactory());

        var value = ((ScalarValue)logEvent.Properties["QueryString"]).Value as string;
        value.Should().Be("?from=2026-01-01&to=2026-02-01");
    }

    [Fact]
    public void Enrich_NoQueryStringOrRequestPathProperty_DoesNothing()
    {
        var logEvent = CreateEvent("SomeOtherProperty", "token=should-not-matter");

        var act = () => Redactor.Enrich(logEvent, new NoopPropertyFactory());

        act.Should().NotThrow();
        ((ScalarValue)logEvent.Properties["SomeOtherProperty"]).Value.Should().Be("token=should-not-matter");
    }

    [Fact]
    public void Enrich_TokenIsCaseInsensitive()
    {
        var logEvent = CreateEvent("QueryString", "?TOKEN=ot_Secret");

        Redactor.Enrich(logEvent, new NoopPropertyFactory());

        var value = ((ScalarValue)logEvent.Properties["QueryString"]).Value as string;
        value.Should().Be("?TOKEN=REDACTED");
    }
}
