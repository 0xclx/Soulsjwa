namespace Soulsjwa.Api.Features.Events;

/// <summary>
/// URL validation for <see cref="Entities.EventGameCompetitorInfo"/>. DeathClip
/// URLs are restricted to Twitch and YouTube hosts so the frontend can embed
/// them safely; generic Link URLs only need to be absolute HTTPS (or HTTP for
/// localhost dev).
/// </summary>
public static class CompetitorInfoUrlValidator
{
    private static readonly HashSet<string> TwitchHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "twitch.tv",
        "www.twitch.tv",
        "m.twitch.tv",
        "clips.twitch.tv",
    };

    private static readonly HashSet<string> YouTubeHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "youtube.com",
        "www.youtube.com",
        "m.youtube.com",
        "music.youtube.com",
        "youtu.be",
    };

    public static bool IsValidLinkUrl(string? url) =>
        TryParseHttpUrl(url, out _);

    public static bool IsValidDeathClipUrl(string? url)
    {
        if (!TryParseHttpUrl(url, out var uri)) return false;
        if (uri.Scheme != Uri.UriSchemeHttps) return false;
        return TwitchHosts.Contains(uri.Host) || YouTubeHosts.Contains(uri.Host);
    }

    private static bool TryParseHttpUrl(string? url, out Uri uri)
    {
        uri = null!;
        if (string.IsNullOrWhiteSpace(url)) return false;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed)) return false;
        if (parsed.Scheme != Uri.UriSchemeHttps && parsed.Scheme != Uri.UriSchemeHttp) return false;
        uri = parsed;
        return true;
    }
}
