using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Soulsjwa.Api.Features.Events.Entities;
using Soulsjwa.Api.Infrastructure.Data;
using Xunit;

namespace Soulsjwa.ApiTests;

/// <summary>
/// The competitor endpoints' HTTP contract: the routes and verbs, the owner
/// gate as a client meets it, and that the event payload reflects a
/// competitor's live flag. Who may be added and how, self-join's
/// preconditions, the placeholder user an unknown handle creates, and the
/// delegation cascade live in
/// <c>Soulsjwa.IntegrationTests.CompetitorAndDelegationTests</c>.
/// </summary>
public class EventCompetitorsEndpointTests : ApiTestBase
{
    [Fact]
    public async Task AddCompetitor_NotOwner_Returns403()
    {
        var (owner, _) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "owner");
        var (other, otherKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "other", role: Soulsjwa.Api.Features.Auth.Entities.UserRole.User);
        var ev = await SeedEventAsync(owner.Id);

        var client = TestAuth.CreateAuthenticatedClient(Factory, otherKey);
        var response = await client.PostAsJsonAsync(
            $"/api/v1/events/{ev.Id}/competitors", new { userId = other.Id });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AddCompetitor_AsOwner_Returns201()
    {
        var (owner, key) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "owner");
        var (competitor, _) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "comp");
        var ev = await SeedEventAsync(owner.Id);

        var client = TestAuth.CreateAuthenticatedClient(Factory, key);
        var response = await client.PostAsJsonAsync(
            $"/api/v1/events/{ev.Id}/competitors", new { userId = competitor.Id });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task RemoveCompetitor_AsOwner_Returns204()
    {
        var (owner, key) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "owner");
        var (competitor, _) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "comp");
        var ev = await SeedEventAsync(owner.Id);

        var client = TestAuth.CreateAuthenticatedClient(Factory, key);
        await client.PostAsJsonAsync($"/api/v1/events/{ev.Id}/competitors", new { userId = competitor.Id });
        var del = await client.DeleteAsync($"/api/v1/events/{ev.Id}/competitors/{competitor.Id}");

        del.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GetEvent_ReflectsCompetitorIsLive()
    {
        var (owner, key) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "owner");
        var (competitor, compKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "comp");
        var ev = await SeedEventAsync(owner.Id);

        var ownerClient = TestAuth.CreateAuthenticatedClient(Factory, key);
        await ownerClient.PostAsJsonAsync($"/api/v1/events/{ev.Id}/competitors", new { userId = competitor.Id });

        var compClient = TestAuth.CreateAuthenticatedClient(Factory, compKey);
        await compClient.PostAsJsonAsync($"/api/v1/events/{ev.Id}/live", new { isLive = true });

        var response = await ownerClient.GetAsync($"/api/v1/events/{ev.Id}");
        var dto = await response.Content.ReadFromJsonAsync<EventWithCompetitorsDto>();

        dto!.Competitors.Should().ContainSingle(c => c.UserId == competitor.Id && c.IsLive);
    }

    private record EventWithCompetitorsDto(List<CompetitorDto> Competitors);
    private record CompetitorDto(Guid UserId, bool IsLive);

    [Fact]
    public async Task SelfJoin_AsAuthenticatedUser_Returns201()
    {
        var (owner, _) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "owner");
        var (competitor, key) = await TestAuth.CreateUserWithApiKeyAsync(
            Factory.Services, "joiner", role: Soulsjwa.Api.Features.Auth.Entities.UserRole.User);
        var ev = await SeedEventAsync(owner.Id);
        var client = TestAuth.CreateAuthenticatedClient(Factory, key);

        var response = await client.PostAsync($"/api/v1/events/{ev.Id}/competitors/self", null);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var isCompetitor = await db.EventCompetitors
            .AnyAsync(c => c.EventId == ev.Id && c.UserId == competitor.Id);
        isCompetitor.Should().BeTrue();
    }

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
