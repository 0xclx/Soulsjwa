using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Soulsjwa.Api.Common;
using Soulsjwa.Api.Features.Audits;
using Soulsjwa.Api.Features.Auth.Entities;
using Soulsjwa.Api.Features.TwitchExtension;
using Soulsjwa.Api.Features.TwitchExtension.Endpoints;
using Soulsjwa.Api.Features.TwitchExtension.Entities;
using Soulsjwa.Api.Features.TwitchExtension.Services;
using Soulsjwa.Api.Infrastructure.Data;
using Xunit;

namespace Soulsjwa.IntegrationTests;

/// <summary>
/// The extension-wide rules an admin sets: how they are saved and audited,
/// and how they bend what a channel shows and what a broadcaster may save.
/// </summary>
public class TwitchExtensionPolicyTests : IntegrationTestBase
{
    private const string ChannelId = "555";
    private static readonly NullTwitchExtensionPushNotifier Notifier = new();

    private sealed class Env : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "test";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static readonly IConfiguration Configuration = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["Frontend:Url"] = "https://events.example.com/" })
        .Build();

    private static readonly TwitchExtensionBundle Bundle = new(Configuration, new Env());

    private static UpdateTwitchExtensionSettingsRequest Rules(
        bool allowEventChoice = true,
        bool allowScopeSwitch = true,
        TwitchExtensionScope defaultScope = TwitchExtensionScope.AllGames,
        bool highlight = true,
        bool trial = true) =>
        new(allowEventChoice, allowScopeSwitch, defaultScope.ToString(), highlight, trial);

    private Task<IResult> SaveRulesAsync(User actor, UpdateTwitchExtensionSettingsRequest request, AppDbContext db) =>
        TwitchExtensionAdminEndpoint.UpdateSettings(
            request, actor.Principal(), db, new DefaultHttpContext(), Audit, Cache,
            TwitchExtensionOptions.Disabled, Bundle, Configuration, default);

    [Fact]
    public async Task WithoutASavedRow_TheDefaultsApply()
    {
        var policy = await TwitchExtensionChannelResolver.LoadPolicyAsync(CreateDbContext(), default);

        policy.Should().BeEquivalentTo(TwitchExtensionSettings.Defaults(), o => o.Excluding(p => p.UpdatedAt));
        var response = await TwitchExtensionEndpoint.BuildScoreboardAsync(ChannelId, CreateDbContext(), default);
        response.Policy.AllowChannelEventChoice.Should().BeTrue();
        response.Policy.AllowViewerScopeSwitch.Should().BeTrue();
        response.Settings.DefaultScope.Should().Be(nameof(TwitchExtensionScope.AllGames));
    }

    [Fact]
    public async Task AnAdminSave_CreatesTheRow_AuditsIt_AndEvictsEveryChannel()
    {
        var db = CreateDbContext();
        var admin = await Fixtures.AddUserAsync(db, "admin", UserRole.Admin);

        var result = await SaveRulesAsync(admin, Rules(allowEventChoice: false, defaultScope: TwitchExtensionScope.ActiveGame, highlight: false), db);

        result.Status().Should().Be(StatusCodes.Status200OK);
        var body = result.Value<TwitchExtensionAdminResponse>();
        body.Settings.Should().BeEquivalentTo(new TwitchExtensionPolicyResponse(false, true, nameof(TwitchExtensionScope.ActiveGame), false, true));
        body.Bundle.ApiUrl.Should().Be("https://events.example.com", "the trailing slash is dropped");
        body.UpdatedById.Should().Be(admin.Id);

        var verify = CreateDbContext();
        var row = await verify.TwitchExtensionSettings.SingleAsync();
        row.Id.Should().Be(TwitchExtensionSettings.SingletonId);
        row.AllowChannelEventChoice.Should().BeFalse();
        var audit = await verify.AuditLogs.SingleAsync(a => a.Type == AuditEventTypes.TwitchExtensionSettingsUpdated);
        audit.ActorUserId.Should().Be(admin.Id);
        audit.BeforeJson.Should().BeNull();
        audit.AfterJson.Should().Contain(nameof(TwitchExtensionScope.ActiveGame));
        Cache.Evicted.Should().Contain(CacheTags.TwitchExtensionAll);
    }

    [Fact]
    public async Task ASecondSave_UpdatesTheSingleRow()
    {
        var db = CreateDbContext();
        var admin = await Fixtures.AddUserAsync(db, "admin", UserRole.Admin);
        await SaveRulesAsync(admin, Rules(allowScopeSwitch: false), db);

        var result = await SaveRulesAsync(admin, Rules(allowScopeSwitch: true, trial: false), CreateDbContext());

        result.Status().Should().Be(StatusCodes.Status200OK);
        var rows = await CreateDbContext().TwitchExtensionSettings.ToListAsync();
        rows.Should().ContainSingle().Which.Should().BeEquivalentTo(new { AllowViewerScopeSwitch = true, DefaultShowTrialProgress = false });
    }

    [Fact]
    public async Task ANonAdmin_IsRefused()
    {
        var db = CreateDbContext();
        var user = await Fixtures.AddUserAsync(db, "user");

        var result = await SaveRulesAsync(user, Rules(), db);

        result.Status().Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task APinnedGameCannotBeTheDefaultForEveryChannel()
    {
        var db = CreateDbContext();
        var admin = await Fixtures.AddUserAsync(db, "admin", UserRole.Admin);

        var result = await SaveRulesAsync(admin, Rules(defaultScope: TwitchExtensionScope.PinnedGame), db);

        result.ValidationErrors().Should().ContainKey(nameof(UpdateTwitchExtensionSettingsRequest.DefaultScope));
    }

    [Fact]
    public async Task DisallowingEventChoice_ForcesEveryChannelOntoTheFeaturedEvent_AndRefusesNewPicks()
    {
        var db = CreateDbContext();
        var admin = await Fixtures.AddUserAsync(db, "admin", UserRole.Admin);
        var featured = await Fixtures.AddEventAsync(db);
        db.Events.Attach(featured.Event).Entity.IsFeatured = true;
        var picked = await Fixtures.AddEventAsync(db);
        db.TwitchExtensionChannelSettings.Add(new TwitchExtensionChannelSettings
        {
            ChannelId = ChannelId,
            EventId = picked.Event.Id,
            UpdatedById = picked.Competitor.Id,
        });
        await db.SaveChangesAsync();
        await SaveRulesAsync(admin, Rules(allowEventChoice: false), CreateDbContext());

        var board = await TwitchExtensionEndpoint.BuildScoreboardAsync(ChannelId, CreateDbContext(), default);
        var save = await TwitchExtensionEndpoint.ApplyConfigurationAsync(
            ChannelId, picked.Competitor,
            new UpdateTwitchExtensionConfigurationRequest(picked.Event.Id, nameof(TwitchExtensionScope.AllGames), null),
            CreateDbContext(), Audit, Cache, Notifier, default);

        board.Event!.Id.Should().Be(featured.Event.Id);
        board.Event.Source.Should().Be(nameof(TwitchExtensionEventSource.Featured));
        board.Settings.EventId.Should().BeNull("the saved pick is suppressed, not deleted");
        (await CreateDbContext().TwitchExtensionChannelSettings.SingleAsync()).EventId.Should().Be(picked.Event.Id);
        save.ValidationErrors().Should().ContainKey(nameof(UpdateTwitchExtensionConfigurationRequest.EventId));
    }

    [Fact]
    public async Task TheAdminDefaults_ApplyToChannelsWithoutTheirOwnSettings_Only()
    {
        var db = CreateDbContext();
        var admin = await Fixtures.AddUserAsync(db, "admin", UserRole.Admin);
        var f = await Fixtures.AddEventAsync(db);
        db.TwitchExtensionChannelSettings.Add(new TwitchExtensionChannelSettings
        {
            ChannelId = "own",
            DefaultScope = TwitchExtensionScope.AllGames,
            ShowTrialProgress = true,
            UpdatedById = f.Competitor.Id,
        });
        await db.SaveChangesAsync();
        await SaveRulesAsync(admin, Rules(defaultScope: TwitchExtensionScope.ActiveGame, trial: false), CreateDbContext());

        var fresh = await TwitchExtensionEndpoint.BuildScoreboardAsync("fresh", CreateDbContext(), default);
        var own = await TwitchExtensionEndpoint.BuildScoreboardAsync("own", CreateDbContext(), default);

        fresh.Settings.DefaultScope.Should().Be(nameof(TwitchExtensionScope.ActiveGame));
        fresh.Settings.ShowTrialProgress.Should().BeFalse();
        own.Settings.DefaultScope.Should().Be(nameof(TwitchExtensionScope.AllGames));
        own.Settings.ShowTrialProgress.Should().BeTrue();
    }
}
