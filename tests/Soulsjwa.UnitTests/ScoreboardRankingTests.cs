using FluentAssertions;
using Soulsjwa.Api.Features.Connector;
using Soulsjwa.Api.Features.Events;
using Soulsjwa.Api.Features.Events.Entities;
using Soulsjwa.Shared;
using Xunit;

namespace Soulsjwa.UnitTests;

public class ScoreboardRankingTests
{
    private sealed record E(int TotalScore, long? TotalInGameTimeMs, DateTime? LastCompletedAt, string Tag = "")
        : IScoreboardSortable;

    private static readonly DateTime T0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ObjectiveOutcome_ForCompetitorWithNoObjectives_IsPending()
    {
        ObjectiveOutcomeCalculator.ForCompetitor(0, 0, 0)
            .Should().Be(ObjectiveOutcome.Pending);
    }

    [Fact]
    public void Sort_OrdersByScoreDesc_ThenIngameTimeAsc()
    {
        var a = new E(10, 5000, T0, "a");
        var b = new E(10, 3000, T0.AddMinutes(10), "b"); // earlier ingame time wins, even though later realtime
        var c = new E(20, null, T0, "c");

        var sorted = ScoreboardRanking.Sort(new[] { a, b, c });

        sorted.Select(x => x.Tag).Should().Equal("c", "b", "a");
    }

    [Fact]
    public void Sort_FallsBackToLastCompletedAt_WhenIngameTimeMissing()
    {
        var a = new E(10, null, T0.AddMinutes(5), "a");
        var b = new E(10, null, T0, "b"); // earlier realtime wins

        var sorted = ScoreboardRanking.Sort(new[] { a, b });

        sorted.Select(x => x.Tag).Should().Equal("b", "a");
    }

    [Fact]
    public void Sort_PrefersEntryWithIngameTime_OverEntryWithout()
    {
        var with = new E(10, 100_000, T0.AddHours(1), "with");
        var without = new E(10, null, T0, "without"); // realtime is earlier but lacks ingame time

        // Present-vs-absent: present should win (the player with the
        // verifiable timer should be ranked ahead).
        var sorted = ScoreboardRanking.Sort(new[] { without, with });
        sorted.Select(x => x.Tag).Should().Equal("with", "without");
    }

    [Fact]
    public void AssignRanks_ByTime_IsStrictOrdinal_EvenWhenTied()
    {
        var entries = new[]
        {
            new E(10, 1000, T0),
            new E(10, 1000, T0), // exact tie
            new E(5, null, T0),
        };

        ScoreboardRanking.AssignRanks(entries, TieBreakMode.ByTime)
            .Should().Equal(1, 2, 3);
    }

    [Fact]
    public void AssignRanks_SharedPlace_AssignsCompetitionRanking_ForEqualScores()
    {
        var entries = new[]
        {
            new E(10, 1000, T0),
            new E(10, 5000, T0.AddSeconds(5)), // different times do not break a score tie
            new E(5, null, T0),
        };

        // 1, 1, 3 ("1224" competition ranking)
        ScoreboardRanking.AssignRanks(entries, TieBreakMode.SharedPlace)
            .Should().Equal(1, 1, 3);
    }

    [Fact]
    public void AssignRanks_SharedPlace_IgnoresLastCompletedAt_WhenTimesMissing()
    {
        var entries = new[]
        {
            new E(10, null, T0),
            new E(10, null, T0),
            new E(10, null, T0.AddMinutes(5)),
        };

        ScoreboardRanking.AssignRanks(entries, TieBreakMode.SharedPlace)
            .Should().Equal(1, 1, 1);
    }

    [Fact]
    public void AssignRanks_EmptyList_Returns_Empty()
    {
        ScoreboardRanking.AssignRanks(Array.Empty<E>(), TieBreakMode.ByTime).Should().BeEmpty();
        ScoreboardRanking.AssignRanks(Array.Empty<E>(), TieBreakMode.SharedPlace).Should().BeEmpty();
    }

    [Fact]
    public void ExtractMilliseconds_SoulMemoryMs_PassedThrough()
    {
        // SoulMemory games use a per-game prefixed id, already in ms.
        var id = $"g{GameIds.DarkSouls1Remastered}_game_time_ms";
        var json = "{\"" + id + "\":98765}";

        var ms = ConnectorIngameTime.ExtractMilliseconds(json, GameIds.DarkSouls1Remastered);

        ms.Should().Be(98_765);
    }

    [Fact]
    public void ExtractMilliseconds_MalformedJson_ReturnsNull()
    {
        ConnectorIngameTime.ExtractMilliseconds("not-json", GameIds.DarkSouls1Remastered)
            .Should().BeNull();
    }

    [Fact]
    public void ExtractMilliseconds_NullKnownGameId_ReturnsNull()
    {
        ConnectorIngameTime.ExtractMilliseconds("{\"g1_game_time_ms\":98765}", null)
            .Should().BeNull();
    }

    [Fact]
    public void ExtractMilliseconds_MissingField_ReturnsNull()
    {
        ConnectorIngameTime.ExtractMilliseconds("{\"100\":1}", GameIds.DarkSouls1Remastered)
            .Should().BeNull();
    }

}
