using Soulsjwa.Api.Features.Auth.Entities;

namespace Soulsjwa.Api.Features.Events.Entities;

/// <summary>
/// A user delegated by a streamer competitor to mark objectives complete on
/// that competitor's behalf. Delegation is scoped to the (event, streamer)
/// pair — one row per relationship, so the same user can moderate for several
/// streamers or several events.
/// </summary>
public class EventCompetitorModerator
{
    public Guid EventId { get; set; }

    /// <summary>The streamer competitor who delegated this moderator.</summary>
    public Guid CompetitorUserId { get; set; }
    public EventCompetitor Competitor { get; set; } = null!;

    public Guid ModeratorUserId { get; set; }
    public User Moderator { get; set; } = null!;

    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}
