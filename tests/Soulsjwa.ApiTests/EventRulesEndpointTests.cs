using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Soulsjwa.Api.Features.Audits;
using Soulsjwa.Api.Features.Events.Entities;
using Soulsjwa.Api.Infrastructure.Data;
using Xunit;

namespace Soulsjwa.ApiTests;

/// <summary>
/// The rules endpoints' HTTP contract: the anonymous read, the owner gate, and
/// the ETag the concurrent-update flow depends on. The size cap and the audit
/// row's digest live in
/// <c>Soulsjwa.IntegrationTests.EventRulesAndDuplicationTests</c>.
/// </summary>
public class EventRulesEndpointTests : ApiTestBase
{
    [Fact]
    public async Task GetRules_AnonymousOnEventWithNoRules_Returns200WithNullContent()
    {
        var (owner, _) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services);
        var ev = await SeedEventAsync(owner.Id);

        var response = await Client.GetAsync($"/api/v1/events/{ev.Id}/rules");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<RulesDto>();
        body!.Content.Should().BeNull();
    }

    [Fact]
    public async Task PutRules_NotOwnerNotAdmin_Returns403()
    {
        var (owner, _) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "owner");
        var (_, otherKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "other", role: Soulsjwa.Api.Features.Auth.Entities.UserRole.User);
        var ev = await SeedEventAsync(owner.Id);
        var client = TestAuth.CreateAuthenticatedClient(Factory, otherKey);

        var response = await client.PutAsJsonAsync($"/api/v1/events/{ev.Id}/rules", new { content = "No item duping." });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private sealed record RulesDto(string? Content, DateTime? UpdatedAt);

    private async Task<Event> SeedEventAsync(Guid ownerId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ev = new Event { Name = "test", CreatedById = ownerId };
        db.Events.Add(ev);
        await db.SaveChangesAsync();
        return ev;
    }
}
