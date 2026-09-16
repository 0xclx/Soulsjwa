using FluentAssertions;
using Soulsjwa.Api.Features.Connector;
using Soulsjwa.Api.Features.Games.Definitions;
using Soulsjwa.Shared;
using Xunit;

namespace Soulsjwa.UnitTests;

public class PredefinedObjectivesConsistencyTests
{
    [Fact]
    public void AllObjectives_HavePositiveScores()
    {
        var objectives = PredefinedObjectives.GetAll();
        objectives.Should().AllSatisfy(o => o.Score.Should().BeGreaterThan(0));
    }

    [Fact]
    public void AllObjectives_HaveNonEmptyNames()
    {
        var objectives = PredefinedObjectives.GetAll();
        objectives.Should().AllSatisfy(o => o.Name.Should().NotBeNullOrWhiteSpace());
    }

    [Fact]
    public void AllObjectives_HaveValidJsonRules()
    {
        var objectives = PredefinedObjectives.GetAll();
        objectives.Should().AllSatisfy(o =>
        {
            o.Rule.Should().NotBeNullOrWhiteSpace();
            o.Rule.Should().StartWith("{");
            o.Rule.Should().EndWith("}");
        });
    }

    [Fact]
    public void AllObjectives_HaveUniqueNames()
    {
        // Names only need to be unique within a single game.
        var objectives = PredefinedObjectives.GetAll();
        foreach (var group in objectives.GroupBy(o => o.GameId))
        {
            var names = group.Select(o => o.Name).ToList();
            names.Should().OnlyHaveUniqueItems(
                $"Game {group.Key} should have unique objective names");
        }
    }

    [Fact]
    public void AllObjectives_HaveACategory()
    {
        // Every objective must carry a grouping category so the OBS overlay can
        // render Game → Category → Objective for all games, not just Elden Ring.
        var objectives = PredefinedObjectives.GetAll();
        objectives.Should().AllSatisfy(o =>
            o.Category.Should().NotBeNullOrWhiteSpace(
                $"Objective '{o.Name}' (game {o.GameId}) should have a category"));
    }

    [Fact]
    public void NoObjective_UsesPlaceholderMainOrRegularCategory()
    {
        // DS1R, DS2 SOTFS, DS3 and Sekiro used to categorise boss objectives as
        // "main"/"regular". They now carry the boss's in-game location instead;
        // this must never regress for any game.
        var objectives = PredefinedObjectives.GetAll();
        objectives.Should().AllSatisfy(o =>
        {
            (o.Category is "main" or "regular").Should().BeFalse(
                $"Objective '{o.Name}' (game {o.GameId}) should carry an in-game location, not a placeholder category");
        });
    }

    [Fact]
    public void EldenRingObjectives_AreCategorisedByArea()
    {
        // Elden Ring groups bosses by in-game area (never the grace/known-
        // event objectives' own "graces"/"progression" fallback buckets),
        // and every area name is a real, non-blank string. Doesn't require
        // every grace/known-event sub-area to also have its own boss, which
        // isn't true of the game world: many named grace locations sit
        // inside a larger zone whose only boss is grouped under that zone's
        // broader name (e.g. "Specimen Storehouse" has graces but its boss
        // is categorised under the parent "Shadow Keep").
        var bossObjectiveNames = SoulMemoryCatalogData.EldenRingBosses
            .Select(b => $"{b.Name} - Slain")
            .ToHashSet();
        var bossObjectives = PredefinedObjectives.ForGame(GameIds.EldenRingMemory)
            .Where(o => bossObjectiveNames.Contains(o.Name));
        bossObjectives.Should().AllSatisfy(o =>
        {
            o.Category.Should().NotBeNullOrWhiteSpace();
            (o.Category is "graces" or "progression").Should().BeFalse();
        });
    }

    [Fact]
    public void ForGame_ReturnsEmptyForUnsupportedGame()
    {
        PredefinedObjectives.ForGame(999).Should().BeEmpty();
    }

    [Fact]
    public void EldenRing_CoversEveryCuratedBoss()
    {
        var objectives = PredefinedObjectives.ForGame(GameIds.EldenRingMemory);
        // One objective per unique boss FlagId in SoulMemory's Elden Ring
        // boss catalog (211 entries; a handful of duo-fight bosses share a
        // FlagId, deduped to one objective per flag), plus extra
        // graces/known-events not in that catalog.
        var uniqueBosses = SoulMemoryCatalogData.EldenRingBosses
            .GroupBy(b => b.Id)
            .Select(group => group.First())
            .ToList();
        objectives.Should().HaveCountGreaterThanOrEqualTo(uniqueBosses.Count);
        foreach (var boss in uniqueBosses)
            objectives.Should().Contain(o => o.Name == $"{boss.Name} - Slain");
    }

    [Fact]
    public void AllObjectives_BelongToSupportedGames()
    {
        var objectives = PredefinedObjectives.GetAll();
        var supportedGameIds = GameDataDefinitions.SupportedGameIds;

        objectives.Should().AllSatisfy(o =>
            supportedGameIds.Should().Contain(o.GameId,
                $"Objective '{o.Name}' belongs to unsupported game {o.GameId}"));
    }

    [Fact]
    public void DataPoints_HaveUniqueIds()
    {
        foreach (var gameId in GameDataDefinitions.SupportedGameIds)
        {
            var dataPoints = GameDataDefinitions.ForGame(gameId);
            var ids = dataPoints.Select(dp => dp.Id).ToList();
            ids.Should().OnlyHaveUniqueItems($"Game {gameId} should have unique data point IDs");
        }
    }

    [Fact]
    public void DataPoints_HaveNonEmptySourceIds()
    {
        foreach (var gameId in GameDataDefinitions.SupportedGameIds)
        {
            var dataPoints = GameDataDefinitions.ForGame(gameId);
            dataPoints.Should().AllSatisfy(dp =>
                dp.SourceId.Should().NotBeNullOrWhiteSpace(
                    $"Game {gameId} data point '{dp.Id}' must identify its upstream source"));
        }
    }

    [Fact]
    public void SoulMemoryCatalogs_MatchPinnedUpstreamCounts()
    {
        SoulMemoryCatalogData.DarkSouls1Bonfires.Should().HaveCount(43);
        SoulMemoryCatalogData.DarkSouls1KnownFlags.Should().HaveCount(52);
        SoulMemoryCatalogData.DarkSouls3Bonfires.Should().HaveCount(77);
        SoulMemoryCatalogData.DarkSouls3ItemPickups.Should().HaveCount(1144);
        SoulMemoryCatalogData.SekiroIdols.Should().HaveCount(55);
        SoulMemoryCatalogData.EldenRingBosses.Should().HaveCount(211);
        SoulMemoryCatalogData.EldenRingGraces.Should().HaveCount(419);
        SoulMemoryCatalogData.EldenRingKnownFlags.Should().HaveCount(50);
        SoulMemoryCatalogData.EldenRingItemPickups.Should().HaveCount(4209);
        SoulMemoryCatalogData.EldenRingInventory.Should().HaveCount(2652);
    }

    [Fact]
    public void ExpandedSoulMemoryDataPoints_ExposeEveryCatalogEntry()
    {
        GameDataDefinitions.ForGame(GameIds.DarkSouls1Remastered)
            .Count(dp => dp.ReaderCapability == GameDataReaderCapability.MemoryLocationState)
            .Should().Be(SoulMemoryCatalogData.DarkSouls1Bonfires.Count);
        GameDataDefinitions.ForGame(GameIds.DarkSouls3)
            .Count(dp => dp.Id.StartsWith("g3_pickup_", StringComparison.Ordinal))
            .Should().Be(SoulMemoryCatalogData.DarkSouls3ItemPickups.Count);
        GameDataDefinitions.ForGame(GameIds.Sekiro)
            .Count(dp => dp.Id.StartsWith("g6_idol_", StringComparison.Ordinal))
            .Should().Be(SoulMemoryCatalogData.SekiroIdols.Count);

        var eldenRing = GameDataDefinitions.ForGame(GameIds.EldenRingMemory);
        eldenRing.Count(dp => dp.Id.StartsWith("g9_grace_", StringComparison.Ordinal))
            .Should().Be(SoulMemoryCatalogData.EldenRingGraces.Count);
        eldenRing.Count(dp => dp.Id.StartsWith("g9_event_", StringComparison.Ordinal))
            .Should().Be(SoulMemoryCatalogData.EldenRingKnownFlags.Count);
        eldenRing.Count(dp => dp.Id.StartsWith("g9_pickup_", StringComparison.Ordinal))
            .Should().Be(SoulMemoryCatalogData.EldenRingItemPickups.Count);
        eldenRing.Count(dp => dp.ReaderCapability == GameDataReaderCapability.MemoryEldenRingInventoryItemPresence)
            .Should().Be(SoulMemoryCatalogData.EldenRingInventory.Count);
    }

    [Fact]
    public void ConnectorSubmissionValidator_AcceptsFinitePositionDecimals()
    {
        var positionId = GameDataDefinitions.ForGame(GameIds.EldenRingMemory)
            .Single(dp => dp.ReaderCapability == GameDataReaderCapability.MemoryPositionComponent
                          && dp.SourceId == nameof(MemoryPositionComponentSource.X))
            .Id;

        ConnectorSubmissionValidator.Validate(
                $$"""{"{{positionId}}":123.5}""",
                GameIds.EldenRingMemory)
            .Should().BeEmpty();
    }

    [Fact]
    public void DataPoints_HaveValidNumericOffsets()
    {
        foreach (var gameId in GameDataDefinitions.SupportedGameIds)
        {
            var dataPoints = GameDataDefinitions.ForGame(gameId);
            // Only event_flag/boss_kill_count offsets are raw numeric ids
            // (decimal SoulMemory flag/BossType ids). Every other data type
            // intentionally repurposes Offset as a semantic string key
            // (attribute enum member names, "{ItemCategory}:{ItemId}" for
            // inventory items, singleton ids like "ng_count"/"player_health"/"ng_level").
            dataPoints.Where(dp => dp.DataType is "event_flag" or "boss_kill_count").Should().AllSatisfy(dp =>
            {
                var offset = dp.Offset;
                var parsed = offset.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                    ? long.TryParse(offset[2..], System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var hex) && hex >= 0
                    : long.TryParse(offset, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var dec) && dec >= 0;
                parsed.Should().BeTrue($"Data point '{dp.DisplayName}' offset '{offset}' should be a valid non-negative number");
            });
        }
    }

    [Fact]
    public void EveryObjectiveRule_ReferencesExistingDataPoint()
    {
        foreach (var gameId in GameDataDefinitions.SupportedGameIds)
        {
            var dataPoints = GameDataDefinitions.ForGame(gameId);
            var dataPointIds = dataPoints.Select(dp => dp.Id).ToHashSet();
            var objectives = PredefinedObjectives.ForGame(gameId);

            foreach (var obj in objectives)
            {
                var referencesDataPoint = dataPointIds.Any(id => obj.Rule.Contains($"\"{id}\""));
                referencesDataPoint.Should().BeTrue(
                    $"Objective '{obj.Name}' rule should reference a data point ID from game {gameId}");
            }
        }
    }

    [Fact]
    public void ConnectorVersion_IsValidSemver()
    {
        var version = ConnectorConstants.Version;
        var parts = version.Split('.');
        parts.Should().HaveCount(3);
        parts.Should().AllSatisfy(p => int.TryParse(p, out _).Should().BeTrue());
    }

    [Fact]
    public void SupportedGameIds_IsNotEmpty()
    {
        GameDataDefinitions.SupportedGameIds.Should().NotBeEmpty();
    }

    [Fact]
    public void EveryBossDataPoint_IsReferencedByAtLeastOneObjective()
    {
        foreach (var gameId in GameDataDefinitions.SupportedGameIds)
        {
            var dataPoints = GameDataDefinitions.ForGame(gameId);
            var objectives = PredefinedObjectives.ForGame(gameId);

            // Only boss data points (event_flag / boss_kill_count) are backed
            // by a curated boss table and therefore always get a matching
            // predefined objective. Attributes, NG counters, player health,
            // and inventory item quantities are catalogable read capabilities
            // meant for user-authored custom rules — they intentionally have
            // no corresponding predefined objective.
            foreach (var dp in dataPoints.Where(dp => dp.Category == GameDataCategory.Bosses))
            {
                var isReferenced = objectives.Any(o => o.Rule.Contains($"\"{dp.Id}\""));
                isReferenced.Should().BeTrue(
                    $"Boss data point '{dp.DisplayName}' (ID {dp.Id}) in game {gameId} should be referenced by at least one objective");
            }
        }
    }
}
