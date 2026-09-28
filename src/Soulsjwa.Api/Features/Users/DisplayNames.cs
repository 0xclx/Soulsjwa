using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using Soulsjwa.Api.Common;
using Soulsjwa.Api.Features.Audits;
using Soulsjwa.Api.Features.Audits.Services;
using Soulsjwa.Api.Features.Auth.Entities;
using Soulsjwa.Api.Infrastructure.Data;

namespace Soulsjwa.Api.Features.Users;

/// <summary>
/// Setting or clearing a user's display-name override, shared by the self
/// and admin endpoints so both validate, audit and evict identically.
/// </summary>
internal static class DisplayNames
{
    /// <summary>The request field validation errors are keyed on.</summary>
    public const string FieldName = "displayName";

    /// <summary>
    /// Free text, trimmed, 1–<see cref="User.DisplayNameOverrideMaxLength"/>
    /// characters, no control characters. Null, empty or whitespace means
    /// "clear the override" and normalises to null.
    /// </summary>
    public static bool TryNormalize(string? raw, out string? normalized, out string? error)
    {
        normalized = null;
        error = null;
        var trimmed = raw?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return true;

        if (trimmed.Length > User.DisplayNameOverrideMaxLength)
        {
            error = $"Display name must be {User.DisplayNameOverrideMaxLength} characters or fewer.";
            return false;
        }
        if (trimmed.Any(char.IsControl))
        {
            error = "Display name must not contain control characters.";
            return false;
        }

        normalized = trimmed;
        return true;
    }

    /// <summary>
    /// Stores <paramref name="displayNameOverride"/> (already normalised). When
    /// the name every surface shows changes, writes a
    /// <c>user.display_name_changed</c> audit and, after saving, evicts every
    /// cached response that embeds it: the scoreboard of each event the user
    /// competes in (which also covers /scores, the overlay and the Twitch
    /// extension) and the global calendar. Returns whether it changed.
    /// </summary>
    public static async Task<bool> ApplyAsync(
        AppDbContext db,
        IAuditService audit,
        IOutputCacheStore cache,
        User user,
        string? displayNameOverride,
        Guid actorUserId,
        CancellationToken ct)
    {
        if (user.DisplayNameOverride == displayNameOverride) return false;

        var before = new { user.DisplayName, user.DisplayNameOverride };
        user.SetDisplayNameOverride(displayNameOverride);
        user.UpdatedAt = DateTime.UtcNow;
        var effectiveChanged = user.DisplayName != before.DisplayName;
        if (effectiveChanged)
        {
            audit.Log(db, AuditEventTypes.UserDisplayNameChanged, actorUserId,
                subjectUserId: user.Id,
                before: before,
                after: new { user.DisplayName, user.DisplayNameOverride });
        }
        await db.SaveChangesAsync(ct);

        if (!effectiveChanged) return false;

        var eventIds = await db.EventCompetitors
            .IgnoreQueryFilters()
            .Where(c => c.UserId == user.Id)
            .Select(c => c.EventId)
            .Distinct()
            .ToListAsync(ct);
        foreach (var eventId in eventIds)
            await cache.EvictByTagAsync(CacheTags.Scoreboard(eventId), ct);
        await cache.EvictByTagAsync(CacheTags.Calendar, ct);
        return true;
    }
}
