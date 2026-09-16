using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Soulsjwa.Api.Common;
using Soulsjwa.Api.Features.Audits;
using Soulsjwa.Api.Features.Auth.Entities;
using Soulsjwa.Api.Features.Events;
using Soulsjwa.Api.Features.Events.Endpoints;
using Soulsjwa.Api.Features.Events.Entities;
using Soulsjwa.Api.Infrastructure.Data;
using Xunit;

namespace Soulsjwa.IntegrationTests;

/// <summary>
/// Who may save an overlay token's look, what a saved look must reference,
/// and that the OBS poll hands it back — the rules over rows, so the handlers
/// are called directly. The HTTP contract is
/// <c>Soulsjwa.ApiTests.OverlayTokensEndpointTests</c>.
/// </summary>
public class OverlayTokenSettingsTests : IntegrationTestBase
{
    private static OverlayTokenSettings Settings(
        string view = OverlayViews.Objectives,
        List<Guid>? gameIds = null,
        List<Guid>? playerIds = null,
        string? title = null) => new(
        View: view,
        Theme: OverlayThemes.Dark,
        GameIds: gameIds,
        PlayerIds: playerIds,
        PageSize: 10,
        CycleSeconds: 30,
        RefreshSeconds: 5,
        ShowTitle: true,
        ShowProgress: true,
        ShowPagination: true,
        Highlight: true,
        HighlightSeconds: 6,
        Animate: true,
        PanelOpacity: 80,
        Title: title);

    /// <summary>Inserts a token the way <c>CreateToken</c> would, returning the raw value the OBS source carries.</summary>
    private static async Task<(EventOverlayToken Token, string Raw)> AddTokenAsync(AppDbContext db, Event ev, User creator)
    {
        var (raw, prefix) = OverlayToken.Generate();
        var token = new EventOverlayToken
        {
            EventId = ev.Id,
            Name = "obs",
            TokenHash = OverlayToken.Hash(raw),
            TokenPrefix = prefix,
            CreatedById = creator.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(1),
        };
        db.EventOverlayTokens.Add(token);
        await db.SaveChangesAsync();
        return (token, raw);
    }

    private Task<IResult> UpdateAsync(AppDbContext db, Event ev, Guid tokenId, User caller, OverlayTokenSettings settings) =>
        OverlayTokensEndpoint.UpdateSettings(ev.Id, tokenId, settings, caller.Principal(), db, Audit, Cache, default);

    [Fact]
    public async Task Creator_SavesLook_WhichIsStoredAuditedAndEvictsTheOverlayCache()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db);
        var (token, _) = await AddTokenAsync(db, f.Event, f.Competitor);

        var result = await UpdateAsync(db, f.Event, token.Id, f.Competitor,
            Settings(view: OverlayViews.Scores, gameIds: [f.Game.Id], playerIds: [f.Competitor.Id], title: " Finals "));

        result.Status().Should().Be(StatusCodes.Status200OK);
        var response = result.Value<OverlayTokenResponse>();
        response.Settings.Should().NotBeNull();
        response.Settings!.View.Should().Be(OverlayViews.Scores);
        response.Settings.Title.Should().Be("Finals", "the stored form is trimmed");

        var stored = await CreateDbContext().EventOverlayTokens.SingleAsync(t => t.Id == token.Id);
        OverlayTokenSettingsJson.Deserialize(stored.SettingsJson)!.GameIds.Should().Equal(f.Game.Id);

        var audit = await CreateDbContext().AuditLogs.SingleAsync(a => a.Type == AuditEventTypes.OverlayTokenSettingsUpdated);
        audit.EventId.Should().Be(f.Event.Id);
        audit.ActorUserId.Should().Be(f.Competitor.Id);

        Cache.Evicted.Should().Contain(CacheTags.Scoreboard(f.Event.Id),
            "the OBS source reads its look from the cached overlay-scoreboard response");
    }

    [Fact]
    public async Task AnotherCompetitor_MayNotChangeTheLook_ButTheOwnerMay()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db);
        var other = await Fixtures.AddUserAsync(db, "other");
        db.EventCompetitors.Add(new EventCompetitor { EventId = f.Event.Id, UserId = other.Id });
        await db.SaveChangesAsync();
        var (token, _) = await AddTokenAsync(db, f.Event, f.Competitor);

        var denied = await UpdateAsync(db, f.Event, token.Id, other, Settings());
        var allowed = await UpdateAsync(db, f.Event, token.Id, f.Owner, Settings());

        denied.Status().Should().Be(StatusCodes.Status403Forbidden);
        allowed.Status().Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task RevokedToken_HasNoLookToEdit()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db);
        var (token, _) = await AddTokenAsync(db, f.Event, f.Competitor);
        token.IsRevoked = true;
        await db.SaveChangesAsync();

        var result = await UpdateAsync(db, f.Event, token.Id, f.Competitor, Settings());

        result.Status().Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task PinnedGame_MustBelongToTheEvent()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db);
        var elsewhere = await Fixtures.AddEventAsync(db);
        var (token, _) = await AddTokenAsync(db, f.Event, f.Competitor);

        var result = await UpdateAsync(db, f.Event, token.Id, f.Competitor,
            Settings(gameIds: [f.Game.Id, elsewhere.Game.Id]));

        result.Status().Should().Be(StatusCodes.Status400BadRequest);
        result.ValidationErrors().Should().ContainKey(nameof(OverlayTokenSettings.GameIds));
        var stored = await CreateDbContext().EventOverlayTokens.SingleAsync(t => t.Id == token.Id);
        stored.SettingsJson.Should().BeNull("nothing is stored when validation fails");
    }

    [Fact]
    public async Task PinnedPlayer_MustCompeteInTheEvent()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db);
        var (token, _) = await AddTokenAsync(db, f.Event, f.Competitor);

        var result = await UpdateAsync(db, f.Event, token.Id, f.Competitor,
            Settings(playerIds: [f.Owner.Id]));

        result.Status().Should().Be(StatusCodes.Status400BadRequest);
        result.ValidationErrors().Should().ContainKey(nameof(OverlayTokenSettings.PlayerIds));
    }

    [Fact]
    public async Task ShapeErrors_AreReportedWithoutMembershipChecks()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db);
        var (token, _) = await AddTokenAsync(db, f.Event, f.Competitor);

        var result = await UpdateAsync(db, f.Event, token.Id, f.Competitor,
            Settings(view: "carousel", gameIds: [Guid.NewGuid()]));

        result.Status().Should().Be(StatusCodes.Status400BadRequest);
        result.ValidationErrors().Should().ContainKey(nameof(OverlayTokenSettings.View));
        result.ValidationErrors().Should().NotContainKey(nameof(OverlayTokenSettings.GameIds),
            "a malformed document is rejected before its pins are looked up");
    }

    [Fact]
    public async Task CreateToken_WithALook_StoresItAlongsideTheToken()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db);

        var result = await OverlayTokensEndpoint.CreateToken(
            f.Event.Id,
            new CreateOverlayTokenRequest("obs", Settings(view: OverlayViews.Games, playerIds: [])),
            f.Competitor.Principal(), db, Audit, default);

        result.Status().Should().Be(StatusCodes.Status201Created);
        var created = result.Value<CreateOverlayTokenResponse>();
        created.Settings.Should().NotBeNull();
        created.Settings!.View.Should().Be(OverlayViews.Games);
        created.Settings.PlayerIds.Should().BeNull("an empty pin list is stored as no pin");

        var stored = await CreateDbContext().EventOverlayTokens.SingleAsync(t => t.Id == created.Id);
        OverlayTokenSettingsJson.Deserialize(stored.SettingsJson)!.View.Should().Be(OverlayViews.Games);
    }

    [Fact]
    public async Task CreateToken_WithAnInvalidLook_MintsNothing()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db);

        var result = await OverlayTokensEndpoint.CreateToken(
            f.Event.Id,
            new CreateOverlayTokenRequest("obs", Settings() with { PageSize = OverlayTokenSettingsLimits.MaxPageSize + 1 }),
            f.Competitor.Principal(), db, Audit, default);

        result.Status().Should().Be(StatusCodes.Status400BadRequest);
        result.ValidationErrors().Should().ContainKey(nameof(OverlayTokenSettings.PageSize));
        (await CreateDbContext().EventOverlayTokens.AnyAsync(t => t.EventId == f.Event.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task OverlayPoll_CarriesTheSavedLook_AndNullBeforeOneIsSaved()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db);
        var (token, raw) = await AddTokenAsync(db, f.Event, f.Competitor);

        var before = await OverlayTokensEndpoint.GetOverlayScoreboard(
            f.Event.Id, CreateDbContext(), new DefaultHttpContext(), default, raw);
        before.Value<OverlayScoreboardResponse>().Settings.Should().BeNull();

        (await UpdateAsync(db, f.Event, token.Id, f.Competitor, Settings(view: OverlayViews.Scores)))
            .Status().Should().Be(StatusCodes.Status200OK);

        var after = await OverlayTokensEndpoint.GetOverlayScoreboard(
            f.Event.Id, CreateDbContext(), new DefaultHttpContext(), default, raw);

        var response = after.Value<OverlayScoreboardResponse>();
        response.Settings!.View.Should().Be(OverlayViews.Scores);
        response.Scoreboard.Entries.Should().ContainSingle(e => e.UserId == f.Competitor.Id,
            "the scoreboard rides in the same response as before");
    }
}
