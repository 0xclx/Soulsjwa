namespace Soulsjwa.Api.Features.TwitchExtension.Entities;

/// <summary>
/// Which figures the extension opens on for a channel's viewers. Viewers can
/// switch scope themselves in the panel; this is only the default the
/// broadcaster picked. Exposed on the wire by name.
/// </summary>
public enum TwitchExtensionScope
{
    /// <summary>Official totals across every game — the app's own ranking.</summary>
    AllGames = 0,

    /// <summary>The event's single enabled game, whichever it is right now.</summary>
    ActiveGame = 1,

    /// <summary>One game the broadcaster chose (<c>PinnedEventGameId</c>), active or not.</summary>
    PinnedGame = 2,
}
