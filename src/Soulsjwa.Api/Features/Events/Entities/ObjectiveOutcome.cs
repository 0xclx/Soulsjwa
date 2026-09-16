namespace Soulsjwa.Api.Features.Events.Entities;

/// <summary>
/// The exactly-one-of-three outcome state of an objective for a given
/// competitor. Exposed on the wire as its <c>nameof</c> string, so API
/// responses never hand-roll "pending"/"completed"/"failed" literals.
/// </summary>
public enum ObjectiveOutcome
{
    Pending,
    Completed,
    Failed,
}

/// <summary>
/// Derives <see cref="ObjectiveOutcome"/> per objective and for a competitor's
/// overall progress: pending while any objective is still pending, then
/// completed if all succeeded, failed if at least one failed.
/// </summary>
public static class ObjectiveOutcomeCalculator
{
    public static ObjectiveOutcome ForObjective(bool isCompleted, bool isFailed) =>
        isCompleted ? ObjectiveOutcome.Completed
        : isFailed ? ObjectiveOutcome.Failed
        : ObjectiveOutcome.Pending;

    public static ObjectiveOutcome ForCompetitor(int totalCount, int completedCount, int failedCount)
    {
        if (totalCount == 0) return ObjectiveOutcome.Pending;
        if (completedCount + failedCount < totalCount) return ObjectiveOutcome.Pending;
        return failedCount > 0 ? ObjectiveOutcome.Failed : ObjectiveOutcome.Completed;
    }
}
