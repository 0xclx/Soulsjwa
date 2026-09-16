using Soulsjwa.Api.Features.Auth.Entities;

namespace Soulsjwa.Api.Features.Events.Entities;

/// <summary>
/// Mutually exclusive with <see cref="CompletedObjective"/> for the same
/// (ObjectiveId, UserId): an objective is exactly one of pending (no row in
/// either table), completed, or failed. Resetting a failure deletes this row,
/// so there is no separate "manual reset" flag.
/// </summary>
public class FailedObjective
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ObjectiveId { get; set; }
    public Objective Objective { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public DateTime FailedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Null for an official failure, otherwise the <see cref="TrialRun"/> it was
    /// recorded under. Official scoring and the overlay always filter
    /// <c>TrialRunId IS NULL</c>, so trial progress never reaches them.
    /// </summary>
    public Guid? TrialRunId { get; set; }
    public TrialRun? TrialRun { get; set; }

    /// <summary>
    /// In-game time (ms) at failure, as reported by the connector. <c>null</c>
    /// for manual failures and games without an in-game timer. Mirrors
    /// <see cref="CompletedObjective.InGameTimeMs"/>.
    /// </summary>
    public long? InGameTimeMs { get; set; }
}
