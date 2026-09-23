using FluentAssertions;
using Soulsjwa.Api.Features.Admin.SampleEvents;
using Soulsjwa.Api.Features.Events.Entities;
using Xunit;

namespace Soulsjwa.UnitTests;

/// <summary>
/// The sample events are fixed data: the same five events, in the same states,
/// every time. These pin that contract without a database.
/// </summary>
public class SampleEventCatalogTests
{
    private static readonly IReadOnlyList<SampleEventDefinition> Events = SampleEventCatalog.Build();

    [Fact]
    public void HasTheFiveEventsInOrder()
    {
        Events.Select(e => e.Name).Should().Equal(
            "Sample: Not started",
            "Sample: Game in progress",
            "Sample: Between games",
            "Sample: Many games",
            "Sample: Empty setup");
    }

    [Fact]
    public void EveryNameIsPrefixed()
    {
        Events.Should().OnlyContain(e => e.Name.StartsWith(SampleEventCatalog.NamePrefix, StringComparison.Ordinal));
        SampleEventCatalog.NamePrefix.Should().Be("Sample: ");
    }

    [Fact]
    public void CoversTheLifecycleStates()
    {
        Events.Select(e => e.StartedAgo).Should().Equal(
            null, TimeSpan.FromHours(2), TimeSpan.FromDays(1), TimeSpan.FromHours(3), null);
        Events.Select(e => e.EnabledGameIndex).Should().Equal(null, 1, null, 0, null);
        Events.Select(e => e.Games.Count).Should().Equal(3, 3, 3, 7, 0);
        Events.Select(e => e.TieBreakMode).Should().Equal(
            TieBreakMode.SharedPlace, TieBreakMode.SharedPlace, TieBreakMode.ByTime,
            TieBreakMode.ByTime, TieBreakMode.SharedPlace);
    }

    [Fact]
    public void OnlyTheGameInProgressEventHasRules()
    {
        Events.Select(e => e.RulesMarkdown is not null).Should().Equal(false, true, false, false, false);
    }

    [Fact]
    public void AnEnabledGameIndexPointsAtAGame()
    {
        Events.Where(e => e.EnabledGameIndex is not null)
            .Should().OnlyContain(e => e.EnabledGameIndex < e.Games.Count);
    }

    [Fact]
    public void EveryGameHasCategorisedObjectivesInRange()
    {
        foreach (var game in Events.SelectMany(e => e.Games))
        {
            game.Name.Should().NotBeNullOrWhiteSpace();
            game.Description.Should().NotBeNullOrWhiteSpace();
            game.Objectives.Should().HaveCountGreaterThanOrEqualTo(6).And.HaveCountLessThanOrEqualTo(12, game.Name);
            game.Objectives.Select(o => o.Category).Distinct().Should().HaveCountGreaterThanOrEqualTo(2, game.Name);
            game.Objectives.Should().OnlyContain(o => o.Score >= 10 && o.Score <= 100, game.Name);
            game.Objectives.Select(o => o.Name).Should().OnlyHaveUniqueItems(game.Name);
        }
    }

    [Fact]
    public void BuildingItTwiceGivesTheSameData()
    {
        SampleEventCatalog.Build().Should().BeEquivalentTo(SampleEventCatalog.Build(), o => o.WithStrictOrdering());
    }
}
