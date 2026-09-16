using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Soulsjwa.Api.Common;
using Soulsjwa.Api.Features.Audits;
using Soulsjwa.Api.Features.Auth.Entities;
using Soulsjwa.Api.Features.Events.Entities;
using Soulsjwa.Api.Features.TwitchExtension.Endpoints;
using Soulsjwa.Api.Features.TwitchExtension.Entities;
using Soulsjwa.Api.Features.TwitchExtension.Services;
using Soulsjwa.Api.Infrastructure.Data;
using Xunit;

namespace Soulsjwa.IntegrationTests;

/// <summary>
/// Which event a channel shows and what the broadcaster may save, computed
/// over rows in the database — so the builders are called directly. The token
/// contract and the HTTP surface are <c>Soulsjwa.ApiTests.TwitchExtensionEndpointTests</c>.
/// </summary>
public class TwitchExtensionTests : IntegrationTestBase
{
    private const string ChannelId = "987654321";

    private static readonly NullTwitchExtensionPushNotifier Notifier = new();

    private static UpdateTwitchExtensionConfigurationRequest Request(
        Guid? eventId = null,
        TwitchExtensionScope scope = TwitchExtensionScope.AllGames,
        Guid? pinned = null) =>
        new(eventId, scope.ToString(), pinned);

    private static async Task FeatureAsync(AppDbContext db, Event ev)
    {
        db.Events.Attach(ev).Entity.IsFeatured = true;
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task AChannelWithNoRowAndNoFeaturedEvent_ShowsNothing()
    {
        var response = await TwitchExtensionEndpoint.BuildScoreboardAsync(ChannelId, CreateDbContext(), default);

        response.Event.Should().BeNull();
        response.Entries.Should().BeEmpty();
        response.Games.Should().BeEmpty();
        response.Settings.EventId.Should().BeNull();
        response.Settings.DefaultScope.Should().Be(nameof(TwitchExtensionScope.AllGames));
    }

    [Fact]
    public async Task AChannelWithNoRow_FollowsTheFeaturedEvent_AndMarksTheBroadcastersOwnRow()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db, score: 12);
        await FeatureAsync(db, f.Event);
        // The broadcaster competes here: their Soulsjwa account carries the channel's Twitch id.
        db.Users.Attach(f.Competitor).Entity.TwitchId = ChannelId;
        db.CompletedObjectives.Add(new CompletedObjective { ObjectiveId = f.Objective.Id, UserId = f.Competitor.Id });
        await db.SaveChangesAsync();

        var response = await TwitchExtensionEndpoint.BuildScoreboardAsync(ChannelId, CreateDbContext(), default);

        response.Event.Should().NotBeNull();
        response.Event!.Id.Should().Be(f.Event.Id);
        response.Event.Source.Should().Be(nameof(TwitchExtensionEventSource.Featured));
        response.Event.ActiveEventGameId.Should().Be(f.Game.Id);
        response.ChannelCompetitorUserId.Should().Be(f.Competitor.Id);
        response.Games.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            EventGameId = f.Game.Id,
            IsEnabled = true,
            TotalObjectives = 1,
        });
        var entry = response.Entries.Should().ContainSingle().Subject;
        entry.TotalScore.Should().Be(12);
        entry.Games.Should().ContainSingle().Which.Score.Should().Be(12);
    }

    [Fact]
    public async Task AnExplicitPick_WinsOverTheFeaturedEvent()
    {
        var db = CreateDbContext();
        var featured = await Fixtures.AddEventAsync(db);
        await FeatureAsync(db, featured.Event);
        var picked = await Fixtures.AddEventAsync(db);
        db.TwitchExtensionChannelSettings.Add(new TwitchExtensionChannelSettings
        {
            ChannelId = ChannelId,
            EventId = picked.Event.Id,
            UpdatedById = picked.Owner.Id,
        });
        await db.SaveChangesAsync();

        var response = await TwitchExtensionEndpoint.BuildScoreboardAsync(ChannelId, CreateDbContext(), default);

        response.Event!.Id.Should().Be(picked.Event.Id);
        response.Event.Source.Should().Be(nameof(TwitchExtensionEventSource.Explicit));
        response.Settings.EventId.Should().Be(picked.Event.Id);
    }

    [Fact]
    public async Task AnArchivedPick_FallsBackToTheFeaturedEvent_KeepingTheRow()
    {
        var db = CreateDbContext();
        var featured = await Fixtures.AddEventAsync(db);
        await FeatureAsync(db, featured.Event);
        var picked = await Fixtures.AddEventAsync(db);
        db.TwitchExtensionChannelSettings.Add(new TwitchExtensionChannelSettings
        {
            ChannelId = ChannelId,
            EventId = picked.Event.Id,
            UpdatedById = picked.Owner.Id,
        });
        db.Events.Attach(picked.Event).Entity.IsArchived = true;
        await db.SaveChangesAsync();

        var response = await TwitchExtensionEndpoint.BuildScoreboardAsync(ChannelId, CreateDbContext(), default);

        response.Event!.Id.Should().Be(featured.Event.Id);
        response.Event.Source.Should().Be(nameof(TwitchExtensionEventSource.Featured));
        response.Settings.EventId.Should().Be(picked.Event.Id, "the pick is kept so the config view can say it is archived");
    }

    [Fact]
    public async Task CompetitorDetail_CarriesTheObjectives_AndIsNullForAStranger()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db);
        db.Objectives.Attach(f.Objective).Entity.Category = "Stormveil";
        db.CompletedObjectives.Add(new CompletedObjective { ObjectiveId = f.Objective.Id, UserId = f.Competitor.Id });
        await db.SaveChangesAsync();

        var detail = await TwitchExtensionEndpoint.BuildCompetitorDetailAsync(f.Event.Id, f.Competitor.Id, CreateDbContext(), default);
        var stranger = await TwitchExtensionEndpoint.BuildCompetitorDetailAsync(f.Event.Id, Guid.NewGuid(), CreateDbContext(), default);

        detail.Should().NotBeNull();
        var game = detail!.Games.Should().ContainSingle().Subject;
        game.EventGameId.Should().Be(f.Game.Id);
        var objective = game.Objectives.Should().ContainSingle().Subject;
        objective.Category.Should().Be("Stormveil");
        objective.IsCompleted.Should().BeTrue();
        stranger.Should().BeNull();
    }

    [Fact]
    public async Task Configuration_ListsEvents_FeaturedFirst_AndFlagsWhereTheLinkedUserCompetes()
    {
        var db = CreateDbContext();
        var other = await Fixtures.AddEventAsync(db);
        var mine = await Fixtures.AddEventAsync(db);
        await FeatureAsync(db, mine.Event);

        var configuration = await TwitchExtensionEndpoint.BuildConfigurationAsync(
            ChannelId, mine.Competitor, CreateDbContext(), default);

        configuration.LinkedUser!.Id.Should().Be(mine.Competitor.Id);
        configuration.ResolvedEvent!.Id.Should().Be(mine.Event.Id);
        configuration.Events.Select(e => e.Id).Should().ContainInOrder(mine.Event.Id, other.Event.Id);
        configuration.Events.Single(e => e.Id == mine.Event.Id).IsCompetitor.Should().BeTrue();
        configuration.Events.Single(e => e.Id == other.Event.Id).IsCompetitor.Should().BeFalse();
        configuration.Events.Single(e => e.Id == mine.Event.Id).Games.Should().ContainSingle()
            .Which.EventGameId.Should().Be(mine.Game.Id);
    }

    [Fact]
    public async Task Configuration_WithoutALinkedUser_StillListsEvents()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db);

        var configuration = await TwitchExtensionEndpoint.BuildConfigurationAsync(ChannelId, null, CreateDbContext(), default);

        configuration.LinkedUser.Should().BeNull();
        configuration.Events.Should().Contain(e => e.Id == f.Event.Id);
        configuration.Events.Should().OnlyContain(e => !e.IsCompetitor);
    }

    [Fact]
    public async Task Save_CreatesTheRow_AuditsIt_AndEvictsTheChannel()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db);

        var result = await TwitchExtensionEndpoint.ApplyConfigurationAsync(
            ChannelId, f.Competitor,
            Request(f.Event.Id, TwitchExtensionScope.PinnedGame, f.Game.Id),
            db, Audit, Cache, Notifier, default);

        result.Status().Should().Be(StatusCodes.Status200OK);
        var body = result.Value<TwitchExtensionConfigurationResponse>();
        body.Settings.Should().BeEquivalentTo(new TwitchExtensionSettingsResponse(
            f.Event.Id, nameof(TwitchExtensionScope.PinnedGame), f.Game.Id, true, true));
        body.ResolvedEvent!.Source.Should().Be(nameof(TwitchExtensionEventSource.Explicit));

        var verify = CreateDbContext();
        var row = await verify.TwitchExtensionChannelSettings.SingleAsync(s => s.ChannelId == ChannelId);
        row.UpdatedById.Should().Be(f.Competitor.Id);
        var audit = await verify.AuditLogs.SingleAsync(a => a.Type == AuditEventTypes.TwitchExtensionConfigurationUpdated);
        audit.ActorUserId.Should().Be(f.Competitor.Id);
        audit.EventId.Should().Be(f.Event.Id);
        audit.EventGameId.Should().Be(f.Game.Id);
        audit.BeforeJson.Should().BeNull("this was a create");
        audit.AfterJson.Should().Contain(nameof(TwitchExtensionScope.PinnedGame));
        Cache.Evicted.Should().Contain(CacheTags.TwitchExtensionChannel(ChannelId));
    }

    [Fact]
    public async Task Save_ASecondTime_UpdatesTheSameRow_WithABeforeSnapshot()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db);
        await TwitchExtensionEndpoint.ApplyConfigurationAsync(
            ChannelId, f.Competitor, Request(f.Event.Id), db, Audit, Cache, Notifier, default);

        var result = await TwitchExtensionEndpoint.ApplyConfigurationAsync(
            ChannelId, f.Owner, Request(null, TwitchExtensionScope.ActiveGame), CreateDbContext(), Audit, Cache, Notifier, default);

        result.Status().Should().Be(StatusCodes.Status200OK);
        var verify = CreateDbContext();
        var row = await verify.TwitchExtensionChannelSettings.SingleAsync(s => s.ChannelId == ChannelId);
        row.EventId.Should().BeNull();
        row.DefaultScope.Should().Be(TwitchExtensionScope.ActiveGame);
        row.UpdatedById.Should().Be(f.Owner.Id);
        var audits = await verify.AuditLogs.Where(a => a.Type == AuditEventTypes.TwitchExtensionConfigurationUpdated).ToListAsync();
        audits.Should().HaveCount(2);
        audits.Should().Contain(a => a.BeforeJson != null && a.BeforeJson.Contains(f.Event.Id.ToString()));
    }

    [Fact]
    public async Task Save_AnUnknownEvent_IsAValidationError()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db);

        var result = await TwitchExtensionEndpoint.ApplyConfigurationAsync(
            ChannelId, f.Competitor, Request(Guid.NewGuid()), db, Audit, Cache, Notifier, default);

        result.Status().Should().Be(StatusCodes.Status400BadRequest);
        result.ValidationErrors().Should().ContainKey(nameof(UpdateTwitchExtensionConfigurationRequest.EventId));
        (await CreateDbContext().TwitchExtensionChannelSettings.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Save_AnArchivedEvent_IsAValidationError()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db);
        db.Events.Attach(f.Event).Entity.IsArchived = true;
        await db.SaveChangesAsync();

        var result = await TwitchExtensionEndpoint.ApplyConfigurationAsync(
            ChannelId, f.Competitor, Request(f.Event.Id), CreateDbContext(), Audit, Cache, Notifier, default);

        result.ValidationErrors().Should().ContainKey(nameof(UpdateTwitchExtensionConfigurationRequest.EventId));
    }

    [Fact]
    public async Task Save_AnUnknownScope_IsAValidationError()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db);

        var result = await TwitchExtensionEndpoint.ApplyConfigurationAsync(
            ChannelId, f.Competitor, new UpdateTwitchExtensionConfigurationRequest(null, "Everything", null),
            db, Audit, Cache, Notifier, default);

        result.ValidationErrors().Should().ContainKey(nameof(UpdateTwitchExtensionConfigurationRequest.DefaultScope));
    }

    [Fact]
    public async Task Save_APinnedGameOfAnotherEvent_IsAValidationError()
    {
        var db = CreateDbContext();
        var shown = await Fixtures.AddEventAsync(db);
        var other = await Fixtures.AddEventAsync(db);

        var result = await TwitchExtensionEndpoint.ApplyConfigurationAsync(
            ChannelId, shown.Competitor,
            Request(shown.Event.Id, TwitchExtensionScope.PinnedGame, other.Game.Id),
            db, Audit, Cache, Notifier, default);

        result.ValidationErrors().Should().ContainKey(nameof(UpdateTwitchExtensionConfigurationRequest.PinnedEventGameId));
    }

    [Fact]
    public async Task Save_PinnedGameWithoutAGame_IsAValidationError()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db);

        var result = await TwitchExtensionEndpoint.ApplyConfigurationAsync(
            ChannelId, f.Competitor, Request(f.Event.Id, TwitchExtensionScope.PinnedGame), db, Audit, Cache, Notifier, default);

        result.ValidationErrors().Should().ContainKey(nameof(UpdateTwitchExtensionConfigurationRequest.PinnedEventGameId));
    }

    [Fact]
    public async Task Save_WhileFollowingTheFeaturedEvent_ValidatesThePinAgainstIt()
    {
        var db = CreateDbContext();
        var featured = await Fixtures.AddEventAsync(db);
        await FeatureAsync(db, featured.Event);

        var result = await TwitchExtensionEndpoint.ApplyConfigurationAsync(
            ChannelId, featured.Competitor,
            Request(null, TwitchExtensionScope.PinnedGame, featured.Game.Id),
            db, Audit, Cache, Notifier, default);

        result.Status().Should().Be(StatusCodes.Status200OK);
        result.Value<TwitchExtensionConfigurationResponse>().Settings.PinnedEventGameId.Should().Be(featured.Game.Id);
    }

    [Fact]
    public async Task Save_ANonPinnedScope_DropsAnyPinnedGame()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db);

        var result = await TwitchExtensionEndpoint.ApplyConfigurationAsync(
            ChannelId, f.Competitor, Request(f.Event.Id, TwitchExtensionScope.AllGames, f.Game.Id), db, Audit, Cache, Notifier, default);

        result.Value<TwitchExtensionConfigurationResponse>().Settings.PinnedEventGameId.Should().BeNull();
    }

    [Fact]
    public async Task DeletingThePickedEvent_LeavesTheRow_FollowingTheFeaturedEvent()
    {
        var db = CreateDbContext();
        var f = await Fixtures.AddEventAsync(db);
        db.TwitchExtensionChannelSettings.Add(new TwitchExtensionChannelSettings
        {
            ChannelId = ChannelId,
            EventId = f.Event.Id,
            PinnedEventGameId = f.Game.Id,
            UpdatedById = f.Owner.Id,
        });
        await db.SaveChangesAsync();

        // Hard delete straight at the database: the FK is ON DELETE SET NULL.
        await db.Database.ExecuteSqlAsync($"DELETE FROM \"Events\" WHERE \"Id\" = {f.Event.Id}");

        var row = await CreateDbContext().TwitchExtensionChannelSettings.SingleAsync(s => s.ChannelId == ChannelId);
        row.EventId.Should().BeNull();
        row.PinnedEventGameId.Should().BeNull();
    }

    [Fact]
    public async Task FindLinkedUser_RequiresTheChannelsTwitchIdAndTheAllowlist()
    {
        var db = CreateDbContext();
        var user = await Fixtures.AddUserAsync(db, "streamer");
        db.Users.Attach(user).Entity.TwitchId = ChannelId;
        await db.SaveChangesAsync();

        (await TwitchExtensionEndpoint.FindLinkedUserAsync(ChannelId, CreateDbContext(), default))!.Id.Should().Be(user.Id);
        (await TwitchExtensionEndpoint.FindLinkedUserAsync("000", CreateDbContext(), default)).Should().BeNull();

        db.Users.Attach(user).Entity.IsAllowlisted = false;
        await db.SaveChangesAsync();
        (await TwitchExtensionEndpoint.FindLinkedUserAsync(ChannelId, CreateDbContext(), default)).Should().BeNull();
    }
}
