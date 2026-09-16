using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Soulsjwa.Api.Features.Audits;
using Soulsjwa.Api.Features.Audits.Entities;
using Soulsjwa.Api.Infrastructure.Data;
using Xunit;

namespace Soulsjwa.ApiTests;

/// <summary>
/// Keyset pagination over the wire: a cursor page's JSON carries a nextCursor
/// and no totalCount, which is the shape the admin UI switches modes on. The
/// traversal itself, the offset-page cap and the absent COUNT query live in
/// <c>Soulsjwa.IntegrationTests.MyEventsAndAuditTests</c>.
/// </summary>
public class AuditsEndpointTests : ApiTestBase
{
    private async Task<Guid> SeedAuditRowsAsync(int count)
    {
        var (actor, _) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "actor");
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var baseTime = DateTime.UtcNow;
        for (var i = 0; i < count; i++)
        {
            db.AuditLogs.Add(new AuditLog
            {
                Type = AuditEventTypes.EventCreated,
                ActorUserId = actor.Id,
                CreatedAt = baseTime.AddSeconds(-i),
            });
        }
        await db.SaveChangesAsync();
        return actor.Id;
    }

    [Fact]
    public async Task ListAdmin_CursorMode_OmitsTotalCountByDefault()
    {
        // pageSize must be one of the allowed values (10/20/30/40/50); 15
        // rows means the first page-of-10 is full (so it carries a
        // nextCursor) and the second page has only 5 rows left.
        await SeedAuditRowsAsync(15);
        var (_, key) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "admin");
        var client = TestAuth.CreateAuthenticatedClient(Factory, key);

        var first = await client.GetAsync("/api/v1/admin/audits?pageSize=10");
        var firstBody = await first.Content.ReadFromJsonAsync<JsonElement>();
        var cursor = firstBody.GetProperty("nextCursor").GetString();
        cursor.Should().NotBeNullOrEmpty();

        var second = await client.GetAsync($"/api/v1/admin/audits?pageSize=10&cursor={Uri.EscapeDataString(cursor!)}");
        var secondBody = await second.Content.ReadFromJsonAsync<JsonElement>();

        (secondBody.TryGetProperty("totalCount", out var totalCount) && totalCount.ValueKind != JsonValueKind.Null)
            .Should().BeFalse("a cursor page should not pay for a COUNT unless includeTotal=true is explicit");
    }

}
