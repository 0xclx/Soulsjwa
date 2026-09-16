using Soulsjwa.Api.Features.Auth.Entities;

namespace Soulsjwa.Api.Features.Events.Entities;

public class EventCompetitor
{
    public Guid EventId { get; set; }
    public Event Event { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    public bool IsLive { get; set; }
    public bool IsStreamer { get; set; }
    public ICollection<EventCompetitorModerator> Moderators { get; set; } = [];
}
