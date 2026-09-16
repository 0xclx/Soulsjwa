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
/// My Events over the wire: authentication, and the payload's three groups with
/// the field names the tabs read. What the aggregation counts, how it ranks, and
/// what it costs in queries live in
/// <c>Soulsjwa.IntegrationTests.MyEventsAndAuditTests</c>.
/// </summary>
public class MyEventsEndpointTests : ApiTestBase
{
    [Fact]
    public async Task List_Anonymous_Returns401()
    {
        var response = await Client.GetAsync("/api/v1/me/events");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_ReturnsEventsGroupedByRoleWithProgress()
    {
        var (owner, _) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "owner");
        var (competitor, competitorKey) = await TestAuth.CreateUserWithApiKeyAsync(
            Factory.Services, "competitor");
        var (streamer, _) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "streamer");

        Guid competitorEventId;
        Guid delegatedEventId;
        Guid objectiveId;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var competitorEvent = new Event
            {
                Name = "Competitor Event",
                UrlAlias = "competitor-event",
                CreatedById = owner.Id,
                IsStarted = true,
            };
            var delegatedEvent = new Event { Name = "Delegated Event", CreatedById = owner.Id };
            var game = new EventGame
            {
                Event = competitorEvent,
                CustomGameName = "Dark Souls III",
                IsEnabled = true,
            };
            var objective = new Objective { EventGame = game, Name = "Defeat Vordt", Score = 150 };
            db.AddRange(
                competitorEvent,
                delegatedEvent,
                new EventCompetitor { Event = competitorEvent, UserId = competitor.Id },
                new EventCompetitor { Event = delegatedEvent, UserId = streamer.Id, IsStreamer = true },
                new EventCompetitorModerator
                {
                    EventId = delegatedEvent.Id,
                    CompetitorUserId = streamer.Id,
                    ModeratorUserId = competitor.Id,
                },
                game,
                objective,
                new CompletedObjective { Objective = objective, UserId = competitor.Id });
            await db.SaveChangesAsync();
            competitorEventId = competitorEvent.Id;
            delegatedEventId = delegatedEvent.Id;
            objectiveId = objective.Id;
        }

        using var client = TestAuth.CreateAuthenticatedClient(Factory, competitorKey);
        var response = await client.GetAsync("/api/v1/me/events");
        var result = await response.Content.ReadFromJsonAsync<MyEventsDto>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        result!.Competitor.Should().ContainSingle(e =>
            e.EventId == competitorEventId
            && e.UrlAlias == "competitor-event"
            && e.Status == "live"
            && e.Score == 150
            && e.Rank == 1
            && e.IncompleteObjectives == 0
            && e.TotalObjectives == 1);
        result.Delegated.Should().ContainSingle(e =>
            e.EventId == delegatedEventId && e.CompetitorId == streamer.Id);
        result.Owned.Should().BeEmpty();

        var objectives = await client.GetFromJsonAsync<MyEventObjectivesDto>(
            $"/api/v1/me/events/{competitorEventId}/objectives");
        objectives!.Games.Should().ContainSingle()
            .Which.Objectives.Should().ContainSingle(o =>
                o.ObjectiveId == objectiveId && o.Completed && o.Score == 150);
    }

    private sealed record MyEventsDto(
        List<CompetitorEventDto> Competitor,
        List<DelegatedEventDto> Delegated,
        List<OwnedEventDto> Owned,
        bool QuickCompleteEnabled);

    private record CompetitorEventDto(
        Guid EventId,
        string? UrlAlias,
        string Status,
        int Score,
        int Rank,
        int IncompleteObjectives,
        int TotalObjectives);

    private sealed record DelegatedEventDto(
        Guid EventId,
        string? UrlAlias,
        Guid CompetitorId,
        string Status,
        int Score,
        int Rank,
        int IncompleteObjectives,
        int TotalObjectives)
        : CompetitorEventDto(
            EventId,
            UrlAlias,
            Status,
            Score,
            Rank,
            IncompleteObjectives,
            TotalObjectives);

    private sealed record OwnedEventDto(Guid EventId);

    private sealed record MyEventObjectivesDto(List<MyEventGameDto> Games);
    private sealed record MyEventGameDto(Guid GameId, List<MyEventObjectiveDto> Objectives);
    private sealed record MyEventObjectiveDto(Guid ObjectiveId, bool Completed, int Score);
}
