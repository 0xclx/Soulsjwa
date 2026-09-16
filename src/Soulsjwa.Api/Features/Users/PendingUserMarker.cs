namespace Soulsjwa.Api.Features.Users;

/// <summary>
/// Sentinel prefix for the <see cref="Soulsjwa.Api.Features.Auth.Entities.User.TwitchId"/>
/// column when the User row was created as a placeholder for a Twitch handle
/// that has never actually logged in. On the first real Twitch login the
/// placeholder is adopted (real id swapped in) by <c>TwitchAuthService</c>.
/// </summary>
public static class PendingUserMarker
{
    public const string Prefix = "pending:";

    public static string For(string twitchLoginLower) => Prefix + twitchLoginLower;

    public static bool IsPending(string twitchId) =>
        !string.IsNullOrEmpty(twitchId) && twitchId.StartsWith(Prefix, StringComparison.Ordinal);
}
