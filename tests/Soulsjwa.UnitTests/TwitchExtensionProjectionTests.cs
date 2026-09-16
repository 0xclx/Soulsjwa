using FluentAssertions;
using Soulsjwa.Api.Features.Events.Endpoints;
using Soulsjwa.Api.Features.Events.Entities;
using Soulsjwa.Api.Features.TwitchExtension.Endpoints;
using Soulsjwa.Api.Features.TwitchExtension.Entities;
using Xunit;

namespace Soulsjwa.UnitTests;

/// <summary>
/// The slim projection the extension polls: everything a row needs to be
/// narrowed to one game, and nothing per objective.
/// </summary>
public class TwitchExtensionProjectionTests
{
    private static readonly DateTime Earlier = new(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Later = new(2026, 9, 1, 11, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ProjectEntries_KeepsTotalsAndPerGameFigures_DropsObjectives()
    {
        var gameId = Guid.NewGuid();
        var trial = new TrialProgress(Guid.NewGuid(), nameof(TrialRunState.Running), 3, 1, 0, Later);
        var scoreboard = new ScoreboardResponse(
            [
                new ScoreboardEntry(
                    Guid.NewGuid(), "Mira", "mira", null, IsLive: true, TotalScore: 25, CompletedCount: 2,
                    IsFinished: false, LastCompletedAt: Later, TotalInGameTimeMs: 1234, Rank: 1,
                    Games:
                    [
                        new GameBreakdown(
                            gameId, "Elden Ring", 25, 2, 5,
                            [
                                new ObjectiveDetail(Guid.NewGuid(), "a", 10, "Limgrave", true, Earlier),
                                new ObjectiveDetail(Guid.NewGuid(), "b", 15, "Limgrave", true, Later),
                                new ObjectiveDetail(Guid.NewGuid(), "c", 1, null, false, null, IsFailed: true, FailedAt: Later),
                            ],
                            [], false, FailedCount: 1, IsEnabled: true, IsTrialActive: true, Trial: trial),
                    ],
                    FailedCount: 1),
            ],
            nameof(TieBreakMode.SharedPlace));

        var entries = TwitchExtensionEndpoint.ProjectEntries(scoreboard);

        var entry = entries.Should().ContainSingle().Subject;
        entry.Should().BeEquivalentTo(new
        {
            DisplayName = "Mira",
            IsLive = true,
            Rank = 1,
            TotalScore = 25,
            CompletedCount = 2,
            FailedCount = 1,
            LastCompletedAt = Later,
            TotalInGameTimeMs = 1234L,
        });
        var game = entry.Games.Should().ContainSingle().Subject;
        game.Should().BeEquivalentTo(new
        {
            EventGameId = gameId,
            Score = 25,
            CompletedCount = 2,
            FailedCount = 1,
            LastCompletedAt = Later,
            IsTrialActive = true,
            Trial = trial,
        });
        game.GetType().GetProperty("Objectives").Should().BeNull("objectives are fetched per competitor on demand");
    }

    [Fact]
    public void ProjectEntries_AGameWithNoCompletions_HasNoLastCompletedAt()
    {
        var scoreboard = new ScoreboardResponse(
            [
                new ScoreboardEntry(
                    Guid.NewGuid(), "Kai", "kai", null, false, 0, 0, false, null, null, 1,
                    [new GameBreakdown(Guid.NewGuid(), "Sekiro", 0, 0, 0, [], [], false)]),
            ],
            nameof(TieBreakMode.ByTime));

        var game = TwitchExtensionEndpoint.ProjectEntries(scoreboard).Single().Games.Single();

        game.LastCompletedAt.Should().BeNull();
        game.Trial.Should().BeNull();
    }

    [Fact]
    public void ToSettingsResponse_WithoutARow_IsTheFeaturedEventWithTheAdminDefaults()
    {
        var policy = new TwitchExtensionSettings
        {
            DefaultScope = TwitchExtensionScope.ActiveGame,
            DefaultHighlightChannelCompetitor = false,
            DefaultShowTrialProgress = true,
        };

        var settings = TwitchExtensionEndpoint.ToSettingsResponse(null, policy);

        settings.Should().BeEquivalentTo(new TwitchExtensionSettingsResponse(
            null, nameof(TwitchExtensionScope.ActiveGame), null, false, true));
    }

    [Fact]
    public void ToSettingsResponse_SuppressesThePick_WhileTheAdminDisallowsPicks()
    {
        var row = new TwitchExtensionChannelSettings { ChannelId = "1", EventId = Guid.NewGuid() };

        var allowed = TwitchExtensionEndpoint.ToSettingsResponse(row, new TwitchExtensionSettings());
        var disallowed = TwitchExtensionEndpoint.ToSettingsResponse(row, new TwitchExtensionSettings { AllowChannelEventChoice = false });

        allowed.EventId.Should().Be(row.EventId);
        disallowed.EventId.Should().BeNull();
    }

    [Fact]
    public void ToPolicyResponse_ExposesTheScopeByName()
    {
        var policy = TwitchExtensionEndpoint.ToPolicyResponse(new TwitchExtensionSettings
        {
            AllowChannelEventChoice = false,
            AllowViewerScopeSwitch = false,
            DefaultScope = TwitchExtensionScope.ActiveGame,
        });

        policy.Should().BeEquivalentTo(new TwitchExtensionPolicyResponse(false, false, nameof(TwitchExtensionScope.ActiveGame), true, true));
    }

    [Fact]
    public void ToSettingsResponse_ExposesTheScopeByName()
    {
        var eventId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var settings = TwitchExtensionEndpoint.ToSettingsResponse(new TwitchExtensionChannelSettings
        {
            ChannelId = "1",
            EventId = eventId,
            DefaultScope = TwitchExtensionScope.PinnedGame,
            PinnedEventGameId = gameId,
            HighlightChannelCompetitor = false,
            ShowTrialProgress = true,
        }, new TwitchExtensionSettings());

        settings.Should().BeEquivalentTo(new TwitchExtensionSettingsResponse(
            eventId, nameof(TwitchExtensionScope.PinnedGame), gameId, false, true));
    }
}
