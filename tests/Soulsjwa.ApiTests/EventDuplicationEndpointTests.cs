using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Soulsjwa.Api.Features.Auth.Entities;
using Soulsjwa.Api.Features.Events.Entities;
using Soulsjwa.Api.Infrastructure.Data;
using Xunit;

namespace Soulsjwa.ApiTests;

/// <summary>
/// The duplication route: that it exists, that it is admin-only as a client
/// meets it, and that the copy comes back in the event payload's shape. What a
/// copy carries over and what it resets lives in
/// <c>Soulsjwa.IntegrationTests.EventRulesAndDuplicationTests</c>.
/// </summary>
public class EventDuplicationEndpointTests : ApiTestBase
{
    [Fact]
    public async Task DuplicateEvent_CopiesGamesAndObjectivesOnly()
    {
        var (owner, key) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services);
        var ev = await SeedEventWithGamesAsync(owner.Id, gameCount: 3, objectivesPerGame: 14);
        await SeedCompetitorAsync(ev.Id, owner.Id);

        using var client = TestAuth.CreateAuthenticatedClient(Factory, key);
        var response = await client.PostAsync($"/api/v1/events/{ev.Id}/duplicate", content: null);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var copy = await response.Content.ReadFromJsonAsync<EventDto>();
        copy.Should().NotBeNull();
        copy!.Games.Should().HaveCount(3);
        copy.Games.Sum(g => g.Objectives.Count).Should().Be(3 * 14);
        copy.Competitors.Should().BeEmpty();

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var completedCount = await db.CompletedObjectives.CountAsync(c => c.Objective!.EventGame!.EventId == copy.Id);
        completedCount.Should().Be(0);
    }

    [Fact]
    public async Task DuplicateEvent_NonAdmin_Returns403()
    {
        var (owner, _) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "owner");
        var (_, otherKey) = await TestAuth.CreateUserWithApiKeyAsync(
            Factory.Services, "other", role: UserRole.User);
        var ev = await SeedEventWithGamesAsync(owner.Id, gameCount: 1, objectivesPerGame: 1);

        using var client = TestAuth.CreateAuthenticatedClient(Factory, otherKey);
        var response = await client.PostAsync($"/api/v1/events/{ev.Id}/duplicate", content: null);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<Event> SeedEventWithGamesAsync(
        Guid ownerId, int gameCount, int objectivesPerGame, int enabledGameIndex = -1)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ev = new Event { Name = "source event", CreatedById = ownerId };

        for (var g = 0; g < gameCount; g++)
        {
            var game = new EventGame
            {
                CustomGameName = $"Custom Game {g}",
                IsEnabled = g == enabledGameIndex,
                SortOrder = g,
            };
            for (var o = 0; o < objectivesPerGame; o++)
            {
                game.Objectives.Add(new Objective
                {
                    Name = $"Objective {g}-{o}",
                    Score = 1,
                    Category = "test",
                    SortOrder = o,
                });
            }
            ev.EventGames.Add(game);
        }

        db.Events.Add(ev);
        await db.SaveChangesAsync();
        return ev;
    }

    private async Task SeedCompetitorAsync(Guid eventId, Guid userId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.EventCompetitors.Add(new EventCompetitor { EventId = eventId, UserId = userId });
        await db.SaveChangesAsync();
    }

    private sealed record EventDto(
        Guid Id,
        string Name,
        Guid CreatedById,
        bool IsArchived,
        bool IsStarted,
        bool IsFeatured,
        string? UrlAlias,
        List<EventCompetitorDto> Competitors,
        List<EventGameDto> Games);

    private sealed record EventCompetitorDto(Guid UserId);

    private sealed record EventGameDto(Guid EventGameId, bool IsEnabled, List<ObjectiveDto> Objectives);

    private sealed record ObjectiveDto(Guid Id, string Name);
}
