using Soulsjwa.Api.Features.Auth.Entities;

namespace Soulsjwa.Api.Features.Events.Entities;

/// <summary>
/// A competitor's trial/training slot for one (event, game) pair —
/// at most one per (EventGameId, UserId), DB-enforced. Enabling trial mode
/// creates the row; disabling it deletes the row, which cascade-deletes exactly
/// this competitor's trial <see cref="CompletedObjective"/>/<see cref="FailedObjective"/>
/// rows for this game and nothing adjacent.
///
/// Reset (clear timestamps and this run's completions, keep the row so the
/// competitor need not re-enable) lives in the endpoint layer, not as a flag here.
/// </summary>
public class TrialRun
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid EventId { get; set; }
    public Event Event { get; set; } = null!;

    public Guid EventGameId { get; set; }
    public EventGame EventGame { get; set; } = null!;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public TrialRunState State { get; set; } = TrialRunState.NotStarted;

    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }

    public ICollection<CompletedObjective> CompletedObjectives { get; set; } = [];
    public ICollection<FailedObjective> FailedObjectives { get; set; } = [];
}
