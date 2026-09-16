using Soulsjwa.Api.Features.Auth.Entities;
using Soulsjwa.Api.Features.Events.Entities;

namespace Soulsjwa.Api.Features.TwitchExtension.Entities;

/// <summary>
/// What one Twitch channel's installed extension shows. At most one row per
/// channel, and no row at all is the common case: a channel with no row
/// follows the site's featured event with default presentation. A row exists
/// only once the broadcaster saved settings, which requires a Soulsjwa account
/// signed in with that same Twitch account (<see cref="UpdatedById"/>), so the
/// change is attributable and audited like every other write.
/// </summary>
public class TwitchExtensionChannelSettings
{
    /// <summary>
    /// The channel's Twitch user id — the <c>channel_id</c> claim of the
    /// tokens Twitch issues for it, and the same value as
    /// <see cref="User.TwitchId"/> for the broadcaster's own account. No FK to
    /// Users on purpose: the id identifies a channel, and viewers' tokens name
    /// it whether or not the broadcaster ever signed in here.
    /// </summary>
    public string ChannelId { get; set; } = string.Empty;

    /// <summary>The event to show; null means "follow the featured event".</summary>
    public Guid? EventId { get; set; }
    public Event? Event { get; set; }

    public TwitchExtensionScope DefaultScope { get; set; } = TwitchExtensionScope.AllGames;

    /// <summary>Only meaningful with <see cref="TwitchExtensionScope.PinnedGame"/>; must be a game of the resolved event.</summary>
    public Guid? PinnedEventGameId { get; set; }
    public EventGame? PinnedEventGame { get; set; }

    /// <summary>Mark the broadcaster's own row when they compete in the shown event.</summary>
    public bool HighlightChannelCompetitor { get; set; } = true;

    /// <summary>Show trial-run figures in amber beside the official ones, as the app does.</summary>
    public bool ShowTrialProgress { get; set; } = true;

    /// <summary>The Soulsjwa user who last saved these settings — the broadcaster, or an admin acting on their channel.</summary>
    public Guid UpdatedById { get; set; }
    public User UpdatedBy { get; set; } = null!;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
