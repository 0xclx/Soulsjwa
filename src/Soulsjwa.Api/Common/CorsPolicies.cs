namespace Soulsjwa.Api.Common;

/// <summary>
/// Named CORS policies. The SPA uses the unnamed default policy (one
/// configurable origin, credentials allowed); anything else that calls this
/// API cross-origin gets its own, narrower policy here.
/// </summary>
public static class CorsPolicies
{
    /// <summary>
    /// The Twitch-hosted extension front end: its origin is Twitch's CDN, it
    /// sends a bearer token rather than cookies, and it only ever reads the
    /// extension routes and saves settings.
    /// </summary>
    public const string TwitchExtension = "twitch-extension";
}
