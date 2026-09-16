using Soulsjwa.Api.Features.Auth.Entities;

namespace Soulsjwa.Api.Features.Events.Entities;

public class Event
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? UrlAlias { get; set; }
    public string Description { get; set; } = string.Empty;
    public Guid CreatedById { get; set; }
    public User CreatedBy { get; set; } = null!;

    public bool IsArchived { get; set; }
    public bool IsStarted { get; set; }

    /// <summary>
    /// When the event was last started; null until the first start. Set by
    /// the start endpoint, kept across stop so "was ever run" survives.
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// When the event was last stopped; cleared again on start. A not-started
    /// event with this set has run and finished ("stopped"), one without has
    /// never run ("upcoming") — state that used to be reconstructed from audit
    /// rows, which retention may have deleted.
    /// </summary>
    public DateTime? StoppedAt { get; set; }

    /// <summary>
    /// True for at most one event at a time. The featured event is surfaced
    /// on the public landing page for unauthenticated users. Featuring an
    /// event clears this flag on whatever event previously held it.
    /// </summary>
    public bool IsFeatured { get; set; }

    /// <summary>
    /// Defaults to <see cref="Entities.TieBreakMode.SharedPlace"/>: an equal
    /// score is an equal placing unless an owner opts into splitting ties by
    /// completion time. The default is applied here, not by the database, so
    /// existing rows keep the mode they were created with and their published
    /// standings do not move.
    /// </summary>
    public TieBreakMode TieBreakMode { get; set; } = TieBreakMode.SharedPlace;

    /// <summary>
    /// Owner/admin switch (default on) gating whether competitors may enable
    /// trial/training runs for this event's games. Only gates
    /// creating new ones; existing trial progress never reaches the official
    /// scoreboard either way.
    /// </summary>
    public bool AllowTrialRuns { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<EventCompetitor> Competitors { get; set; } = [];
    public ICollection<EventGame> EventGames { get; set; } = [];
}
