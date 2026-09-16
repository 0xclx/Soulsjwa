using Microsoft.EntityFrameworkCore;
using Soulsjwa.Api.Infrastructure.Data;

namespace Soulsjwa.Api.Features.Events;

public static class ObjectiveOutcomeLock
{
    public static Task AcquireAsync(
        AppDbContext db,
        Guid eventGameId,
        CancellationToken ct)
    {
        var key = $"objective-outcomes:{eventGameId:N}";
        return db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))",
            ct);
    }
}
