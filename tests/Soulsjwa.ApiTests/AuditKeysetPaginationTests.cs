using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Soulsjwa.Api.Features.Audits;
using Soulsjwa.Api.Features.Audits.Entities;
using Soulsjwa.Api.Features.Auth.Entities;
using Soulsjwa.Api.Infrastructure.Auth;
using Soulsjwa.Api.Infrastructure.Data;
using Xunit;

namespace Soulsjwa.ApiTests;

/// <summary>
/// Keyset pagination over a large audit fixture with many colliding CreatedAt
/// values must traverse every row exactly once — no duplicates or gaps at page
/// boundaries. Driven over HTTP because the traversal feeds each response's own
/// <c>nextCursor</c> back in as a query string, so the contract under test is
/// the wire one.
/// </summary>
public class AuditKeysetPaginationTests : ApiTestBase
{
    /// <summary>
    /// Kept under 100 total pages at pageSize=50
    /// (BucketCount * RowsPerBucket / 50 &lt; 100) so the traversal itself
    /// doesn't trip the global fallback limiter's default 100/min. This test is
    /// about pagination correctness, not throughput.
    /// </summary>
    private const int BucketCount = 9;
    private const int RowsPerBucket = 500;
    private const int RowCount = BucketCount * RowsPerBucket;

    private async Task<(User Admin, HttpClient Client)> CreateAdminClientAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var admin = new User
        {
            TwitchId = Guid.NewGuid().ToString(),
            TwitchLogin = "admin-" + Guid.NewGuid().ToString("N")[..8],
            DisplayName = "Admin",
            Role = UserRole.Admin,
            IsAllowlisted = true,
        };
        db.Users.Add(admin);
        var (rawKey, prefix) = ApiKeyAuthHandler.GenerateApiKey();
        db.ApiKeys.Add(new ApiKey
        {
            UserId = admin.Id,
            Name = "test-key",
            KeyHash = ApiKeyAuthHandler.HashApiKey(rawKey),
            KeyPrefix = prefix,
        });
        await db.SaveChangesAsync();

        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", rawKey);
        return (admin, client);
    }

    [Fact]
    public async Task CursorPaging_ThroughManyRowsWithCollidingTimestamps_VisitsEveryRowExactlyOnce()
    {
        var (admin, client) = await CreateAdminClientAsync();

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            // BucketCount distinct timestamps, RowsPerBucket rows apiece, so
            // every page boundary below lands inside a run of rows sharing the
            // same CreatedAt — exercising the Id tie-break on every page.
            var baseTime = DateTime.UtcNow;
            var buffer = new List<AuditLog>(RowsPerBucket);
            for (var bucket = 0; bucket < BucketCount; bucket++)
            {
                var createdAt = baseTime.AddMinutes(-bucket);
                for (var i = 0; i < RowsPerBucket; i++)
                {
                    buffer.Add(new AuditLog
                    {
                        Type = AuditEventTypes.EventCreated,
                        ActorUserId = admin.Id,
                        CreatedAt = createdAt,
                    });
                }
                db.AuditLogs.AddRange(buffer);
                await db.SaveChangesAsync();
                buffer.Clear();
            }
        }

        var seen = new HashSet<Guid>();
        string? cursor = null;
        do
        {
            var url = "/api/v1/admin/audits?pageSize=50" + (cursor is null ? "" : $"&cursor={Uri.EscapeDataString(cursor)}");
            var response = await client.GetAsync(url);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();

            foreach (var item in body.GetProperty("items").EnumerateArray())
                seen.Add(item.GetProperty("id").GetGuid()).Should().BeTrue("cursor traversal must never return the same row twice");

            // A full page always carries a nextCursor, even when it is the last
            // one — the endpoint doesn't peek ahead. So a fixture size that's an
            // exact multiple of pageSize costs one extra, empty final request.
            cursor = body.TryGetProperty("nextCursor", out var nc) && nc.ValueKind == JsonValueKind.String
                ? nc.GetString()
                : null;
        } while (cursor is not null);

        seen.Should().HaveCount(RowCount, "every row must be visited exactly once, with no gaps at page boundaries");
    }
}
