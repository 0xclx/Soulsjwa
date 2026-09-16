namespace Soulsjwa.Api.Features.Events.Entities;

/// <summary>
/// How scoreboard ties (same total score) are resolved for an event.
/// </summary>
public enum TieBreakMode
{
    /// <summary>
    /// Tied competitors are ordered by completion time, each getting a distinct
    /// rank (1, 2, 3 …). Completion time prefers the connector's in-game time
    /// and falls back to real-world time when it is absent (manual completions,
    /// non-connector games, or mixed competitor data).
    /// </summary>
    ByTime = 0,

    /// <summary>
    /// Default. Equal scores share a rank regardless of completion time, using
    /// standard competition ranking ("1224"): two tied for 1st are both 1st and
    /// the next is 3rd.
    /// </summary>
    SharedPlace = 1,
}
