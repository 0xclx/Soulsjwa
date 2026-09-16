using Soulsjwa.Api.Features.Auth.Entities;

namespace Soulsjwa.Api.Features.Events.Entities;

public class CompletedObjective
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ObjectiveId { get; set; }
    public Objective Objective { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Null for an official completion, otherwise the <see cref="TrialRun"/> it
    /// was recorded under. Official scoring and the overlay always filter
    /// <c>TrialRunId IS NULL</c>, so trial progress never reaches them.
    /// </summary>
    public Guid? TrialRunId { get; set; }
    public TrialRun? TrialRun { get; set; }

    /// <summary>
    /// In-game time (ms) at completion, as reported by the connector. Preferred
    /// scoreboard tie-breaker, so competitors playing at different real-world
    /// times still rank fairly. <c>null</c> for manual completions, custom
    /// games, and games without an in-game timer.
    /// </summary>
    public long? InGameTimeMs { get; set; }
}
