using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Soulsjwa.Api.Diagnostics;

namespace Soulsjwa.Api.Infrastructure.Data;

/// <summary>
/// Deletes expired <see cref="Auth.Entities.RefreshToken"/>s past a grace window
/// and, only when explicitly enabled, <see cref="Audits.Entities.AuditLog"/>s past
/// an operator-configured age. A Postgres advisory lock — the "try" variant, so a
/// second instance skips rather than blocks — keeps a multi-instance deployment
/// from running two passes at once.
/// </summary>
public sealed class RetentionService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<RetentionService> logger) : BackgroundService
{
    private const string AdvisoryLockKey = "retention-service";
    private const int DefaultIntervalHours = 24;
    private const int DefaultRefreshTokenGraceDays = 7;
    private const int DefaultBatchSize = 5000;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalHours = configuration.GetValue("Retention:IntervalHours", DefaultIntervalHours);
        using var timer = new PeriodicTimer(TimeSpan.FromHours(intervalHours));

        do
        {
            try
            {
                await RunPassAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A failed cleanup pass must never take the host down — log and
                // retry on the next tick.
                logger.RetentionPassFailed(ex);
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>Public so a pass is directly testable without waiting on <see cref="ExecuteAsync"/>'s timer.</summary>
    public async Task RunPassAsync(CancellationToken ct = default)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // pg_try_advisory_lock is session-scoped: acquire, every batch delete and
        // the release must run on the same physical connection, or EF's per-command
        // pooling could acquire and release on two different pooled connections and
        // leak the lock. Opening the connection explicitly pins it for the whole
        // pass, while still letting each batch commit independently (which one
        // pg_try_advisory_xact_lock transaction around the whole pass would not).
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            // SqlQuery<T> for a scalar type requires the result column named
            // "Value" — confirmed against a real Postgres instance.
            var acquired = await db.Database
                .SqlQuery<bool>($"SELECT pg_try_advisory_lock(hashtextextended({AdvisoryLockKey}, 0)) AS \"Value\"")
                .SingleAsync(ct);
            if (!acquired)
            {
                logger.RetentionPassSkipped();
                return;
            }

            try
            {
                await RunDeletesAsync(db, ct);
            }
            finally
            {
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT pg_advisory_unlock(hashtextextended({AdvisoryLockKey}, 0))", ct);
            }
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private async Task RunDeletesAsync(AppDbContext db, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        var batchSize = configuration.GetValue("Retention:BatchSize", DefaultBatchSize);
        var refreshTokenGraceDays = configuration.GetValue("Retention:RefreshTokenGraceDays", DefaultRefreshTokenGraceDays);
        var auditRetentionDays = configuration.GetValue<int?>("Retention:AuditRetentionDays", null);

        var refreshTokensDeleted = await DeleteInBatchesAsync(
            () =>
            {
                var cutoff = DateTime.UtcNow.AddDays(-refreshTokenGraceDays);
                // Grace window past expiry, not IsRevoked alone: a revoked-but-
                // unexpired row is what JwtTokenService.ValidateRefreshTokenAsync
                // needs to detect reuse (theft), so it must outlive its revocation.
                return db.RefreshTokens
                    .Where(t => t.ExpiresAt < cutoff)
                    .OrderBy(t => t.Id)
                    .Take(batchSize);
            },
            ct);

        var auditLogsDeleted = 0;
        if (auditRetentionDays.HasValue)
        {
            auditLogsDeleted = await DeleteInBatchesAsync(
                () =>
                {
                    var cutoff = DateTime.UtcNow.AddDays(-auditRetentionDays.Value);
                    return db.AuditLogs
                        .Where(a => a.CreatedAt < cutoff)
                        .OrderBy(a => a.Id)
                        .Take(batchSize);
                },
                ct);
        }

        logger.RetentionPassCompleted(stopwatch.ElapsedMilliseconds, refreshTokensDeleted, auditLogsDeleted);
    }

    /// <summary>Batched so a first run against a large table isn't one long-running delete.</summary>
    private static async Task<int> DeleteInBatchesAsync<T>(Func<IQueryable<T>> queryFactory, CancellationToken ct)
    {
        var total = 0;
        while (true)
        {
            var affected = await queryFactory().ExecuteDeleteAsync(ct);
            total += affected;
            if (affected == 0)
                break;
        }
        return total;
    }
}
