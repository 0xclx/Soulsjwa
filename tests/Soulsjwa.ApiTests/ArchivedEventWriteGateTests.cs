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
/// <c>db.Events</c>'s global query filter on <c>IsArchived</c> means an archived
/// event's write endpoints must resolve to 404 (the row looks absent), never
/// silently skip their "is the event running?" gate. Covers every site that used
/// the buggy `ev is not null && !ev.IsStarted` pattern.
/// </summary>
public class ArchivedEventWriteGateTests : ApiTestBase
{
    private const int SeededGameId = 1;

    [Fact]
    public async Task CompleteObjective_OnArchivedEvent_Returns404AndWritesNothing()
    {
        var (owner, key, ev, eventGameId, objectiveId) = await SeedArchivedEventWithObjectiveAsync();
        var client = TestAuth.CreateAuthenticatedClient(Factory, key);

        var response = await client.PostAsync(
            $"/api/v1/events/{ev.Id}/games/{eventGameId}/objectives/{objectiveId}/complete", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await AssertNoOutcomeRowsAsync(objectiveId, owner.Id);
    }

    [Fact]
    public async Task UncompleteObjective_OnArchivedEvent_Returns404()
    {
        var (owner, key, ev, eventGameId, objectiveId) = await SeedArchivedEventWithObjectiveAsync();
        var client = TestAuth.CreateAuthenticatedClient(Factory, key);

        var response = await client.DeleteAsync(
            $"/api/v1/events/{ev.Id}/games/{eventGameId}/objectives/{objectiveId}/complete");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task FailObjective_OnArchivedEvent_Returns404AndWritesNothing()
    {
        var (owner, key, ev, eventGameId, objectiveId) = await SeedArchivedEventWithObjectiveAsync();
        var client = TestAuth.CreateAuthenticatedClient(Factory, key);

        var response = await client.PostAsync(
            $"/api/v1/events/{ev.Id}/games/{eventGameId}/objectives/{objectiveId}/fail", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await AssertNoOutcomeRowsAsync(objectiveId, owner.Id);
    }

    [Fact]
    public async Task ResetFailedObjective_OnArchivedEvent_Returns404()
    {
        var (owner, key, ev, eventGameId, objectiveId) = await SeedArchivedEventWithObjectiveAsync();
        var client = TestAuth.CreateAuthenticatedClient(Factory, key);

        var response = await client.DeleteAsync(
            $"/api/v1/events/{ev.Id}/games/{eventGameId}/objectives/{objectiveId}/fail");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ConnectorSubmit_OnArchivedEvent_Returns404AndWritesNothing()
    {
        var (owner, key, ev, eventGameId, objectiveId) = await SeedArchivedEventWithObjectiveAsync();
        var client = TestAuth.CreateAuthenticatedClient(Factory, key);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/connector/events/{ev.Id}/games/{eventGameId}/submit",
            new { data = "{}" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await AssertNoOutcomeRowsAsync(objectiveId, owner.Id);
    }

    private async Task<(Soulsjwa.Api.Features.Auth.Entities.User Owner, string Key, Event Event, Guid EventGameId, Guid ObjectiveId)>
        SeedArchivedEventWithObjectiveAsync()
    {
        var (owner, key) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "owner");

        // Seeded unstarted so the game can be added through the API (adding
        // games to a running event is a 409). The event is then started before
        // the game is enabled, since EnableEventGame requires the opposite: the
        // event must already be running.
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ev = new Event { Name = "archived-write-gate", CreatedById = owner.Id };
        db.Events.Add(ev);
        await db.SaveChangesAsync();

        var ownerClient = TestAuth.CreateAuthenticatedClient(Factory, key);
        var addResp = await ownerClient.PostAsJsonAsync($"/api/v1/events/{ev.Id}/games", new { gameId = SeededGameId });
        var eventGameId = (await addResp.Content.ReadFromJsonAsync<CreatedGameResponse>())!.Id;

        ev.IsStarted = true;
        await db.SaveChangesAsync();
        await ownerClient.PostAsync($"/api/v1/events/{ev.Id}/games/{eventGameId}/enable", null);

        var create = await ownerClient.PostAsJsonAsync(
            $"/api/v1/events/{ev.Id}/games/{eventGameId}/objectives/",
            new { name = "obj", score = 10 });
        var created = await create.Content.ReadFromJsonAsync<ObjDto>();

        await ownerClient.PostAsJsonAsync($"/api/v1/events/{ev.Id}/competitors", new { userId = owner.Id });

        // Archive directly in the DB — equivalent to POST .../archive, and avoids
        // an extra authenticated round trip per test.
        var tracked = await db.Events.FirstAsync(e => e.Id == ev.Id);
        tracked.IsArchived = true;
        await db.SaveChangesAsync();

        return (owner, key, ev, eventGameId, created!.Id);
    }

    private async Task AssertNoOutcomeRowsAsync(Guid objectiveId, Guid userId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.CompletedObjectives.AnyAsync(co => co.ObjectiveId == objectiveId && co.UserId == userId))
            .Should().BeFalse();
        (await db.FailedObjectives.AnyAsync(f => f.ObjectiveId == objectiveId && f.UserId == userId))
            .Should().BeFalse();
    }

    private record ObjDto(Guid Id, string Name, int Score);
    private record CreatedGameResponse(Guid Id);
}
