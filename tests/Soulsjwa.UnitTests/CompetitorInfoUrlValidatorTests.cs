using FluentAssertions;
using Soulsjwa.Api.Features.Events;
using Xunit;

namespace Soulsjwa.UnitTests;

public class CompetitorInfoUrlValidatorTests
{
    [Theory]
    [InlineData("https://www.twitch.tv/videos/123456789")]
    [InlineData("https://twitch.tv/somechannel/clip/HappyClip-AbCdEfG")]
    [InlineData("https://clips.twitch.tv/AbCdEfG")]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ")]
    [InlineData("https://m.youtube.com/shorts/AbCdEfG")]
    public void IsValidDeathClipUrl_AcceptsTwitchAndYouTube(string url)
    {
        CompetitorInfoUrlValidator.IsValidDeathClipUrl(url).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a url")]
    [InlineData("http://www.twitch.tv/videos/1")] // http rejected for death clips (embed safety)
    [InlineData("https://example.com/video")]
    [InlineData("https://evil.twitch.tv.attacker.com/x")]
    [InlineData("ftp://twitch.tv/x")]
    public void IsValidDeathClipUrl_RejectsEverythingElse(string? url)
    {
        CompetitorInfoUrlValidator.IsValidDeathClipUrl(url).Should().BeFalse();
    }

    [Theory]
    [InlineData("https://example.com/build")]
    [InlineData("http://localhost:5173/x")]
    [InlineData("https://www.twitch.tv/videos/1")]
    public void IsValidLinkUrl_AcceptsAnyHttpOrHttps(string url)
    {
        CompetitorInfoUrlValidator.IsValidLinkUrl(url).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("/relative")]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://example.com")]
    public void IsValidLinkUrl_RejectsNonHttp(string? url)
    {
        CompetitorInfoUrlValidator.IsValidLinkUrl(url).Should().BeFalse();
    }
}
