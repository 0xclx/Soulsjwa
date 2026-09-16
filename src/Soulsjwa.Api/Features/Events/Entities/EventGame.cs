using Soulsjwa.Api.Features.Games.Entities;

namespace Soulsjwa.Api.Features.Events.Entities;

public class EventGame
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    public Event Event { get; set; } = null!;

    /// <summary>
    /// FK to a predefined (known) <see cref="Game"/>. Null when this is a custom game.
    /// </summary>
    public int? KnownGameId { get; set; }
    public Game? KnownGame { get; set; }

    /// <summary>
    /// Display name for a custom (per-event) game. Required when <see cref="KnownGameId"/> is null.
    /// </summary>
    public string? CustomGameName { get; set; }

    public string? CustomGameDescription { get; set; }

    public bool IsEnabled { get; set; }

    /// <summary>
    /// Display order within the event, ascending. Assigned incrementally at
    /// creation; changed via the games reorder endpoint.
    /// </summary>
    public int SortOrder { get; set; }

    public bool IsCustomGame => KnownGameId is null;

    public ICollection<Objective> Objectives { get; set; } = [];

    public ICollection<EventGameCompetitorInfo> CompetitorInfos { get; set; } = [];
}
