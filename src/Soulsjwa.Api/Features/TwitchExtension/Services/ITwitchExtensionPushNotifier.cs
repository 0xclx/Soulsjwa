namespace Soulsjwa.Api.Features.TwitchExtension.Services;

/// <summary>
/// Tells running extension front ends that something they show has changed,
/// through Twitch's Extension PubSub, so they refetch at once instead of on
/// their next poll. Both calls are fire-and-forget: a lost push costs a few
/// seconds of staleness, never correctness, since every panel still polls.
/// </summary>
public interface ITwitchExtensionPushNotifier
{
    /// <summary>An event's scoreboard changed; every channel showing it should refetch.</summary>
    void ScoreboardChanged(Guid eventId);

    /// <summary>A channel's settings changed; its viewers should reload the board.</summary>
    void ConfigurationChanged(string channelId);
}

/// <summary>What runs when the server cannot sign Twitch API tokens (no <c>TwitchExtension:OwnerUserId</c>): viewers just poll.</summary>
public sealed class NullTwitchExtensionPushNotifier : ITwitchExtensionPushNotifier
{
    public void ScoreboardChanged(Guid eventId) { }

    public void ConfigurationChanged(string channelId) { }
}
