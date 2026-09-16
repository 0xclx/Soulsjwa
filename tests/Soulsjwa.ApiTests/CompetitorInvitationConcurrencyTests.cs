using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Soulsjwa.Api.Features.Auth.Entities;
using Soulsjwa.Api.Features.Events.Entities;
using Soulsjwa.Api.Infrastructure.Auth;
using Soulsjwa.Api.Infrastructure.Data;
using Xunit;

namespace Soulsjwa.ApiTests;

/// <summary>
/// Two concurrent invites of the same brand-new Twitch handle must not 500 —
/// the loser resolves to the winner's placeholder row instead of throwing an
/// uncaught unique-violation. Needs two real concurrent requests against real
/// Postgres: the race is between two whole request pipelines, and the
/// arbitration is a unique index the InMemory provider doesn't have.
/// </summary>
public class CompetitorInvitationConcurrencyTests : ApiTestBase
{
    private async Task<(User Admin, HttpClient Client)> CreateAdminClientAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var admin = new User
        {
            TwitchId = Guid.NewGuid().ToString(),
            TwitchLogin = "admin-" + Guid.NewGuid().ToString("N")[..8],
            DisplayName = "Admin",
            Role = UserRole.Admin,
            IsAllowlisted = true,
        };
        db.Users.Add(admin);
        var (rawKey, prefix) = ApiKeyAuthHandler.GenerateApiKey();
        db.ApiKeys.Add(new ApiKey
        {
            UserId = admin.Id,
            Name = "test-key",
            KeyHash = ApiKeyAuthHandler.HashApiKey(rawKey),
            KeyPrefix = prefix,
        });
        await db.SaveChangesAsync();

        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", rawKey);
        return (admin, client);
    }

    [Fact]
    public async Task ConcurrentInvitesOfSameUnknownHandleToSameEvent_ProduceOneCreatedOneConflictNoServerErrors()
    {
        var (admin, client) = await CreateAdminClientAsync();
        Guid eventId;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var ev = new Event { Name = "race-event", CreatedById = admin.Id };
            db.Events.Add(ev);
            await db.SaveChangesAsync();
            eventId = ev.Id;
        }

        var handle = $"racehandle_{Guid.NewGuid():N}"[..24];

        // Two requests invite the SAME brand-new handle to the SAME event at
        // once, racing on both the placeholder User/allowlist creation and
        // the EventCompetitors row itself.
        var responses = await Task.WhenAll(
            client.PostAsJsonAsync($"/api/v1/events/{eventId}/competitors", new { twitchLogin = handle }),
            client.PostAsJsonAsync($"/api/v1/events/{eventId}/competitors", new { twitchLogin = handle }));

        responses.Select(r => r.StatusCode).Should().OnlyContain(
            code => code == HttpStatusCode.Created || code == HttpStatusCode.Conflict,
            "neither request should 500");
        responses.Should().ContainSingle(r => r.StatusCode == HttpStatusCode.Created);
        responses.Should().ContainSingle(r => r.StatusCode == HttpStatusCode.Conflict);

        using var verifyScope = Factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var users = await verifyDb.Users.Where(u => u.TwitchLogin == handle.ToLower()).ToListAsync();
        users.Should().ContainSingle("only one placeholder User row must exist despite the race");

        var allowlistRows = await verifyDb.AllowlistedTwitchLogins
            .Where(a => a.TwitchLogin == handle.ToLower()).ToListAsync();
        allowlistRows.Should().ContainSingle("only one AllowlistedTwitchLogin row must exist despite the race");

        var competitorRows = await verifyDb.EventCompetitors
            .Where(c => c.EventId == eventId && c.UserId == users[0].Id)
            .ToListAsync();
        competitorRows.Should().ContainSingle("exactly one EventCompetitor row must exist despite the race");
    }
}
