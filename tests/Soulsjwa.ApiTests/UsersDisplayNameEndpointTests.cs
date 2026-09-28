using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Soulsjwa.Api.Features.Calendar.Entities;
using Soulsjwa.Api.Features.Events.Entities;
using Soulsjwa.Api.Infrastructure.Data;
using Xunit;

namespace Soulsjwa.ApiTests;

/// <summary>
/// <c>PATCH /users/me/display-name</c> over the wire, including the part only
/// the HTTP pipeline has: responses cached before a rename show the new name.
/// The rules (audit, no-op changes) live in DisplayNameChangeTests.
/// </summary>
public class UsersDisplayNameEndpointTests : ApiTestBase
{
    private const string Route = "/api/v1/users/me/display-name";

    [Fact]
    public async Task Rename_Returns200_WithTheEffectiveAndTwitchNames()
    {
        var (user, key) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "renamer");
        var client = TestAuth.CreateAuthenticatedClient(Factory, key);

        var response = await client.PatchAsJsonAsync(Route, new { displayName = "Solaire" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("displayName").GetString().Should().Be("Solaire");
        body.GetProperty("displayNameOverride").GetString().Should().Be("Solaire");
        body.GetProperty("twitchDisplayName").GetString().Should().Be(user.TwitchLogin);

        var me = await client.GetFromJsonAsync<JsonElement>("/api/v1/users/me");
        me.GetProperty("displayName").GetString().Should().Be("Solaire");
    }

    [Fact]
    public async Task TooLong_Is400_KeyedDisplayName()
    {
        var (_, key) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "renamer");

        var response = await TestAuth.CreateAuthenticatedClient(Factory, key)
            .PatchAsJsonAsync(Route, new { displayName = new string('x', 51) });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errors").TryGetProperty("displayName", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Anonymous_Is401()
    {
        var response = await Client.PatchAsJsonAsync(Route, new { displayName = "Solaire" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Rename_EvictsCachedScoreboardAndCalendarResponses()
    {
        var (owner, _) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "owner");
        var (competitor, key) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "renamer");
        var eventId = await SeedEventWithPlannedRunAsync(owner.Id, competitor.Id);

        // Warm both caches with the old name.
        (await ScoreboardNameAsync(eventId, competitor.Id)).Should().Be(competitor.TwitchLogin);
        (await CalendarNameAsync(eventId)).Should().Be(competitor.TwitchLogin);

        (await TestAuth.CreateAuthenticatedClient(Factory, key)
            .PatchAsJsonAsync(Route, new { displayName = "Solaire" })).EnsureSuccessStatusCode();

        (await ScoreboardNameAsync(eventId, competitor.Id)).Should().Be("Solaire");
        (await CalendarNameAsync(eventId)).Should().Be("Solaire");
    }

    private async Task<string?> ScoreboardNameAsync(Guid eventId, Guid userId)
    {
        var scoreboard = await Client.GetFromJsonAsync<JsonElement>($"/api/v1/events/{eventId}/scoreboard");
        return scoreboard.GetProperty("entries").EnumerateArray()
            .Single(e => e.GetProperty("userId").GetGuid() == userId)
            .GetProperty("displayName").GetString();
    }

    private async Task<string?> CalendarNameAsync(Guid eventId)
    {
        var calendar = await Client.GetFromJsonAsync<JsonElement>("/api/v1/calendar");
        return calendar.GetProperty("plannedRuns").EnumerateArray()
            .Single(r => r.GetProperty("eventId").GetGuid() == eventId)
            .GetProperty("competitorName").GetString();
    }

    private async Task<Guid> SeedEventWithPlannedRunAsync(Guid ownerId, Guid competitorId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ev = new Event { Name = "rename-cache", CreatedById = ownerId, IsStarted = true };
        db.Events.Add(ev);
        var game = new EventGame { EventId = ev.Id, CustomGameName = "Custom", IsEnabled = true };
        db.EventGames.Add(game);
        db.EventCompetitors.Add(new EventCompetitor { EventId = ev.Id, UserId = competitorId });
        db.PlannedRuns.Add(new PlannedRun
        {
            EventId = ev.Id,
            EventGameId = game.Id,
            UserId = competitorId,
            StartsAt = DateTime.UtcNow,
            EndsAt = DateTime.UtcNow.AddHours(1),
        });
        await db.SaveChangesAsync();
        return ev.Id;
    }
}
