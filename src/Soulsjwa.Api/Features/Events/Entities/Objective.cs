using Soulsjwa.Api.Features.Games.Entities;

namespace Soulsjwa.Api.Features.Events.Entities;

public class Objective
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? EventGameId { get; set; }
    public EventGame? EventGame { get; set; }
    public int? GameId { get; set; }
    public Game? Game { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Score { get; set; }

    /// <summary>
    /// Free-form grouping label (e.g. Elden Ring area, or "main"/"regular")
    /// used to group objectives in UIs such as the OBS overlay:
    /// Game → Category → Objective.
    /// </summary>
    public string? Category { get; set; }
    public string? Metadata { get; set; }
    public string? Rule { get; set; }

    /// <summary>
    /// JsonLogic rule that, when true against a connector submission, marks the
    /// objective failed rather than completed. Same building blocks as
    /// <see cref="Rule"/>, plus the virtual <c>competitorCompletions</c>
    /// variable (count of OTHER event competitors who already completed it).
    /// A completion match always wins over a failure match in the same
    /// submission — see <c>ConnectorEndpoint.SubmitGameData</c>.
    /// </summary>
    public string? FailRule { get; set; }
    public bool IsPredefined { get; set; }

    /// <summary>
    /// Display order within the event game, ascending. Only meaningful for
    /// event-scoped objectives (<see cref="EventGameId"/> non-null); changed
    /// via the objectives reorder endpoint.
    /// </summary>
    public int SortOrder { get; set; }
    public ICollection<CompletedObjective> CompletedObjectives { get; set; } = [];
    public ICollection<FailedObjective> FailedObjectives { get; set; } = [];
}
