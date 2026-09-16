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
/// The planned-run routes over the wire: authentication, the anonymous read,
/// and that the JSON binder accepts every timestamp form a client may send.
/// Who may write a run, the ordering rule and what instant an offset resolves
/// to live in <c>Soulsjwa.IntegrationTests.ScheduleTests</c>.
/// </summary>
public class PlannedRunsEndpointTests : ApiTestBase
{
    [Fact]
    public async Task Create_AsCompetitorForSelf_Returns201()
    {
        var (owner, _) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "owner");
        var (comp, compKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "comp",
            role: Soulsjwa.Api.Features.Auth.Entities.UserRole.User);
        var (ev, eg) = await SeedEventWithGameAsync(owner.Id, competitorIds: comp.Id);
        var client = TestAuth.CreateAuthenticatedClient(Factory, compKey);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/events/{ev.Id}/competitors/{comp.Id}/planned-runs",
            new { eventGameId = eg.Id, startsAt = "2026-10-01T18:00:00Z", endsAt = "2026-10-01T20:00:00Z" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Create_Anonymous_Returns401()
    {
        var (owner, _) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "owner");
        var (comp, _) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "comp");
        var (ev, eg) = await SeedEventWithGameAsync(owner.Id, competitorIds: comp.Id);
        var anon = Factory.CreateClient();

        var response = await anon.PostAsJsonAsync(
            $"/api/v1/events/{ev.Id}/competitors/{comp.Id}/planned-runs",
            new { eventGameId = eg.Id, startsAt = "2026-10-01T18:00:00Z", endsAt = "2026-10-01T20:00:00Z" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("2026-10-01T18:00:00Z")]
    [InlineData("2026-10-01T20:00:00+02:00")]
    [InlineData("2026-10-01T18:00:00")]
    public async Task Create_AcceptsZuluOffsetAndZonelessTimestamps(string startsAt)
    {
        // The zone-less form must no longer 500.
        var (owner, _) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "owner");
        var (comp, compKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "comp",
            role: Soulsjwa.Api.Features.Auth.Entities.UserRole.User);
        var (ev, eg) = await SeedEventWithGameAsync(owner.Id, competitorIds: comp.Id);
        var client = TestAuth.CreateAuthenticatedClient(Factory, compKey);

        var response = await client.PostAsJsonAsync(
            $"/api/v1/events/{ev.Id}/competitors/{comp.Id}/planned-runs",
            new { eventGameId = eg.Id, startsAt, endsAt = "2026-10-01T22:00:00Z" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task List_IsAnonymouslyReadable()
    {
        var (owner, _) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "owner");
        var (comp, compKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "comp",
            role: Soulsjwa.Api.Features.Auth.Entities.UserRole.User);
        var (ev, eg) = await SeedEventWithGameAsync(owner.Id, competitorIds: comp.Id);
        var client = TestAuth.CreateAuthenticatedClient(Factory, compKey);
        await client.PostAsJsonAsync(
            $"/api/v1/events/{ev.Id}/competitors/{comp.Id}/planned-runs",
            new { eventGameId = eg.Id, startsAt = "2026-10-01T18:00:00Z", endsAt = "2026-10-01T20:00:00Z" });

        var anon = Factory.CreateClient();
        var list = await anon.GetFromJsonAsync<List<RunDto>>(
            $"/api/v1/events/{ev.Id}/competitors/{comp.Id}/planned-runs");

        list.Should().ContainSingle(r => r.EventGameId == eg.Id);
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

    private sealed record RunDto(
        Guid Id, Guid EventId, Guid EventGameId, Guid UserId,
        DateTime StartsAt, DateTime EndsAt, DateTime CreatedAt, DateTime UpdatedAt);
}
