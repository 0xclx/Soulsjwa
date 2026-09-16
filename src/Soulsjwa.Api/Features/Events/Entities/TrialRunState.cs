namespace Soulsjwa.Api.Features.Events.Entities;

/// <summary>
/// Lifecycle of one <see cref="TrialRun"/> session:
/// <c>start</c> takes NotStarted or Paused to Running; <c>stop</c> takes
/// Running to Paused; <c>reset</c> takes any state back to NotStarted, clearing
/// timestamps and deleting this run's trial completions/failures.
/// <see cref="Completed"/> is reserved for a run explicitly finished rather than
/// merely paused; nothing transitions to it yet.
/// </summary>
public enum TrialRunState
{
    NotStarted = 0,
    Running = 1,
    Paused = 2,
    Completed = 3,
}
