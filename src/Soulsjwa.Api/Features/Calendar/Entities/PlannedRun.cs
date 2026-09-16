using Soulsjwa.Api.Features.Auth.Entities;
using Soulsjwa.Api.Features.Events.Entities;

namespace Soulsjwa.Api.Features.Calendar.Entities;

/// <summary>
/// A competitor's scheduled play session for one of an event's games; several
/// may exist per competitor. Readable by everyone, written by whoever
/// <see cref="EventOwnership.RequireCanEditCompetitorInfoAsync"/> allows (the
/// competitor, the event owner, an admin, or a delegated moderator).
/// <see cref="Color"/> reuses <see cref="CalendarEntry.Color"/>'s named-slot
/// system so both kinds of calendar item repaint together under a custom theme.
/// </summary>
public class PlannedRun
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid EventId { get; set; }
    public Event Event { get; set; } = null!;

    public Guid EventGameId { get; set; }
    public EventGame EventGame { get; set; } = null!;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }

    public CalendarEntryColor Color { get; set; } = CalendarEntryColor.Default;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
