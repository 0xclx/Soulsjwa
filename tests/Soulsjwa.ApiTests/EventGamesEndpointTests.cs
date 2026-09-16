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
/// The event-games endpoints' HTTP contract: the routes and verbs, the owner
/// gate as a client meets it, the created-id and updated-list payloads, and
/// that a reorder written over the wire is what a later GET returns. The rules
/// (when the game list is frozen, what a reorder accepts, how a display name
/// resolves, the concurrent-enable race) live in
/// <c>Soulsjwa.IntegrationTests.EventGameTests</c>.
/// </summary>
public class EventGamesEndpointTests : ApiTestBase
{
    private const int SeededGameId = 1; // "Dark Souls" — present via DbContext seed

    [Fact]
    public async Task AddGame_AsOwner_Returns201()
    {
        var (owner, key) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services);
        var ev = await SeedEventAsync(owner.Id);
        var client = TestAuth.CreateAuthenticatedClient(Factory, key);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/events/{ev.Id}/games", new { gameId = SeededGameId });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task AddGame_NotOwner_Returns403()
    {
        var (owner, _) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "owner");
        var (_, otherKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "other", role: Soulsjwa.Api.Features.Auth.Entities.UserRole.User);
        var ev = await SeedEventAsync(owner.Id);

        var client = TestAuth.CreateAuthenticatedClient(Factory, otherKey);
        var response = await client.PostAsJsonAsync(
            $"/api/v1/events/{ev.Id}/games", new { gameId = SeededGameId });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RemoveGame_AsOwner_Returns204()
    {
        var (owner, key) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services);
        var ev = await SeedEventAsync(owner.Id);
        var client = TestAuth.CreateAuthenticatedClient(Factory, key);

        var addResponse = await client.PostAsJsonAsync(
            $"/api/v1/events/{ev.Id}/games", new { gameId = SeededGameId });
        var created = await addResponse.Content.ReadFromJsonAsync<CreatedResponse>();

        var del = await client.DeleteAsync($"/api/v1/events/{ev.Id}/games/{created!.Id}");

        del.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task AddCustomGame_AsOwner_Returns201()
    {
        var (owner, key) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services);
        var ev = await SeedEventAsync(owner.Id);
        var client = TestAuth.CreateAuthenticatedClient(Factory, key);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/events/{ev.Id}/games/custom",
            new { name = "IRL Quiz", description = "A fun trivia game" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task ReorderGames_ValidFullSet_Succeeds_AndGetReflectsOrder()
    {
        var (owner, key) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services);
        var ev = await SeedEventAsync(owner.Id);
        var client = TestAuth.CreateAuthenticatedClient(Factory, key);

        var first = await CreateCustomGameAsync(client, ev.Id, "First");
        var second = await CreateCustomGameAsync(client, ev.Id, "Second");
        var third = await CreateCustomGameAsync(client, ev.Id, "Third");

        var response = await client.PutAsJsonAsync(
            $"/api/v1/events/{ev.Id}/games/reorder",
            new { eventGameIds = new[] { third, first, second } });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var get = await client.GetFromJsonAsync<EventDto>($"/api/v1/events/{ev.Id}");
        get!.Games.Select(g => g.EventGameId).Should().Equal(third, first, second);
    }

    [Fact]
    public async Task ReorderObjectives_ValidFullSet_Succeeds_AndGetReflectsOrder()
    {
        var (owner, key) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services);
        var ev = await SeedEventAsync(owner.Id);
        var client = TestAuth.CreateAuthenticatedClient(Factory, key);
        var eventGameId = await CreateCustomGameAsync(client, ev.Id, "Game");

        var o1 = await CreateObjectiveAsync(client, ev.Id, eventGameId, "Obj1");
        var o2 = await CreateObjectiveAsync(client, ev.Id, eventGameId, "Obj2");
        var o3 = await CreateObjectiveAsync(client, ev.Id, eventGameId, "Obj3");

        var response = await client.PutAsJsonAsync(
            $"/api/v1/events/{ev.Id}/games/{eventGameId}/objectives/reorder",
            new { objectiveIds = new[] { o3, o1, o2 } });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var get = await client.GetFromJsonAsync<EventDto>($"/api/v1/events/{ev.Id}");
        var game = get!.Games.Single(g => g.EventGameId == eventGameId);
        game.Objectives.Select(o => o.Id).Should().Equal(o3, o1, o2);
    }

    [Fact]
    public async Task PatchEventGame_RenameCustomGame_Succeeds()
    {
        var (owner, key) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services);
        var ev = await SeedEventAsync(owner.Id);
        var client = TestAuth.CreateAuthenticatedClient(Factory, key);
        var eventGameId = await CreateCustomGameAsync(client, ev.Id, "Original Name");

        var response = await client.PatchAsJsonAsync(
            $"/api/v1/events/{ev.Id}/games/{eventGameId}", new { name = "New Name" });
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var get = await client.GetFromJsonAsync<EventDto>($"/api/v1/events/{ev.Id}");
        get!.Games.Single(g => g.EventGameId == eventGameId).GameName.Should().Be("New Name");
    }

    [Fact]
    public async Task EnableEventGame_EventStarted_Returns200WithUpdatedList()
    {
        var (owner, key) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services);
        var ev = await SeedEventAsync(owner.Id);
        var client = TestAuth.CreateAuthenticatedClient(Factory, key);
        var eventGameId = await CreateCustomGameAsync(client, ev.Id, "Game");
        await client.PostAsync($"/api/v1/events/{ev.Id}/start", null);

        var response = await client.PostAsync($"/api/v1/events/{ev.Id}/games/{eventGameId}/enable", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var games = await response.Content.ReadFromJsonAsync<List<EventGameDto>>();
        games!.Single(g => g.EventGameId == eventGameId).IsEnabled.Should().BeTrue();
    }

    private static async Task<Guid> CreateCustomGameAsync(HttpClient client, Guid eventId, string name)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/v1/events/{eventId}/games/custom", new { name });
        return (await response.Content.ReadFromJsonAsync<CreatedResponse>())!.Id;
    }

    private static async Task<Guid> CreateObjectiveAsync(HttpClient client, Guid eventId, Guid eventGameId, string name)
    {
        var response = await client.PostAsJsonAsync(
            $"/api/v1/events/{eventId}/games/{eventGameId}/objectives",
            new { name, score = 10 });
        return (await response.Content.ReadFromJsonAsync<ObjectiveCreatedResponse>())!.Id;
    }

    private sealed record CreatedResponse(Guid Id);
    private sealed record ObjectiveCreatedResponse(Guid Id);
    private sealed record EventDto(Guid Id, List<EventGameDto> Games);
    private sealed record EventGameDto(Guid EventGameId, string GameName, string? KnownGameName, bool IsEnabled, List<ObjectiveDto> Objectives);
    private sealed record ObjectiveDto(Guid Id, string Name);

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
