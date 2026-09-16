using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Soulsjwa.Api.Features.Audits.Entities;
using Soulsjwa.Api.Features.Auth.Entities;
using Soulsjwa.Api.Infrastructure.Data;
using Xunit;

namespace Soulsjwa.IntegrationTests;

/// <summary>
/// Retention deletes refresh tokens past a grace window beyond their expiry,
/// but never a revoked-and-not-yet-expired token (reuse detection needs it),
/// and never audit rows while retention is at its default "keep forever".
/// A background service, so the test resolves it from a container of its own
/// and calls its pass directly. The predicate under test only exists as SQL:
/// <c>Where(...).ExecuteDeleteAsync()</c> becomes a single <c>DELETE ... WHERE</c>.
/// </summary>
public class RetentionServiceTests : IntegrationTestBase
{
    [Fact]
    public async Task RunPassAsync_DeletesOnlyExpiredPastGraceWindow_LeavesAuditLogsAlone()
    {
        var seed = CreateDbContext();

        var user = new User { TwitchId = $"tw_{Guid.NewGuid():N}", TwitchLogin = "retention-user", DisplayName = "retention-user" };
        seed.Users.Add(user);
        await seed.SaveChangesAsync();

        var now = DateTime.UtcNow;
        var expiredPastGrace = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = "hash-1",
            TokenPrefix = "pfx-0001",
            ExpiresAt = now.AddDays(-10), // default grace window is 7 days
        };
        var recentlyExpired = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = "hash-2",
            TokenPrefix = "pfx-0002",
            ExpiresAt = now.AddDays(-1),
        };
        var revokedUnexpired = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = "hash-3",
            TokenPrefix = "pfx-0003",
            ExpiresAt = now.AddDays(5),
            IsRevoked = true,
            RevokedAt = now,
        };
        var active = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = "hash-4",
            TokenPrefix = "pfx-0004",
            ExpiresAt = now.AddDays(5),
        };
        seed.RefreshTokens.AddRange(expiredPastGrace, recentlyExpired, revokedUnexpired, active);

        seed.AuditLogs.Add(new AuditLog
        {
            Type = "test.retention",
            ActorUserId = user.Id,
            CreatedAt = now.AddYears(-2),
        });
        await seed.SaveChangesAsync();

        var provider = CreateServiceProvider();
        var service = new RetentionService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<IConfiguration>(),
            NullLogger<RetentionService>.Instance);

        await service.RunPassAsync();

        // A second context: the deletes above ran as raw SQL through the
        // service's own scope, which the seeding context's change tracker
        // knows nothing about.
        var verify = CreateDbContext();

        (await verify.RefreshTokens.AnyAsync(t => t.Id == expiredPastGrace.Id)).Should().BeFalse(
            "a token expired longer ago than the grace window must be deleted");
        (await verify.RefreshTokens.AnyAsync(t => t.Id == recentlyExpired.Id)).Should().BeTrue(
            "a token still inside the grace window must survive");
        (await verify.RefreshTokens.AnyAsync(t => t.Id == revokedUnexpired.Id)).Should().BeTrue(
            "a revoked-but-unexpired token must survive so reuse detection can still fire for it");
        (await verify.RefreshTokens.AnyAsync(t => t.Id == active.Id)).Should().BeTrue();

        (await verify.AuditLogs.CountAsync(a => a.Type == "test.retention")).Should().Be(1,
            "audit retention defaults to disabled — nothing should be deleted");
    }
}
