using Soulsjwa.Api.Features.Auth.Entities;

namespace Soulsjwa.Api.Features.Events.Entities;

public enum EventGameCompetitorInfoType
{
    /// <summary>
    /// A Twitch or YouTube VOD/clip URL marking a death during the run. The
    /// scoreboard renders a 💀 marker next to the game when a competitor has
    /// any death clip; the frontend may also embed it.
    /// </summary>
    DeathClip = 0,

    /// <summary>An arbitrary HTTPS URL (build link, write-up, etc.).</summary>
    Link = 1,

    /// <summary>Plain-text note. Not rendered as a clickable link.</summary>
    Other = 2,
}

/// <summary>
/// Metadata a competitor (or someone acting on their behalf) can attach to one
/// <see cref="EventGame"/>. Multiple rows per (eventGame, competitor) are
/// allowed — e.g. several death clips for a multi-death run.
///
/// The endpoint layer enforces who may author: admin, the competitor
/// themselves, or an <see cref="EventCompetitorModerator"/> delegated for that
/// competitor (when the competitor is marked as a streamer in the event).
/// </summary>
public class EventGameCompetitorInfo
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid EventGameId { get; set; }
    public EventGame EventGame { get; set; } = null!;

    /// <summary>The competitor the info belongs to (not necessarily its author).</summary>
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public EventGameCompetitorInfoType Type { get; set; }

    /// <summary>
    /// URL value for <see cref="EventGameCompetitorInfoType.DeathClip"/> (Twitch/YouTube
    /// only) and <see cref="EventGameCompetitorInfoType.Link"/> (any HTTPS). Null for
    /// <see cref="EventGameCompetitorInfoType.Other"/>.
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// Free-form text. Required for <see cref="EventGameCompetitorInfoType.Other"/>;
    /// optional caption for <see cref="EventGameCompetitorInfoType.DeathClip"/> and
    /// <see cref="EventGameCompetitorInfoType.Link"/>.
    /// </summary>
    public string? Text { get; set; }

    public Guid CreatedById { get; set; }
    public User CreatedBy { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
