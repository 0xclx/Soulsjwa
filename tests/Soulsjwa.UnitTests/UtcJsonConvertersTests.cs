using System.Text.Json;
using FluentAssertions;
using Soulsjwa.Api.Common;
using Xunit;

namespace Soulsjwa.UnitTests;

/// <summary>
/// The server reads and writes every time as UTC. A zone-less timestamp is
/// UTC, never the machine's local time — this assembly runs in a non-UTC
/// zone (see <see cref="NonUtcLocalTimeZone"/>), so a local reading fails here.
/// </summary>
public class UtcJsonConvertersTests
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        UtcJsonConverters.AddTo(options.Converters);
        return options;
    }

    private static readonly DateTime SixPmUtc = new(2026, 10, 1, 18, 0, 0, DateTimeKind.Utc);

    private sealed record OffsetBody(DateTimeOffset At, DateTimeOffset? Maybe);
    private sealed record DateTimeBody(DateTime At, DateTime? Maybe);

    [Theory]
    [InlineData("2026-10-01T18:00:00")]
    [InlineData("2026-10-01T18:00:00Z")]
    [InlineData("2026-10-01T20:00:00+02:00")]
    [InlineData("2026-10-01T18:00:00.000")]
    public void DateTimeOffset_ReadsEveryFormAsTheSameUtcInstant(string raw)
    {
        var body = JsonSerializer.Deserialize<OffsetBody>($$"""{"at":"{{raw}}","maybe":"{{raw}}"}""", Options)!;

        body.At.UtcDateTime.Should().Be(SixPmUtc);
        body.Maybe!.Value.UtcDateTime.Should().Be(SixPmUtc);
    }

    [Fact]
    public void DateTimeOffset_ReadsAZonelessValueWithAZeroOffset()
    {
        var body = JsonSerializer.Deserialize<OffsetBody>("""{"at":"2026-10-01T18:00:00"}""", Options)!;

        body.At.Offset.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void DateTimeOffset_KeepsAnExplicitOffset()
    {
        var body = JsonSerializer.Deserialize<OffsetBody>("""{"at":"2026-10-01T20:00:00+02:00"}""", Options)!;

        body.At.Offset.Should().Be(TimeSpan.FromHours(2));
    }

    [Fact]
    public void NullableValues_StayNull()
    {
        JsonSerializer.Deserialize<OffsetBody>("""{"at":"2026-10-01T18:00:00Z","maybe":null}""", Options)!
            .Maybe.Should().BeNull();
        JsonSerializer.Deserialize<DateTimeBody>("""{"at":"2026-10-01T18:00:00Z","maybe":null}""", Options)!
            .Maybe.Should().BeNull();
    }

    [Theory]
    [InlineData("2026-10-01T18:00:00")]
    [InlineData("2026-10-01T18:00:00Z")]
    [InlineData("2026-10-01T20:00:00+02:00")]
    public void DateTime_ReadsEveryFormAsUtcKind(string raw)
    {
        var body = JsonSerializer.Deserialize<DateTimeBody>($$"""{"at":"{{raw}}"}""", Options)!;

        body.At.Should().Be(SixPmUtc);
        body.At.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Theory]
    [InlineData(DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Unspecified)]
    public void DateTime_WritesUtcWithAZ(DateTimeKind kind)
    {
        var json = JsonSerializer.Serialize(
            new DateTimeBody(DateTime.SpecifyKind(SixPmUtc, kind), null), Options);

        json.Should().Contain("\"at\":\"2026-10-01T18:00:00Z\"");
    }

    [Fact]
    public void DateTime_WritesALocalValueAsItsUtcInstant()
    {
        var json = JsonSerializer.Serialize(new DateTimeBody(SixPmUtc.ToLocalTime(), null), Options);

        json.Should().Contain("\"at\":\"2026-10-01T18:00:00Z\"");
    }

    [Theory]
    [InlineData("not a date")]
    [InlineData("10/01/2026 18:00")]
    [InlineData("")]
    public void RejectsAnythingButIso8601(string raw)
    {
        var offset = () => JsonSerializer.Deserialize<OffsetBody>($$"""{"at":"{{raw}}"}""", Options);
        var dateTime = () => JsonSerializer.Deserialize<DateTimeBody>($$"""{"at":"{{raw}}"}""", Options);

        offset.Should().Throw<JsonException>();
        dateTime.Should().Throw<JsonException>();
    }
}
