using System.Text.Json;
using Soulsjwa.Api.Features.Audits.Entities;
using Soulsjwa.Api.Infrastructure.Data;

namespace Soulsjwa.Api.Features.Audits.Services;

/// <summary>
/// Appends <see cref="AuditLog"/> rows. Callers build their primary change, call
/// <see cref="Log"/>, then <c>SaveChangesAsync</c> once so both writes land in
/// the same transaction.
/// </summary>
public interface IAuditService
{
    /// <summary>
    /// Stages the row only — the caller commits it with <c>SaveChangesAsync</c>.
    /// </summary>
    AuditLog Log(
        AppDbContext db,
        string type,
        Guid actorUserId,
        Guid? eventId = null,
        Guid? eventGameId = null,
        Guid? objectiveId = null,
        Guid? subjectUserId = null,
        object? before = null,
        object? after = null,
        string? reason = null);
}

public sealed class AuditService : IAuditService
{
    // Web defaults so audit snapshots are serialised the same way API responses are.
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
    };

    public AuditLog Log(
        AppDbContext db,
        string type,
        Guid actorUserId,
        Guid? eventId = null,
        Guid? eventGameId = null,
        Guid? objectiveId = null,
        Guid? subjectUserId = null,
        object? before = null,
        object? after = null,
        string? reason = null)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentException.ThrowIfNullOrEmpty(type);

        var entry = new AuditLog
        {
            Type = type,
            ActorUserId = actorUserId,
            EventId = eventId,
            EventGameId = eventGameId,
            ObjectiveId = objectiveId,
            SubjectUserId = subjectUserId,
            BeforeJson = before is null ? null : JsonSerializer.Serialize(before, JsonOptions),
            AfterJson = after is null ? null : JsonSerializer.Serialize(after, JsonOptions),
            Reason = reason,
        };
        db.AuditLogs.Add(entry);
        return entry;
    }
}
