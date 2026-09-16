namespace Soulsjwa.Api.Common;

public static class CacheTags
{
    private const string ScoreboardPrefix = "scoreboard";

    /// <summary>
    /// One event's cached scoreboard/score/overlay responses. Per-event rather than
    /// one global tag so a write in one event doesn't evict every other event's.
    /// </summary>
    public static string Scoreboard(Guid eventId) => $"{ScoreboardPrefix}:{eventId:N}";

    /// <summary>
    /// The inverse of <see cref="Scoreboard"/>: recognises an eviction of one
    /// event's scoreboard, which is the single signal that "the board changed"
    /// (every write path evicts it) and so what the Twitch push rides on.
    /// </summary>
    public static bool TryParseScoreboard(string tag, out Guid eventId)
    {
        eventId = Guid.Empty;
        const string prefix = ScoreboardPrefix + ":";
        return tag.StartsWith(prefix, StringComparison.Ordinal)
            && Guid.TryParseExact(tag.AsSpan(prefix.Length), "N", out eventId);
    }

    /// <summary>One tag, not per-event: the global calendar endpoint aggregates across every event.</summary>
    public const string Calendar = "calendar";

    private const string PredefinedObjectivesPrefix = "predefined-objectives";

    /// <summary>
    /// One game's cached predefined-objective catalog. Per-game so adding a template
    /// to one game doesn't evict every other game's cached list.
    /// </summary>
    public static string PredefinedObjectives(int gameId) => $"{PredefinedObjectivesPrefix}:{gameId}";

    /// <summary>Tag for the unfiltered (no gameId) cached catalog fetch — the admin catalog table's whole-list view.</summary>
    public const string PredefinedObjectivesAll = $"{PredefinedObjectivesPrefix}:all";

    private const string TwitchExtensionChannelPrefix = "twitch-extension";

    /// <summary>
    /// Every channel's cached extension responses at once — evicted when an
    /// admin changes the extension-wide rules, which every response embeds.
    /// </summary>
    public const string TwitchExtensionAll = TwitchExtensionChannelPrefix;

    /// <summary>
    /// One Twitch channel's cached extension responses. Evicted when the
    /// broadcaster saves settings; the same entries also carry the shown
    /// event's <see cref="Scoreboard"/> tag, so score changes evict them too.
    /// </summary>
    public static string TwitchExtensionChannel(string channelId) => $"{TwitchExtensionChannelPrefix}:{channelId}";
}
