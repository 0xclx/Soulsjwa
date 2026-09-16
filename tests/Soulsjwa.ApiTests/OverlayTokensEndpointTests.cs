using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Soulsjwa.Api.Features.Events.Entities;
using Soulsjwa.Api.Infrastructure.Data;
using Xunit;

namespace Soulsjwa.ApiTests;

public partial class OverlayTokensEndpointTests : ApiTestBase
{
    [GeneratedRegex("^ot_[A-Za-z0-9_-]+$")]
    private static partial Regex UrlSafeTokenPattern();

    [Fact]
    public async Task CreateToken_AsOwner_ReturnsRawTokenOnceAndPersistsHash()
    {
        var (owner, ownerKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "owner");
        var ev = await CreateEventAsync(owner.Id);

        var ownerClient = TestAuth.CreateAuthenticatedClient(Factory, ownerKey);
        var response = await ownerClient.PostAsJsonAsync(
            $"/api/v1/events/{ev.Id}/overlay-tokens", new { name = "OBS main" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<CreateOverlayTokenDto>();
        body!.Token.Should().StartWith("ot_");
        // Base64Url.EncodeToString replaced the hand-rolled +/= substitution
        // — assert the real URL-safe character set.
        UrlSafeTokenPattern().IsMatch(body.Token).Should().BeTrue();
        body.TokenPrefix.Should().HaveLength(8);
        body.ExpiresAt.Should().NotBeNull();
        body.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddDays(90), TimeSpan.FromMinutes(1));

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = db.EventOverlayTokens.Single(t => t.Id == body.Id);
        stored.TokenHash.Should().NotBeNullOrEmpty();
        // Cleartext token must never be persisted; the hash should differ from the raw value.
        stored.TokenHash.Should().NotBe(body.Token);
        stored.IsRevoked.Should().BeFalse();
    }

    [Fact]
    public async Task CreateToken_AsNonOwnerNonCompetitor_Returns403()
    {
        var (owner, _) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "owner");
        var (_, strangerKey) = await TestAuth.CreateUserWithApiKeyAsync(
            Factory.Services, "stranger", Soulsjwa.Api.Features.Auth.Entities.UserRole.User);
        var ev = await CreateEventAsync(owner.Id);

        var strangerClient = TestAuth.CreateAuthenticatedClient(Factory, strangerKey);
        var response = await strangerClient.PostAsJsonAsync(
            $"/api/v1/events/{ev.Id}/overlay-tokens", new { name = "nope" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateToken_AnonymousCaller_Returns401()
    {
        var (owner, _) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "owner");
        var ev = await CreateEventAsync(owner.Id);

        var response = await Client.PostAsJsonAsync(
            $"/api/v1/events/{ev.Id}/overlay-tokens", new { name = "nope" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ListTokens_OmitsTokenHashAndRaw()
    {
        var (owner, ownerKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "owner");
        var ev = await CreateEventAsync(owner.Id);

        var ownerClient = TestAuth.CreateAuthenticatedClient(Factory, ownerKey);
        await ownerClient.PostAsJsonAsync(
            $"/api/v1/events/{ev.Id}/overlay-tokens", new { name = "one" });

        var listResponse = await ownerClient.GetAsync($"/api/v1/events/{ev.Id}/overlay-tokens");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var raw = await listResponse.Content.ReadAsStringAsync();
        // The raw token value and its full hash must not leak through the list endpoint.
        raw.Should().NotContain("\"token\"");
        raw.Should().NotContain("\"tokenHash\"");
        raw.Should().Contain("tokenPrefix");
    }

    [Fact]
    public async Task GetOverlayScoreboard_WithoutToken_Returns401()
    {
        var (owner, _) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "owner");
        var ev = await CreateEventAsync(owner.Id);

        var response = await Client.GetAsync($"/api/v1/events/{ev.Id}/overlay-scoreboard");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetOverlayScoreboard_WithInvalidToken_Returns401()
    {
        var (owner, _) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "owner");
        var ev = await CreateEventAsync(owner.Id);

        var response = await Client.GetAsync(
            $"/api/v1/events/{ev.Id}/overlay-scoreboard?token=ot_doesnotexistdoesnotexistdoesnt0");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetOverlayScoreboard_WithValidToken_Returns200()
    {
        var (owner, ownerKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "owner");
        var ev = await CreateEventAsync(owner.Id);

        var ownerClient = TestAuth.CreateAuthenticatedClient(Factory, ownerKey);
        var create = await ownerClient.PostAsJsonAsync(
            $"/api/v1/events/{ev.Id}/overlay-tokens", new { name = "valid" });
        var body = await create.Content.ReadFromJsonAsync<CreateOverlayTokenDto>();

        var response = await Client.GetAsync(
            $"/api/v1/events/{ev.Id}/overlay-scoreboard?token={body!.Token}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetOverlayScoreboard_WithRevokedToken_Returns401()
    {
        var (owner, ownerKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "owner");
        var ev = await CreateEventAsync(owner.Id);

        var ownerClient = TestAuth.CreateAuthenticatedClient(Factory, ownerKey);
        var create = await ownerClient.PostAsJsonAsync(
            $"/api/v1/events/{ev.Id}/overlay-tokens", new { name = "revoke-me" });
        var body = await create.Content.ReadFromJsonAsync<CreateOverlayTokenDto>();

        var del = await ownerClient.DeleteAsync(
            $"/api/v1/events/{ev.Id}/overlay-tokens/{body!.Id}");
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await Client.GetAsync(
            $"/api/v1/events/{ev.Id}/overlay-scoreboard?token={body.Token}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetOverlayScoreboard_TokenForDifferentEvent_Returns401()
    {
        var (owner, ownerKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "owner");
        var evA = await CreateEventAsync(owner.Id, "ev-a");
        var evB = await CreateEventAsync(owner.Id, "ev-b");

        var ownerClient = TestAuth.CreateAuthenticatedClient(Factory, ownerKey);
        var create = await ownerClient.PostAsJsonAsync(
            $"/api/v1/events/{evA.Id}/overlay-tokens", new { name = "for-a" });
        var body = await create.Content.ReadFromJsonAsync<CreateOverlayTokenDto>();

        // Same raw token, but used against event B — must not work.
        var response = await Client.GetAsync(
            $"/api/v1/events/{evB.Id}/overlay-scoreboard?token={body!.Token}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateToken_AsCompetitor_Succeeds()
    {
        var (owner, _) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "owner");
        var (competitor, competitorKey) = await TestAuth.CreateUserWithApiKeyAsync(
            Factory.Services, "competitor", Soulsjwa.Api.Features.Auth.Entities.UserRole.User);
        var ev = await CreateEventAsync(owner.Id);
        await AddCompetitorAsync(ev.Id, competitor.Id);

        var competitorClient = TestAuth.CreateAuthenticatedClient(Factory, competitorKey);
        var response = await competitorClient.PostAsJsonAsync(
            $"/api/v1/events/{ev.Id}/overlay-tokens", new { name = "my obs" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<CreateOverlayTokenDto>();
        body!.Token.Should().StartWith("ot_");

        // Token is recorded as created by the competitor — not the event owner —
        // so the scoped list/revoke rules below have something to key on.
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = db.EventOverlayTokens.Single(t => t.Id == body.Id);
        stored.CreatedById.Should().Be(competitor.Id);
    }

    [Fact]
    public async Task ListTokens_AsCompetitor_OnlyReturnsOwnTokens()
    {
        var (owner, ownerKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "owner");
        var (competitorA, keyA) = await TestAuth.CreateUserWithApiKeyAsync(
            Factory.Services, "compA", Soulsjwa.Api.Features.Auth.Entities.UserRole.User);
        var (competitorB, keyB) = await TestAuth.CreateUserWithApiKeyAsync(
            Factory.Services, "compB", Soulsjwa.Api.Features.Auth.Entities.UserRole.User);
        var ev = await CreateEventAsync(owner.Id);
        await AddCompetitorAsync(ev.Id, competitorA.Id);
        await AddCompetitorAsync(ev.Id, competitorB.Id);

        var ownerClient = TestAuth.CreateAuthenticatedClient(Factory, ownerKey);
        var clientA = TestAuth.CreateAuthenticatedClient(Factory, keyA);
        var clientB = TestAuth.CreateAuthenticatedClient(Factory, keyB);

        await ownerClient.PostAsJsonAsync($"/api/v1/events/{ev.Id}/overlay-tokens", new { name = "owner-tok" });
        await clientA.PostAsJsonAsync($"/api/v1/events/{ev.Id}/overlay-tokens", new { name = "a-tok" });
        await clientB.PostAsJsonAsync($"/api/v1/events/{ev.Id}/overlay-tokens", new { name = "b-tok" });

        var listA = await clientA.GetFromJsonAsync<List<OverlayTokenListDto>>(
            $"/api/v1/events/{ev.Id}/overlay-tokens");
        listA.Should().ContainSingle();
        listA![0].Name.Should().Be("a-tok");
        listA[0].CreatedById.Should().Be(competitorA.Id);

        // Owner/admin sees every token on the event regardless of who minted it.
        var listOwner = await ownerClient.GetFromJsonAsync<List<OverlayTokenListDto>>(
            $"/api/v1/events/{ev.Id}/overlay-tokens");
        listOwner.Should().HaveCount(3);
    }

    [Fact]
    public async Task RevokeToken_CompetitorCannotRevokeAnothersToken()
    {
        var (owner, _) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "owner");
        var (competitorA, keyA) = await TestAuth.CreateUserWithApiKeyAsync(
            Factory.Services, "compA", Soulsjwa.Api.Features.Auth.Entities.UserRole.User);
        var (competitorB, keyB) = await TestAuth.CreateUserWithApiKeyAsync(
            Factory.Services, "compB", Soulsjwa.Api.Features.Auth.Entities.UserRole.User);
        var ev = await CreateEventAsync(owner.Id);
        await AddCompetitorAsync(ev.Id, competitorA.Id);
        await AddCompetitorAsync(ev.Id, competitorB.Id);

        var clientA = TestAuth.CreateAuthenticatedClient(Factory, keyA);
        var clientB = TestAuth.CreateAuthenticatedClient(Factory, keyB);

        var createA = await clientA.PostAsJsonAsync(
            $"/api/v1/events/{ev.Id}/overlay-tokens", new { name = "a-tok" });
        var bodyA = await createA.Content.ReadFromJsonAsync<CreateOverlayTokenDto>();

        // Competitor B may not revoke A's token even though both are competitors
        // in the same event — token revocation is scoped to the minter.
        var attempt = await clientB.DeleteAsync(
            $"/api/v1/events/{ev.Id}/overlay-tokens/{bodyA!.Id}");
        attempt.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var ownDelete = await clientA.DeleteAsync(
            $"/api/v1/events/{ev.Id}/overlay-tokens/{bodyA.Id}");
        ownDelete.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task RevokeToken_OwnerCanRevokeCompetitorToken()
    {
        var (owner, ownerKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "owner");
        var (competitor, competitorKey) = await TestAuth.CreateUserWithApiKeyAsync(
            Factory.Services, "comp", Soulsjwa.Api.Features.Auth.Entities.UserRole.User);
        var ev = await CreateEventAsync(owner.Id);
        await AddCompetitorAsync(ev.Id, competitor.Id);

        var ownerClient = TestAuth.CreateAuthenticatedClient(Factory, ownerKey);
        var competitorClient = TestAuth.CreateAuthenticatedClient(Factory, competitorKey);

        var create = await competitorClient.PostAsJsonAsync(
            $"/api/v1/events/{ev.Id}/overlay-tokens", new { name = "comp-tok" });
        var body = await create.Content.ReadFromJsonAsync<CreateOverlayTokenDto>();

        var del = await ownerClient.DeleteAsync(
            $"/api/v1/events/{ev.Id}/overlay-tokens/{body!.Id}");
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task CreateToken_NoCapEnforced()
    {
        // Previously capped at 25; a single event may legitimately have many
        // streamers, so verify we can mint well past the old limit without 4xx.
        var (owner, ownerKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "owner");
        var ev = await CreateEventAsync(owner.Id);
        var ownerClient = TestAuth.CreateAuthenticatedClient(Factory, ownerKey);

        for (var i = 0; i < 30; i++)
        {
            var response = await ownerClient.PostAsJsonAsync(
                $"/api/v1/events/{ev.Id}/overlay-tokens", new { name = $"tok-{i}" });
            response.StatusCode.Should().Be(HttpStatusCode.Created);
        }
    }

    [Fact]
    public async Task UpdateSettings_AsCreator_Returns200_AndTheOverlayPollCarriesTheLook()
    {
        var (owner, ownerKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "owner");
        var ev = await CreateEventAsync(owner.Id);
        var ownerClient = TestAuth.CreateAuthenticatedClient(Factory, ownerKey);
        var create = await ownerClient.PostAsJsonAsync(
            $"/api/v1/events/{ev.Id}/overlay-tokens", new { name = "styled" });
        var created = await create.Content.ReadFromJsonAsync<CreateOverlayTokenDto>();

        var update = await ownerClient.PutAsJsonAsync(
            $"/api/v1/events/{ev.Id}/overlay-tokens/{created!.Id}/settings",
            ValidSettings with { view = "scores", title = "Finals" });

        update.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await update.Content.ReadFromJsonAsync<OverlayTokenListDto>();
        updated!.Settings.Should().NotBeNull();
        updated.Settings!.View.Should().Be("scores");

        // The listing shows the look too, so the app can open it for editing.
        var list = await ownerClient.GetFromJsonAsync<List<OverlayTokenListDto>>(
            $"/api/v1/events/{ev.Id}/overlay-tokens");
        list.Should().ContainSingle().Which.Settings!.Title.Should().Be("Finals");

        // And the anonymous OBS poll gets scoreboard and look in one body.
        var poll = await Client.GetFromJsonAsync<OverlayScoreboardDto>(
            $"/api/v1/events/{ev.Id}/overlay-scoreboard?token={created.Token}");
        poll!.Scoreboard.Should().NotBeNull();
        poll.Settings!.View.Should().Be("scores");
    }

    [Fact]
    public async Task UpdateSettings_OutOfRange_Returns400_NamingTheKnob()
    {
        var (owner, ownerKey) = await TestAuth.CreateUserWithApiKeyAsync(Factory.Services, "owner");
        var ev = await CreateEventAsync(owner.Id);
        var ownerClient = TestAuth.CreateAuthenticatedClient(Factory, ownerKey);
        var create = await ownerClient.PostAsJsonAsync(
            $"/api/v1/events/{ev.Id}/overlay-tokens", new { name = "styled" });
        var created = await create.Content.ReadFromJsonAsync<CreateOverlayTokenDto>();

        var update = await ownerClient.PutAsJsonAsync(
            $"/api/v1/events/{ev.Id}/overlay-tokens/{created!.Id}/settings",
            ValidSettings with { pageSize = 0 });

        update.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await update.Content.ReadFromJsonAsync<ValidationProblemDto>();
        problem!.Errors.Should().ContainKey("PageSize");
    }

    private static readonly SettingsWireDto ValidSettings = new(
        view: "objectives",
        theme: "dark",
        gameIds: null,
        playerIds: null,
        pageSize: 10,
        cycleSeconds: 30,
        refreshSeconds: 5,
        showTitle: true,
        showProgress: true,
        showPagination: true,
        highlight: true,
        highlightSeconds: 6,
        animate: true,
        panelOpacity: 80,
        title: null);

    private async Task AddCompetitorAsync(Guid eventId, Guid userId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.EventCompetitors.Add(new EventCompetitor { EventId = eventId, UserId = userId });
        await db.SaveChangesAsync();
    }

    private async Task<Event> CreateEventAsync(Guid ownerId, string name = "overlay-token-test")
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ev = new Event { Name = name, CreatedById = ownerId };
        db.Events.Add(ev);
        await db.SaveChangesAsync();
        return ev;
    }

    private record CreateOverlayTokenDto(
        Guid Id, string Name, string Token, string TokenPrefix, DateTime CreatedAt, DateTime? ExpiresAt);

    private record OverlayTokenListDto(
        Guid Id, string Name, string TokenPrefix, Guid CreatedById, DateTime CreatedAt, DateTime? LastUsedAt, DateTime? ExpiresAt,
        SettingsDto? Settings);

    private record SettingsDto(string View, string Theme, int PageSize, string? Title);

    private record OverlayScoreboardDto(ScoreboardDto Scoreboard, SettingsDto? Settings);

    private record ScoreboardDto(string TieBreakMode);

    private record ValidationProblemDto(Dictionary<string, string[]> Errors);

    // Sent as the SPA sends it: every knob, camelCase.
    private record SettingsWireDto(
        string view, string theme, List<Guid>? gameIds, List<Guid>? playerIds,
        int pageSize, int cycleSeconds, int refreshSeconds,
        bool showTitle, bool showProgress, bool showPagination, bool highlight,
        int highlightSeconds, bool animate, int panelOpacity, string? title);
}
