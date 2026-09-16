using FluentAssertions;
using Soulsjwa.Api.Common;
using Xunit;

namespace Soulsjwa.UnitTests;

public class UtcTimeTests
{
    [Fact]
    public void ToStorage_DateTimeOffset_Zulu_ConvertsToUtcDateTime()
    {
        var value = DateTimeOffset.Parse("2026-01-01T10:00:00Z");

        var result = UtcTime.ToStorage(value);

        result.Kind.Should().Be(DateTimeKind.Utc);
        result.Should().Be(new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void ToStorage_DateTimeOffset_WithPositiveOffset_ConvertsToTheCorrectUtcInstant()
    {
        // 12:00 at +02:00 is 10:00 UTC — the offset form must round-trip the
        // exact instant, not the wall-clock hour. DateTime.SpecifyKind would
        // have stamped this as 12:00 UTC instead.
        var value = DateTimeOffset.Parse("2026-01-01T12:00:00+02:00");

        var result = UtcTime.ToStorage(value);

        result.Kind.Should().Be(DateTimeKind.Utc);
        result.Should().Be(new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void ToStorage_DateTimeOffset_WithNegativeOffset_ConvertsToTheCorrectUtcInstant()
    {
        var value = DateTimeOffset.Parse("2026-01-01T05:00:00-05:00");

        var result = UtcTime.ToStorage(value);

        result.Should().Be(new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void ToStorage_DateTime_Utc_ReturnsUnchanged()
    {
        var value = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);

        var result = UtcTime.ToStorage(value);

        result.Should().Be(value);
        result.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void ToStorage_DateTime_Local_ConvertsToUtc()
    {
        var value = DateTime.SpecifyKind(new DateTime(2026, 1, 1, 10, 0, 0), DateTimeKind.Local);

        var result = UtcTime.ToStorage(value);

        result.Kind.Should().Be(DateTimeKind.Utc);
        result.Should().Be(value.ToUniversalTime());
    }

    [Fact]
    public void ToStorage_DateTime_Unspecified_ThrowsRatherThanGuessing()
    {
        var value = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Unspecified);

        var act = () => UtcTime.ToStorage(value);

        act.Should().Throw<ArgumentException>();
    }
}
