using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Soulsjwa.Api.Features.Events.Entities;
using Soulsjwa.Api.Infrastructure.Data;
using Xunit;

namespace Soulsjwa.ApiTests;

public class EventGameCompetitorInfosEndpointTests : ApiTestBase
{
    [Fact]
    public async Task Create_AsCompetitor_DeathClipWithTwitchUrl_Returns201()
    {
        var (owner, _) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "owner");
        var (comp, compKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "comp",
            role: Soulsjwa.Api.Features.Auth.Entities.UserRole.User);
        var (ev, eg) = await SeedEventWithGameAsync(owner.Id, competitorIds: comp.Id);
        var client = TestAuth.CreateAuthenticatedClient(Factory, compKey);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/events/{ev.Id}/games/{eg.Id}/competitors/{comp.Id}/infos",
            new { type = "DeathClip", url = "https://clips.twitch.tv/AbCdEfGhIj" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Create_AsEventOwner_Returns201()
    {
        var (owner, ownerKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "owner");
        var (comp, _) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "comp");
        var (ev, eg) = await SeedEventWithGameAsync(owner.Id, competitorIds: comp.Id);
        var client = TestAuth.CreateAuthenticatedClient(Factory, ownerKey);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/events/{ev.Id}/games/{eg.Id}/competitors/{comp.Id}/infos",
            new { type = "Other", text = "hi from owner" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Create_AsUnrelatedUser_Returns403()
    {
        var (owner, _) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "owner");
        var (comp, _) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "comp");
        var (other, otherKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "other",
            role: Soulsjwa.Api.Features.Auth.Entities.UserRole.User);
        var (ev, eg) = await SeedEventWithGameAsync(owner.Id, competitorIds: comp.Id);
        var client = TestAuth.CreateAuthenticatedClient(Factory, otherKey);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/events/{ev.Id}/games/{eg.Id}/competitors/{comp.Id}/infos",
            new { type = "Other", text = "should not work" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_AsDelegatedModerator_Returns201()
    {
        var (owner, _) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "owner");
        // Competitor is also a streamer (so delegations make sense).
        var (comp, _) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "comp");
        var (mod, modKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "mod",
            role: Soulsjwa.Api.Features.Auth.Entities.UserRole.User);
        var (ev, eg) = await SeedEventWithGameAsync(owner.Id, competitorIds: comp.Id);
        await SeedStreamerWithModeratorAsync(ev.Id, comp.Id, mod.Id);

        var client = TestAuth.CreateAuthenticatedClient(Factory, modKey);
        var response = await client.PostAsJsonAsync(
            $"/api/v1/events/{ev.Id}/games/{eg.Id}/competitors/{comp.Id}/infos",
            new { type = "Link", url = "https://example.com/build" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Create_DeathClipWithBadHost_Returns400()
    {
        var (owner, _) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "owner");
        var (comp, compKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "comp");
        var (ev, eg) = await SeedEventWithGameAsync(owner.Id, competitorIds: comp.Id);
        var client = TestAuth.CreateAuthenticatedClient(Factory, compKey);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/events/{ev.Id}/games/{eg.Id}/competitors/{comp.Id}/infos",
            new { type = "DeathClip", url = "https://example.com/not-a-clip" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_OtherWithoutText_Returns400()
    {
        var (owner, _) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "owner");
        var (comp, compKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "comp");
        var (ev, eg) = await SeedEventWithGameAsync(owner.Id, competitorIds: comp.Id);
        var client = TestAuth.CreateAuthenticatedClient(Factory, compKey);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/events/{ev.Id}/games/{eg.Id}/competitors/{comp.Id}/infos",
            new { type = "Other", text = "" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_TargetNotACompetitor_Returns404()
    {
        var (owner, _) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "owner");
        // The caller isn't a competitor in this event, just a random user.
        var (stranger, strangerKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "stranger");
        var (ev, eg) = await SeedEventWithGameAsync(owner.Id);
        var client = TestAuth.CreateAuthenticatedClient(Factory, strangerKey);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/events/{ev.Id}/games/{eg.Id}/competitors/{stranger.Id}/infos",
            new { type = "Other", text = "x" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task List_ReturnsInfosOrderedByCreatedAt()
    {
        var (owner, _) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "owner");
        var (comp, compKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "comp");
        var (ev, eg) = await SeedEventWithGameAsync(owner.Id, competitorIds: comp.Id);
        var client = TestAuth.CreateAuthenticatedClient(Factory, compKey);

        await client.PostAsJsonAsync(
            $"/api/v1/events/{ev.Id}/games/{eg.Id}/competitors/{comp.Id}/infos",
            new { type = "Other", text = "first" });
        await client.PostAsJsonAsync(
            $"/api/v1/events/{ev.Id}/games/{eg.Id}/competitors/{comp.Id}/infos",
            new { type = "DeathClip", url = "https://youtu.be/abc12345678" });

        var anon = Factory.CreateClient();
        var list = await anon.GetFromJsonAsync<List<JsonInfo>>(
            $"/api/v1/events/{ev.Id}/games/{eg.Id}/competitors/{comp.Id}/infos");

        list.Should().NotBeNull();
        list!.Should().HaveCount(2);
        list[0].Type.Should().Be("Other");
        list[1].Type.Should().Be("DeathClip");
    }

    [Fact]
    public async Task Delete_AsCompetitor_Returns204AndRemovesRow()
    {
        var (owner, _) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "owner");
        var (comp, compKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "comp");
        var (ev, eg) = await SeedEventWithGameAsync(owner.Id, competitorIds: comp.Id);
        var client = TestAuth.CreateAuthenticatedClient(Factory, compKey);

        var created = await client.PostAsJsonAsync(
            $"/api/v1/events/{ev.Id}/games/{eg.Id}/competitors/{comp.Id}/infos",
            new { type = "Other", text = "delete me" });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await created.Content.ReadFromJsonAsync<JsonInfo>();

        var del = await client.DeleteAsync(
            $"/api/v1/events/{ev.Id}/games/{eg.Id}/competitors/{comp.Id}/infos/{body!.Id}");
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.EventGameCompetitorInfos.AnyAsync(i => i.Id == body.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task Scoreboard_FlagsHasDeathClip_AndIncludesInfos()
    {
        var (owner, _) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "owner");
        var (comp, compKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "comp");
        var (ev, eg) = await SeedEventWithGameAsync(owner.Id, competitorIds: comp.Id);
        var client = TestAuth.CreateAuthenticatedClient(Factory, compKey);

        await client.PostAsJsonAsync(
            $"/api/v1/events/{ev.Id}/games/{eg.Id}/competitors/{comp.Id}/infos",
            new { type = "DeathClip", url = "https://www.twitch.tv/videos/123" });

        var anon = Factory.CreateClient();
        var board = await anon.GetFromJsonAsync<JsonScoreboard>($"/api/v1/events/{ev.Id}/scoreboard");

        board!.Entries.Should().ContainSingle(e => e.UserId == comp.Id);
        var game = board.Entries.Single(e => e.UserId == comp.Id).Games.Single();
        game.HasDeathClip.Should().BeTrue();
        game.Infos.Should().HaveCount(1);
        game.Infos[0].Type.Should().Be("DeathClip");
    }

    private async Task<(Event Ev, EventGame Eg)> SeedEventWithGameAsync(Guid ownerId, params Guid[] competitorIds)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ev = new Event { Name = "test", CreatedById = ownerId };
        db.Events.Add(ev);
        var eg = new EventGame
        {
            EventId = ev.Id,
            CustomGameName = "Custom",
            IsEnabled = true,
        };
        db.EventGames.Add(eg);
        foreach (var cid in competitorIds)
            db.EventCompetitors.Add(new EventCompetitor { EventId = ev.Id, UserId = cid });
        await db.SaveChangesAsync();
        return (ev, eg);
    }

    private async Task SeedStreamerWithModeratorAsync(Guid eventId, Guid streamerUserId, Guid moderatorUserId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var competitor = await db.EventCompetitors
            .FirstAsync(c => c.EventId == eventId && c.UserId == streamerUserId);
        competitor.IsStreamer = true;
        db.EventCompetitorModerators.Add(new EventCompetitorModerator
        {
            EventId = eventId,
            CompetitorUserId = streamerUserId,
            ModeratorUserId = moderatorUserId,
        });
        await db.SaveChangesAsync();
    }

    private sealed record JsonInfo(Guid Id, Guid EventGameId, Guid UserId, string Type, string? Url, string? Text);
    private sealed record JsonScoreboard(List<JsonLbEntry> Entries);
    private sealed record JsonLbEntry(Guid UserId, List<JsonLbGame> Games);
    private sealed record JsonLbGame(Guid EventGameId, bool HasDeathClip, List<JsonInfo> Infos);
}
