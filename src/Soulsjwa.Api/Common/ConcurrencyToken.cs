using Microsoft.EntityFrameworkCore;

namespace Soulsjwa.Api.Common;

/// <summary>
/// Surfaces Postgres's <c>xmin</c> shadow concurrency token (see
/// <c>AppDbContext.OnModelCreating</c>) as an HTTP <c>ETag</c>/<c>If-Match</c>
/// pair: <c>GET</c> emits the row's token, and a client sending it back on its
/// next write gets a 409 instead of silently overwriting a concurrent change.
/// A missing or unparsable <c>If-Match</c> is accepted — sending one is opt-in,
/// so older clients keep working.
/// </summary>
public static class ConcurrencyToken
{
    public static string ToETag(uint xmin) => $"\"{xmin}\"";

    public static bool TryParse(string? ifMatch, out uint xmin)
    {
        xmin = 0;
        if (string.IsNullOrWhiteSpace(ifMatch)) return false;
        return uint.TryParse(ifMatch.Trim().Trim('"'), out xmin);
    }

    public static uint GetXmin<TEntity>(DbContext db, TEntity entity) where TEntity : class =>
        db.Entry(entity).Property<uint>("xmin").CurrentValue;

    /// <summary>
    /// Overrides the tracked entity's original <c>xmin</c> with the client-supplied
    /// one, so <c>SaveChangesAsync</c>'s <c>WHERE</c> checks what the client saw
    /// rather than what this request just reloaded — otherwise a stale write inside
    /// a single request could never be detected.
    /// </summary>
    public static bool ApplyIfMatch<TEntity>(DbContext db, TEntity entity, string? ifMatch) where TEntity : class
    {
        if (!TryParse(ifMatch, out var xmin)) return false;
        db.Entry(entity).Property<uint>("xmin").OriginalValue = xmin;
        return true;
    }

    /// <summary>
    /// <see cref="ApplyIfMatch{TEntity}"/> for endpoints that carry the token in a
    /// body <c>Version</c> field instead of a header — e.g. a list-shaped resource
    /// with no single-item GET to hang an ETag off.
    /// </summary>
    public static bool ApplyExpectedVersion<TEntity>(DbContext db, TEntity entity, uint? expectedVersion) where TEntity : class
    {
        if (expectedVersion is not { } xmin) return false;
        db.Entry(entity).Property<uint>("xmin").OriginalValue = xmin;
        return true;
    }
}
