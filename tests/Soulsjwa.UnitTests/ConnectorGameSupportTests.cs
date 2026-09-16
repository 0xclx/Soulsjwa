using FluentAssertions;
using Soulsjwa.Api.Features.Events.Entities;
using Soulsjwa.Api.Features.Games.Definitions;
using Soulsjwa.Api.Features.Games.Entities;
using Soulsjwa.Api.Features.Games.Services;
using Soulsjwa.Shared;
using Xunit;

namespace Soulsjwa.UnitTests;

public class ConnectorGameSupportTests
{
    [Fact]
    public void ConnectorVersion_Is_Defined()
    {
        ConnectorConstants.Version.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void PredefinedObjectives_Contains_EldenRing_Bosses()
    {
        var eldenRingObjectives = PredefinedObjectives.ForGame(GameIds.EldenRingMemory);
        eldenRingObjectives.Should().NotBeEmpty();
        eldenRingObjectives.Should().Contain(o => o.Name.Contains("Malenia"));
        eldenRingObjectives.Should().Contain(o => o.Name.Contains("Margit"));
        eldenRingObjectives.Should().Contain(o => o.Name.Contains("Godrick"));
    }

    [Fact]
    public void PredefinedObjectives_Have_Rules_But_No_Offset()
    {
        var objectives = PredefinedObjectives.GetAll();
        foreach (var obj in objectives)
        {
            obj.Rule.Should().NotBeNullOrWhiteSpace();
            // Objectives should NOT contain offset/dataType — those are in GameDataDefinitions
            obj.Rule.Should().NotContain("offset");
        }
    }

    [Fact]
    public void GameDataDefinitions_Contains_EldenRing_DataPoints()
    {
        var dataPoints = GameDataDefinitions.ForGame(GameIds.EldenRingMemory);
        dataPoints.Should().NotBeEmpty();
        var malenia = GameDataDefinitions.SoulMemoryFlagId(GameIds.EldenRingMemory, 15000800);
        var margit = GameDataDefinitions.SoulMemoryFlagId(GameIds.EldenRingMemory, 10000850);
        var godrick = GameDataDefinitions.SoulMemoryFlagId(GameIds.EldenRingMemory, 10000800);
        dataPoints.Should().Contain(dp => dp.Id == malenia && dp.DisplayName.Contains("Malenia"));
        dataPoints.Should().Contain(dp => dp.Id == margit && dp.DisplayName.Contains("Margit"));
        dataPoints.Should().Contain(dp => dp.Id == godrick && dp.DisplayName.Contains("Godrick"));
    }

    [Fact]
    public void GameDataDefinitions_Contains_GameTime()
    {
        var dataPoints = GameDataDefinitions.ForGame(GameIds.EldenRingMemory);
        var gameTimeId = GameDataDefinitions.SoulMemoryGameTimeId(GameIds.EldenRingMemory);
        dataPoints.Should().Contain(dp => dp.Id == gameTimeId);
    }

    [Fact]
    public void PredefinedObjectives_Cover_All_Bosses_With_Correct_Scores()
    {
        // 1 point per regular boss kill, 10 points per remembrance.
        var objectives = PredefinedObjectives.ForGame(GameIds.EldenRingMemory);
        objectives.Where(o => o.IsRemembrance).Should().AllSatisfy(o =>
            o.Score.Should().Be(PredefinedObjectives.RemembranceBossScore));
        objectives.Where(o => !o.IsRemembrance).Should().AllSatisfy(o =>
            o.Score.Should().Be(PredefinedObjectives.RegularBossScore));
        objectives.Should().Contain(o => o.Name.StartsWith("Malenia") && o.IsRemembrance && o.Score == 10);
        objectives.Should().Contain(o => o.Name.StartsWith("Soldier of Godrick") && !o.IsRemembrance && o.Score == 1);
    }

    [Fact]
    public void GameDataDefinitions_DataPoints_Have_Offset_And_DataType()
    {
        var dataPoints = GameDataDefinitions.ForGame(GameIds.EldenRingMemory);
        dataPoints.Should().AllSatisfy(dp =>
        {
            dp.Offset.Should().NotBeNullOrWhiteSpace();
            dp.DataType.Should().NotBeNullOrWhiteSpace();
            dp.Id.Should().NotBeNullOrWhiteSpace();
            dp.DisplayName.Should().NotBeNullOrWhiteSpace();
        });
    }

    [Fact]
    public void GameDataDefinitions_Returns_Empty_For_Unsupported_Game()
    {
        var dataPoints = GameDataDefinitions.ForGame(999);
        dataPoints.Should().BeEmpty();
    }

    [Fact]
    public void GameDataDefinitions_SupportedGameIds_Contains_EldenRing()
    {
        GameDataDefinitions.SupportedGameIds.Should().Contain(GameIds.EldenRingMemory);
    }

    [Fact]
    public void RuleEvaluator_Returns_True_When_Rule_Satisfied()
    {
        var rule = """{">":[{"var":"0xE4E1080"},0]}""";
        var data = """{"0xE4E1080":1}""";
        RuleEvaluator.Evaluate(rule, data).Should().BeTrue();
    }

    [Fact]
    public void RuleEvaluator_Returns_False_When_Rule_Not_Satisfied()
    {
        var rule = """{">":[{"var":"0xE4E1080"},0]}""";
        var data = """{"0xE4E1080":0}""";
        RuleEvaluator.Evaluate(rule, data).Should().BeFalse();
    }

    [Fact]
    public void RuleEvaluator_Returns_False_On_Invalid_Json()
    {
        RuleEvaluator.Evaluate("invalid", "{}").Should().BeFalse();
    }

    [Fact]
    public void RuleEvaluator_Supports_And_Logic()
    {
        // Malenia slain AND game time < 30 hours (108000 seconds)
        var rule = """{"and":[{">":[{"var":"0xE4E1080"},0]},{"<":[{"var":"game_time"},108000]}]}""";
        var dataPass = """{"0xE4E1080":1,"game_time":100000}""";
        var dataFail = """{"0xE4E1080":1,"game_time":200000}""";
        RuleEvaluator.Evaluate(rule, dataPass).Should().BeTrue();
        RuleEvaluator.Evaluate(rule, dataFail).Should().BeFalse();
    }

    [Fact]
    public void DataPoints_And_Objectives_Use_Same_Ids()
    {
        var dataPoints = GameDataDefinitions.ForGame(GameIds.EldenRingMemory);
        var dataPointIds = dataPoints.Select(dp => dp.Id).ToHashSet();
        var objectives = PredefinedObjectives.ForGame(GameIds.EldenRingMemory);

        foreach (var obj in objectives)
        {
            // Each rule uses {"var":"<id>"} — at least one data point ID should appear
            var matchesAny = dataPointIds.Any(id => obj.Rule.Contains($"\"{id}\""));
            matchesAny.Should().BeTrue(
                $"Objective '{obj.Name}' rule should reference a data point ID");
        }
    }

    [Fact]
    public void GameIds_Constants_Match_Seed_Json_Ids()
    {
        // Pure shared constants — the only way these can drift is if the seed
        // JSON ids change. Locking them down prevents silent breakage of every
        // connector lookup.
        GameIds.DarkSouls1Remastered.Should().Be(1);
        GameIds.DarkSouls2Scholar.Should().Be(2);
        GameIds.DarkSouls3.Should().Be(3);
        GameIds.Sekiro.Should().Be(6);
        GameIds.EldenRingMemory.Should().Be(9);
    }

    [Fact]
    public void Seed_Json_Game_Names_Match_Issue_Spec()
    {
        var seedPath = Path.Combine(AppContext.BaseDirectory, "Infrastructure", "Data", "Seed", "games.json");
        File.Exists(seedPath).Should().BeTrue($"seed JSON missing at {seedPath}");

        var games = System.Text.Json.JsonSerializer.Deserialize<List<Game>>(
            File.ReadAllText(seedPath),
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        games.Single(g => g.Id == GameIds.DarkSouls1Remastered).Name.Should().Be("Dark Souls: Remastered");
        games.Single(g => g.Id == GameIds.DarkSouls2Scholar).Name.Should().Be("Dark Souls II: Scholar of the First Sin");
        games.Single(g => g.Id == GameIds.EldenRingMemory).Name.Should().Be("Elden Ring");

        games.Single(g => g.Id == GameIds.EldenRingMemory).ConnectorSupported.Should().BeTrue();
        games.Should().OnlyContain(g => g.ConnectorSupported,
            "unsupported games (Demon's Souls, Bloodborne, Armored Core VI) and the ER savefile source were removed from the catalog");
    }
}
