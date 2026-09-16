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
/// The permission system's HTTP contract: admin-only routes answer 403 to a
/// plain user, the allowlist route round-trips an entry, and delegation and
/// on-behalf-of completion work through the real auth pipeline — the one part
/// of the scheme that only exists once a request has been authenticated. The
/// rules themselves (what the allowlist stores, the last-admin guard,
/// per-streamer delegation and its cascade) live in
/// <c>Soulsjwa.IntegrationTests.CompetitorAndDelegationTests</c>.
/// </summary>
public class PermissionsEndpointTests : ApiTestBase
{
    [Fact]
    public async Task CreateEvent_NonAdminUser_Returns403()
    {
        var (_, key) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, role: UserRole.User);
        var client = TestAuth.CreateAuthenticatedClient(Factory, key);

        var response = await client.PostAsJsonAsync("/api/v1/events/", new { name = "x", description = "" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ListAllowlist_NonAdmin_Returns403()
    {
        var (_, key) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, role: UserRole.User);
        var client = TestAuth.CreateAuthenticatedClient(Factory, key);
        var response = await client.GetAsync("/api/v1/admin/allowlist/");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AddAllowlist_AsAdmin_Returns201AndIsListed()
    {
        var (_, key) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, role: UserRole.Admin);
        var client = TestAuth.CreateAuthenticatedClient(Factory, key);

        var add = await client.PostAsJsonAsync("/api/v1/admin/allowlist/", new { twitchLogin = "Streamer1", note = "main partner" });
        add.StatusCode.Should().Be(HttpStatusCode.Created);

        var list = await client.GetAsync("/api/v1/admin/allowlist/");
        var entries = await list.Content.ReadFromJsonAsync<List<AllowlistEntryDto>>();
        entries!.Select(e => e.TwitchLogin).Should().Contain("streamer1"); // stored lowercased
    }

    [Fact]
    public async Task AddModerator_AsStreamer_Works()
    {
        var (admin, _) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, role: UserRole.Admin);
        var (streamer, streamerKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "streamer", role: UserRole.User);
        var (mod, _) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "mod", role: UserRole.User);
        var ev = await SeedEventWithStreamerAsync(admin.Id, streamer.Id);

        var client = TestAuth.CreateAuthenticatedClient(Factory, streamerKey);
        var response = await client.PostAsJsonAsync(
            $"/api/v1/events/{ev.Id}/competitors/{streamer.Id}/moderators/",
            new { userId = mod.Id });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CompleteObjective_OnBehalfOfStreamer_AsDelegatedMod_Succeeds()
    {
        var (admin, _) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, role: UserRole.Admin);
        var (streamer, _) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "streamer", role: UserRole.User);
        var (mod, modKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "mod", role: UserRole.User);

        var (eventId, eventGameId, objectiveId) = await SeedFullEventAsync(admin.Id, streamer.Id, mod.Id);

        var client = TestAuth.CreateAuthenticatedClient(Factory, modKey);
        var url = $"/api/v1/events/{eventId}/games/{eventGameId}/objectives/{objectiveId}/complete?onBehalfOfUserId={streamer.Id}";
        var response = await client.PostAsync(url, null);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.CompletedObjectives.AnyAsync(c => c.ObjectiveId == objectiveId && c.UserId == streamer.Id)).Should().BeTrue();
    }

    private async Task<Event> SeedEventWithStreamerAsync(Guid ownerId, Guid streamerId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ev = new Event { Name = "ev", CreatedById = ownerId };
        db.Events.Add(ev);
        await db.SaveChangesAsync();
        db.EventCompetitors.Add(new EventCompetitor { EventId = ev.Id, UserId = streamerId, IsStreamer = true });
        await db.SaveChangesAsync();
        return ev;
    }

    private async Task<(Guid EventId, Guid EventGameId, Guid ObjectiveId)> SeedFullEventAsync(
        Guid ownerId, Guid streamerId, Guid moderatorId, Guid? extraStreamer = null)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ev = new Event { Name = "ev", CreatedById = ownerId, IsStarted = true };
        db.Events.Add(ev);
        db.EventCompetitors.Add(new EventCompetitor { Event = ev, UserId = streamerId, IsStreamer = true });
        if (extraStreamer.HasValue)
            db.EventCompetitors.Add(new EventCompetitor { Event = ev, UserId = extraStreamer.Value, IsStreamer = true });
        var eg = new EventGame { Event = ev, KnownGameId = 1, IsEnabled = true };
        db.EventGames.Add(eg);
        var obj = new Objective { EventGame = eg, Name = "Beat boss", Score = 10, IsPredefined = false };
        db.Objectives.Add(obj);
        await db.SaveChangesAsync();

        db.EventCompetitorModerators.Add(new EventCompetitorModerator
        {
            EventId = ev.Id,
            CompetitorUserId = streamerId,
            ModeratorUserId = moderatorId,
        });
        await db.SaveChangesAsync();
        return (ev.Id, eg.Id, obj.Id);
    }

    private record AllowlistEntryDto(Guid Id, string TwitchLogin, string? Note, DateTime CreatedAt, Guid? AddedById, Guid? LinkedUserId, string? LinkedDisplayName);
}
