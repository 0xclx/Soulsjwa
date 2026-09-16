namespace Soulsjwa.Api.Features.Games.Definitions;

using Soulsjwa.Shared;

/// <summary>
/// Hardcoded predefined objectives for connector-supported games: WHAT counts as
/// completion (name, score, rule). The connector-side data (offset, dataType)
/// lives in <see cref="GameDataDefinitions"/>, not here. Bump
/// <c>ConnectorConstants.Version</c> when these change.
/// </summary>
public static class PredefinedObjectives
{
    public const int RegularBossScore = 1;

    public const int RemembranceBossScore = 10;

    public static IReadOnlyList<PredefinedObjectiveDefinition> GetAll() =>
        [
            .. EldenRingMemoryObjectives(),
            .. DarkSouls1RemasteredObjectives(),
            .. DarkSouls2ScholarObjectives(),
            .. DarkSouls3Objectives(),
            .. SekiroObjectives(),
        ];

    public static IReadOnlyList<PredefinedObjectiveDefinition> ForGame(int gameId) =>
        GetAll().Where(o => o.GameId == gameId).ToList();

    /// <summary>
    /// FlagIds of the remembrance-dropping ("main") Elden Ring bosses, scored at
    /// <see cref="RemembranceBossScore"/>. Hand-curated, then re-anchored to
    /// <see cref="SoulMemoryCatalogData.EldenRingBosses"/>'s FlagIds — now the
    /// sole source for Elden Ring boss data.
    /// </summary>
    private static readonly HashSet<uint> EldenRingRemembranceBossFlagIds =
    [
        10000800, // Godrick the Grafted
        11000800, // Morgott, the Omen King
        11050800, // Hoarah Loux, Warrior
        12030850, // Lichdragon Fortissax
        12040800, // Astel, Naturalborn of the Void
        12050800, // Mohg, Lord of Blood
        12090800, // Regal Ancestor Spirit
        13000800, // Maliketh, the Black Blade
        13000830, // Dragonlord Placidusax
        14000800, // Rennala, Queen of the Full Moon
        15000800, // Malenia, Blade of Miquella
        16000800, // Rykard, Lord of Blasphemy
        19000800, // Elden Beast
        20000800, // Divine Beast Dancing Lion
        20010800, // Promised Consort Radahn
        21010800, // Messmer the Impaler
        22000800, // Putrescent Knight
        25000800, // Metyr, Mother of Fingers
        28000800, // Midra, Lord of Frenzied Flame
        1052520800, // Fire Giant (SoulMemory: "Mountaintops of the Giants" — the curated table's old "Flame Peak" area was wrong)
        1252380800, // Starscourge Radahn
        2044450800, // Romina, Saint of the Bud
        2048440800, // Rellana, Twin Moon Knight
        2049480800, // Commander Gaius
        2050480800, // Scadutree Avatar
        2054390800, // Bayle, the Dread
    ];

    /// <summary>
    /// A few <see cref="SoulMemoryCatalogData.EldenRingBosses"/> entries carry
    /// their parent region as <c>Group</c> rather than the dungeon they are
    /// actually fought in — e.g. Putrescent Knight's is "Cerulean Coast" though
    /// the fight, and every grace in it, is "Stone Coffin Fissure". Corrected
    /// here rather than in the generated catalog file.
    /// </summary>
    private static readonly Dictionary<uint, string> EldenRingBossCategoryOverrides = new()
    {
        [22000800] = "Stone Coffin Fissure", // Putrescent Knight
        [28000800] = "Midra's Manse", // Midra, Lord of Frenzied Flame
        [2049410800] = "Foot of the Jagged Peak", // Jagged Peak Drake
        [2044470800] = "Rauh Base", // Rugalea the Great Red Bear
    };

    private static IEnumerable<PredefinedObjectiveDefinition> EldenRingMemoryObjectives()
    {
        const int gameId = GameIds.EldenRingMemory;

        // A handful of bosses share a FlagId with another named encounter
        // (e.g. either Crucible Knight in a duo fight sets the same flag) —
        // keep one objective per underlying flag, same as every other
        // event-flag-derived list in this file.
        foreach (var boss in SoulMemoryCatalogData.EldenRingBosses
                     .GroupBy(b => b.Id)
                     .Select(group => group.First()))
        {
            var id = GameDataDefinitions.SoulMemoryFlagId(gameId, boss.Id);
            var isRemembrance = EldenRingRemembranceBossFlagIds.Contains(boss.Id);
            var score = isRemembrance ? RemembranceBossScore : RegularBossScore;
            // Every objective carries a category so the overlay can render
            // Game → Category → Objective; for Elden Ring that's the in-game area.
            var category = EldenRingBossCategoryOverrides.TryGetValue(boss.Id, out var overriddenCategory)
                ? overriddenCategory
                : CategoryOrFallback(boss, "bosses");
            yield return new PredefinedObjectiveDefinition(
                GameId: gameId,
                Name: $"{boss.Name} - Slain",
                Score: score,
                Rule: $$"""{">":[{"var":"{{id}}"},0]}""",
                Category: category,
                IsRemembrance: isRemembrance);
        }

        foreach (var grace in SoulMemoryCatalogData.EldenRingGraces)
        {
            yield return FlagObjective(
                gameId,
                GameDataDefinitions.SoulMemoryCatalogId(gameId, SoulMemoryCatalogKinds.Grace, grace.Member),
                $"{grace.Name} - Grace Discovered",
                RegularBossScore,
                CategoryOrFallback(grace, "graces"));
        }

        foreach (var state in SoulMemoryCatalogData.EldenRingKnownFlags)
        {
            yield return FlagObjective(
                gameId,
                GameDataDefinitions.SoulMemoryCatalogId(gameId, SoulMemoryCatalogKinds.Event, state.Member),
                $"{state.Name} - Achieved",
                RegularBossScore,
                CategoryOrFallback(state, "progression"));
        }
    }

    private static IEnumerable<PredefinedObjectiveDefinition> DarkSouls1RemasteredObjectives()
    {
        foreach (var objective in BuildEventFlagObjectives(
            GameIds.DarkSouls1Remastered,
            DarkSouls1RemasteredBossData.All.Select(e => (e.Id, e.Name, e.Category, e.IsMainBoss))))
            yield return objective;

        foreach (var state in SoulMemoryCatalogData.DarkSouls1KnownFlags)
            yield return FlagObjective(
                GameIds.DarkSouls1Remastered,
                GameDataDefinitions.SoulMemoryCatalogId(GameIds.DarkSouls1Remastered, SoulMemoryCatalogKinds.Event, state.Member),
                $"{state.Name} - Achieved",
                RegularBossScore,
                CategoryOrFallback(state, "progression"));

        foreach (var bonfire in SoulMemoryCatalogData.DarkSouls1Bonfires)
            yield return new PredefinedObjectiveDefinition(
                GameIds.DarkSouls1Remastered,
                $"{bonfire.Name} - Bonfire Discovered",
                RegularBossScore,
                $$"""{">=":[{"var":"{{GameDataDefinitions.SoulMemoryCatalogId(GameIds.DarkSouls1Remastered, SoulMemoryCatalogKinds.Bonfire, bonfire.Member)}}"},0]}""",
                CategoryOrFallback(bonfire, "bonfires"));
    }

    private static IEnumerable<PredefinedObjectiveDefinition> DarkSouls3Objectives()
    {
        foreach (var objective in BuildEventFlagObjectives(
            GameIds.DarkSouls3,
            DarkSouls3BossData.All.Select(e => (e.Id, e.Name, e.Category, e.IsMainBoss))))
            yield return objective;

        foreach (var bonfire in SoulMemoryCatalogData.DarkSouls3Bonfires)
            yield return FlagObjective(
                GameIds.DarkSouls3,
                GameDataDefinitions.SoulMemoryCatalogId(GameIds.DarkSouls3, SoulMemoryCatalogKinds.Bonfire, bonfire.Member),
                $"{bonfire.Name} - Bonfire Lit",
                RegularBossScore,
                CategoryOrFallback(bonfire, "bonfires"));
    }

    private static IEnumerable<PredefinedObjectiveDefinition> SekiroObjectives()
    {
        foreach (var objective in BuildEventFlagObjectives(
            GameIds.Sekiro,
            SekiroBossData.All.Select(e => (e.Id, e.Name, e.Category, e.IsMainBoss))))
            yield return objective;

        foreach (var idol in SoulMemoryCatalogData.SekiroIdols)
            yield return FlagObjective(
                GameIds.Sekiro,
                GameDataDefinitions.SoulMemoryCatalogId(GameIds.Sekiro, SoulMemoryCatalogKinds.Idol, idol.Member),
                $"{idol.Name} - Idol Discovered",
                RegularBossScore,
                CategoryOrFallback(idol, "idols"));
    }

    private static IEnumerable<PredefinedObjectiveDefinition> DarkSouls2ScholarObjectives()
    {
        const int gameId = GameIds.DarkSouls2Scholar;
        foreach (var boss in DarkSouls2ScholarBossData.All)
        {
            var id = GameDataDefinitions.SoulMemoryFlagId(gameId, boss.Id);
            var score = boss.IsMainBoss ? RemembranceBossScore : RegularBossScore;
            yield return new PredefinedObjectiveDefinition(
                GameId: gameId,
                Name: $"{boss.Name} - Slain",
                Score: score,
                // GetBossKillCount returns kills across NG cycles; > 0 == ever killed.
                Rule: $$"""{">":[{"var":"{{id}}"},0]}""",
                Category: boss.Category,
                IsRemembrance: boss.IsMainBoss);
        }
    }

    private static IEnumerable<PredefinedObjectiveDefinition> BuildEventFlagObjectives(
        int gameId, IEnumerable<(long Id, string Name, string Category, bool IsMainBoss)> bosses)
    {
        foreach (var b in bosses)
        {
            var id = GameDataDefinitions.SoulMemoryFlagId(gameId, b.Id);
            var score = b.IsMainBoss ? RemembranceBossScore : RegularBossScore;
            yield return new PredefinedObjectiveDefinition(
                GameId: gameId,
                Name: $"{b.Name} - Slain",
                Score: score,
                Rule: $$"""{">":[{"var":"{{id}}"},0]}""",
                Category: b.Category,
                IsRemembrance: b.IsMainBoss);
        }
    }

    private static PredefinedObjectiveDefinition FlagObjective(
        int gameId,
        string dataPointId,
        string name,
        int score,
        string category) =>
        new(
            gameId,
            name,
            score,
            $$"""{">":[{"var":"{{dataPointId}}"},0]}""",
            category);

    /// <summary>
    /// The upstream catalog leaves <c>Group</c> empty for entries with no in-game
    /// location (inventory-style ones), hence the fallback.
    /// </summary>
    private static string CategoryOrFallback(SoulMemoryCatalogData.CatalogEntry entry, string fallback) =>
        string.IsNullOrEmpty(entry.Group) ? fallback : entry.Group;
}

/// <param name="Category">Free-form grouping label for UI filtering — an in-game location for boss objectives, or a bucket like "bonfires"/"graces"/"progression" for the rest.</param>
public sealed record PredefinedObjectiveDefinition(
    int GameId,
    string Name,
    int Score,
    string Rule,
    string? Category = null,
    bool IsRemembrance = false,
    string? FailRule = null);
