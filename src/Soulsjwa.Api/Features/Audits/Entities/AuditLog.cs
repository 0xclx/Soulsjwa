using Soulsjwa.Api.Features.Auth.Entities;
using Soulsjwa.Api.Features.Events.Entities;

namespace Soulsjwa.Api.Features.Audits.Entities;

/// <summary>
/// Immutable record of a user-driven mutation. Rows are append-only — never
/// updated or deleted in normal operation — so each captures both the before and
/// after snapshots needed for a git-diff style review.
///
/// <see cref="Type"/> is a string rather than an enum so a new action type needs
/// no migration, and so rows referencing a since-renamed type still render.
/// Scope narrows through <see cref="EventId"/>, <see cref="EventGameId"/>,
/// <see cref="ObjectiveId"/>.
/// </summary>
public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>String key from <see cref="AuditEventTypes"/>.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Null for non-event actions (role changes, allowlist).</summary>
    public Guid? EventId { get; set; }
    public Event? Event { get; set; }

    public Guid? EventGameId { get; set; }
    public Guid? ObjectiveId { get; set; }

    /// <summary>The principal that initiated the action.</summary>
    public Guid ActorUserId { get; set; }
    public User Actor { get; set; } = null!;

    /// <summary>The user the action was performed on or on behalf of.</summary>
    public Guid? SubjectUserId { get; set; }
    public User? Subject { get; set; }

    /// <summary>Null for creates.</summary>
    public string? BeforeJson { get; set; }

    /// <summary>Null for deletes.</summary>
    public string? AfterJson { get; set; }

    /// <summary>Required for some actions (e.g. completion-time edits).</summary>
    public string? Reason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
