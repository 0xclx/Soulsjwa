using System.Runtime.CompilerServices;
using FluentAssertions;
using Xunit;

namespace Soulsjwa.UnitTests;

/// <summary>
/// Runs this test assembly with a non-UTC local time zone. The server must
/// treat every time as UTC and never consult the machine's zone; on a UTC
/// machine (CI, containers) a leak of server-local time is invisible, so pin
/// an odd, DST-free offset (+05:45) that makes any leak fail loudly.
/// </summary>
internal static class NonUtcLocalTimeZone
{
    public const string Id = "Asia/Kathmandu";

    [ModuleInitializer]
    internal static void Pin()
    {
        Environment.SetEnvironmentVariable("TZ", Id);
        TimeZoneInfo.ClearCachedData();
    }
}

public class NonUtcLocalTimeZoneTests
{
    [Fact]
    public void LocalTimeZone_IsPinnedAwayFromUtc()
    {
        // Guards the pin itself: without it the time-zone tests pass vacuously.
        TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow)
            .Should().Be(TimeSpan.FromMinutes(5 * 60 + 45), "the module initializer pins {0}", NonUtcLocalTimeZone.Id);
    }
}
