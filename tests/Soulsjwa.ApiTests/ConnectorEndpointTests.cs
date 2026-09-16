using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Soulsjwa.Api.Features.Connector;
using Soulsjwa.Api.Features.Events.Entities;
using Soulsjwa.Api.Features.Games.Definitions;
using Soulsjwa.Api.Features.Games.Entities;
using Soulsjwa.Api.Features.Auth.Entities;
using Soulsjwa.Api.Infrastructure.Data;
using Soulsjwa.Shared;
using Xunit;

namespace Soulsjwa.ApiTests;

/// <summary>
/// The connector endpoints' HTTP contract: which routes are anonymous, which
/// need a key, the payload field names the desktop app reads, and one whole
/// submission driven the way the connector drives it. What a submission
/// actually does — what it completes, fails, cascades and refuses — lives in
/// <c>Soulsjwa.IntegrationTests.ConnectorSubmissionTests</c>.
/// </summary>
public class ConnectorEndpointTests : ApiTestBase
{
    private const int ConnectorSupportedGameId = 1;     // Dark Souls (seeded as supported)
    private const int NonSupportedGameId = 90001;       // Fixture-only game seeded below as not connector-supported
    private const int ConnectorFlagId = 16;              // Asylum Demon
    private static readonly string ConnectorFlagDataPointId =
        GameDataDefinitions.SoulMemoryFlagId(ConnectorSupportedGameId, ConnectorFlagId);

    [Fact]
    public async Task GetVersion_IsAnonymousAndReturnsString()
    {
        var response = await Client.GetAsync("/api/v1/connector/version");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<VersionDto>();
        body!.RequiredVersion.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GetSupportedGames_IsAnonymousAndIncludesAtLeastOne()
    {
        var response = await Client.GetAsync("/api/v1/connector/supported-games");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var games = await response.Content.ReadFromJsonAsync<List<SupportedGameDto>>();
        games.Should().NotBeNull();
        games!.Should().NotBeEmpty();
        games.Should().Contain(g => g.Id == ConnectorSupportedGameId);
    }

    [Fact]
    public async Task GetGameData_RequiresAuth()
    {
        var response = await Client.GetAsync($"/api/v1/connector/games/{ConnectorSupportedGameId}/data");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }


    [Fact]
    public async Task GetEvents_RequiresAuth()
    {
        var response = await Client.GetAsync("/api/v1/connector/events");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// The connector's event picker reads this route, and it is the first
    /// authenticated call in the connect sequence — so it must exist in every
    /// environment (see <see cref="ConnectorRouteAvailabilityTests"/>) and
    /// carry the games the public events list deliberately omits.
    /// </summary>
    [Fact]
    public async Task GetEvents_ListsOnlyEventsTheCallerCompetesIn_WithTheirGames()
    {
        var (owner, ownerKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services);
        var (competitor, competitorKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "comp", UserRole.User);
        var mine = await SeedEventAsync(owner.Id);
        var notMine = await SeedEventAsync(owner.Id);
        var ownerClient = TestAuth.CreateAuthenticatedClient(Factory, ownerKey);
        var addResp = await ownerClient.PostAsJsonAsync($"/api/v1/events/{mine.Id}/games", new { gameId = ConnectorSupportedGameId });
        var eventGameId = (await addResp.Content.ReadFromJsonAsync<CreatedGameResponse>())!.Id;
        (await ownerClient.PostAsJsonAsync($"/api/v1/events/{mine.Id}/games/custom", new { name = "Board game" }))
            .EnsureSuccessStatusCode();
        await ownerClient.PostAsJsonAsync($"/api/v1/events/{mine.Id}/competitors", new { userId = competitor.Id });
        await ownerClient.PostAsJsonAsync($"/api/v1/events/{notMine.Id}/games", new { gameId = ConnectorSupportedGameId });

        var competitorClient = TestAuth.CreateAuthenticatedClient(Factory, competitorKey);
        var response = await competitorClient.GetAsync("/api/v1/connector/events");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var events = await response.Content.ReadFromJsonAsync<List<ConnectorEventResponse>>();
        var ev = events.Should().ContainSingle().Subject;
        ev.Id.Should().Be(mine.Id);
        ev.IsStarted.Should().BeFalse();
        ev.Games.Should().HaveCount(2);
        var known = ev.Games.Single(g => g.EventGameId == eventGameId);
        known.KnownGameId.Should().Be(ConnectorSupportedGameId);
        known.ConnectorSupported.Should().BeTrue();
        known.RequiredConnectorVersion.Should().NotBeNullOrWhiteSpace();
        var custom = ev.Games.Single(g => g.EventGameId != eventGameId);
        custom.KnownGameId.Should().BeNull();
        custom.GameName.Should().Be("Board game");
        custom.ConnectorSupported.Should().BeFalse();
    }

    [Fact]
    public async Task GetEvents_HidesArchivedEvents()
    {
        var (owner, ownerKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services);
        var ev = await SeedEventAsync(owner.Id);
        var ownerClient = TestAuth.CreateAuthenticatedClient(Factory, ownerKey);
        await ownerClient.PostAsJsonAsync($"/api/v1/events/{ev.Id}/competitors", new { userId = owner.Id });
        (await ownerClient.GetFromJsonAsync<List<ConnectorEventResponse>>("/api/v1/connector/events"))
            .Should().ContainSingle();

        await ownerClient.PostAsync($"/api/v1/events/{ev.Id}/archive", null);

        (await ownerClient.GetFromJsonAsync<List<ConnectorEventResponse>>("/api/v1/connector/events"))
            .Should().BeEmpty();
    }

    [Fact]
    public async Task SubmitGameData_RuleMatches_CompletesObjectiveAndIsIdempotent()
    {
        var (owner, ownerKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services);
        var ev = await SeedEventAsync(owner.Id);
        var ownerClient = TestAuth.CreateAuthenticatedClient(Factory, ownerKey);
        var addResp = await ownerClient.PostAsJsonAsync($"/api/v1/events/{ev.Id}/games", new { gameId = ConnectorSupportedGameId });
        var eventGameId = (await addResp.Content.ReadFromJsonAsync<CreatedGameResponse>())!.Id;
        await StartEventAsync(ev.Id);
        await ownerClient.PostAsync($"/api/v1/events/{ev.Id}/games/{eventGameId}/enable", null);
        await ownerClient.PostAsJsonAsync($"/api/v1/events/{ev.Id}/competitors", new { userId = owner.Id });

        await ownerClient.PostAsJsonAsync(
            $"/api/v1/events/{ev.Id}/games/{eventGameId}/objectives/",
            new
            {
                name = "Reach checkpoint",
                score = 5,
                rule = $$"""{">":[{"var":"{{ConnectorFlagDataPointId}}"},0]}""",
            });

        var first = await ownerClient.PostAsJsonAsync(
            $"/api/v1/connector/events/{ev.Id}/games/{eventGameId}/submit",
            new { data = $$"""{"{{ConnectorFlagDataPointId}}":1}""" });
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        (await first.Content.ReadFromJsonAsync<ResultDto>())!.CompletedCount.Should().Be(1);

        // Re-submit with the same data: already completed, so 0 new completions
        var second = await ownerClient.PostAsJsonAsync(
            $"/api/v1/connector/events/{ev.Id}/games/{eventGameId}/submit",
            new { data = $$"""{"{{ConnectorFlagDataPointId}}":1}""" });
        (await second.Content.ReadFromJsonAsync<ResultDto>())!.CompletedCount.Should().Be(0);
    }

    /// <summary>
    /// Seeded unstarted so games/objectives can be added through the API
    /// (adding games to a running event is a 409). Callers that need a running
    /// event call <see cref="StartEventAsync"/> once setup is done.
    /// </summary>
    private async Task<Event> SeedEventAsync(Guid ownerId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ev = new Event { Name = "test", CreatedById = ownerId };
        db.Events.Add(ev);
        await db.SaveChangesAsync();
        return ev;
    }

    private async Task StartEventAsync(Guid eventId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ev = await db.Events.FirstAsync(e => e.Id == eventId);
        ev.IsStarted = true;
        await db.SaveChangesAsync();
    }

    private record VersionDto(string RequiredVersion);
    private record SupportedGameDto(int Id, string Name, string? RequiredConnectorVersion);
    private record ResultDto(int CompletedCount, int FailedCount);
    private record CreatedGameResponse(Guid Id);
}
