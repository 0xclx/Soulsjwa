using Soulsjwa.Api.Features.Events.Entities;

namespace Soulsjwa.Api.Features.Admin.SampleEvents;

internal sealed record SampleObjectiveDefinition(string Name, int Score, string Category);

internal sealed record SampleGameDefinition(
    string Name,
    string Description,
    IReadOnlyList<SampleObjectiveDefinition> Objectives);

internal sealed record SampleEventDefinition(
    string Name,
    string Description,
    TieBreakMode TieBreakMode,
    /// <summary>How long before "now" the event started; null = not started.</summary>
    TimeSpan? StartedAgo,
    /// <summary>Index into <see cref="Games"/> of the enabled game; null = none.</summary>
    int? EnabledGameIndex,
    string? RulesMarkdown,
    IReadOnlyList<SampleGameDefinition> Games);

/// <summary>
/// The fixed content of the admin "Create sample events" action: five events,
/// each in a different state, for testing the UI. Pure data — the same on
/// every call, no randomness — so the seeder, its tests and the admin page all
/// describe exactly the same thing. Custom games only, so nothing depends on
/// the game catalog; no competitors, which are added by hand.
/// </summary>
internal static class SampleEventCatalog
{
    public const string NamePrefix = "Sample: ";

    public static IReadOnlyList<SampleEventDefinition> Build()
    {
        return
        [
            new(
                NamePrefix + "Not started",
                "Games and objectives are set up; add competitors and start it to try the scoreboard.",
                TieBreakMode.SharedPlace,
                StartedAgo: null,
                EnabledGameIndex: null,
                RulesMarkdown: null,
                [DarkSouls(), Bloodborne(), EldenRing()]),
            new(
                NamePrefix + "Game in progress",
                "Started, with the second game enabled — the current game view.",
                TieBreakMode.SharedPlace,
                StartedAgo: TimeSpan.FromHours(2),
                EnabledGameIndex: 1,
                RulesMarkdown:
                    "## Rules\n\n" +
                    "- Games are played in order; only the enabled game scores.\n" +
                    "- A death ends the run for that game: fail the remaining objectives.\n" +
                    "- Ties share a place.",
                [DarkSoulsIii(), Sekiro(), DarkSouls()]),
            new(
                NamePrefix + "Between games",
                "Started, with no game enabled — only the whole event view.",
                TieBreakMode.ByTime,
                StartedAgo: TimeSpan.FromDays(1),
                EnabledGameIndex: null,
                RulesMarkdown: null,
                [DemonsSouls(), DarkSoulsIi(), Bloodborne()]),
            new(
                NamePrefix + "Many games",
                "Started, seven games, the first enabled — a wide whole event table.",
                TieBreakMode.ByTime,
                StartedAgo: TimeSpan.FromHours(3),
                EnabledGameIndex: 0,
                RulesMarkdown: null,
                [DemonsSouls(), DarkSouls(), DarkSoulsIi(), DarkSoulsIii(), Bloodborne(), Sekiro(), EldenRing()]),
            new(
                NamePrefix + "Empty setup",
                "No games and no competitors — the empty states.",
                TieBreakMode.SharedPlace,
                StartedAgo: null,
                EnabledGameIndex: null,
                RulesMarkdown: null,
                []),
        ];
    }

    private static SampleObjectiveDefinition O(string name, int score, string category) => new(name, score, category);

    private static SampleGameDefinition DemonsSouls() => new(
        "Demon's Souls",
        "Boleteria and its archstones.",
        [
            O("Phalanx", 10, "Boletarian Palace"),
            O("Tower Knight", 20, "Boletarian Palace"),
            O("Penetrator", 30, "Boletarian Palace"),
            O("Armor Spider", 20, "Stonefang Tunnel"),
            O("Flamelurker", 40, "Stonefang Tunnel"),
            O("Dragon God", 50, "Stonefang Tunnel"),
            O("Maneater", 60, "Shrine of Storms"),
            O("Old Hero", 40, "Shrine of Storms"),
        ]);

    private static SampleGameDefinition DarkSouls() => new(
        "Dark Souls",
        "Lordran, from the Asylum to the Kiln.",
        [
            O("Asylum Demon", 10, "Undead Asylum"),
            O("Taurus Demon", 20, "Undead Burg"),
            O("Bell Gargoyles", 30, "Undead Parish"),
            O("Capra Demon", 40, "Lower Undead Burg"),
            O("Ornstein and Smough", 80, "Anor Londo"),
            O("Dark Sun Gwyndolin", 60, "Anor Londo"),
            O("Seath the Scaleless", 70, "The Duke's Archives"),
            O("Gwyn, Lord of Cinder", 100, "Kiln of the First Flame"),
        ]);

    private static SampleGameDefinition DarkSoulsIi() => new(
        "Dark Souls II",
        "Drangleic and the four Great Souls.",
        [
            O("The Last Giant", 10, "Forest of Fallen Giants"),
            O("The Pursuer", 20, "Forest of Fallen Giants"),
            O("Dragonrider", 20, "Heide's Tower of Flame"),
            O("Old Dragonslayer", 30, "Heide's Tower of Flame"),
            O("The Rotten", 50, "Black Gulch"),
            O("Lost Sinner", 60, "Sinners' Rise"),
            O("Old Iron King", 60, "Iron Keep"),
        ]);

    private static SampleGameDefinition DarkSoulsIii() => new(
        "Dark Souls III",
        "Lothric and the Lords of Cinder.",
        [
            O("Iudex Gundyr", 10, "Cemetery of Ash"),
            O("Vordt of the Boreal Valley", 20, "High Wall of Lothric"),
            O("Dancer of the Boreal Valley", 60, "High Wall of Lothric"),
            O("Abyss Watchers", 50, "Farron Keep"),
            O("Pontiff Sulyvahn", 70, "Irithyll of the Boreal Valley"),
            O("Aldrich, Devourer of Gods", 60, "Anor Londo"),
            O("Nameless King", 90, "Archdragon Peak"),
            O("Soul of Cinder", 100, "Kiln of the First Flame"),
        ]);

    private static SampleGameDefinition Bloodborne() => new(
        "Bloodborne",
        "Yharnam, on the night of the Hunt.",
        [
            O("Cleric Beast", 10, "Central Yharnam"),
            O("Father Gascoigne", 30, "Central Yharnam"),
            O("Blood-starved Beast", 40, "Old Yharnam"),
            O("Vicar Amelia", 40, "Cathedral Ward"),
            O("Darkbeast Paarl", 50, "Hemwick and Castle Cainhurst"),
            O("Martyr Logarius", 70, "Hemwick and Castle Cainhurst"),
            O("Gehrman, the First Hunter", 100, "Hunter's Dream"),
        ]);

    private static SampleGameDefinition Sekiro() => new(
        "Sekiro: Shadows Die Twice",
        "Ashina, and a promise to the Divine Heir.",
        [
            O("Gyoubu Oniwa", 20, "Ashina Outskirts"),
            O("Lady Butterfly", 30, "Hirata Estate"),
            O("Genichiro Ashina", 50, "Ashina Castle"),
            O("Guardian Ape", 50, "Sunken Valley"),
            O("Corrupted Monk", 60, "Mibu Village"),
            O("Great Shinobi Owl", 80, "Ashina Castle"),
            O("Sword Saint Isshin", 100, "Ashina Castle"),
        ]);

    private static SampleGameDefinition EldenRing() => new(
        "Elden Ring",
        "The Lands Between, shard by shard.",
        [
            O("Margit, the Fell Omen", 20, "Limgrave"),
            O("Godrick the Grafted", 30, "Limgrave"),
            O("Rennala, Queen of the Full Moon", 40, "Liurnia of the Lakes"),
            O("Starscourge Radahn", 70, "Caelid"),
            O("Morgott, the Omen King", 60, "Leyndell, Royal Capital"),
            O("Maliketh, the Black Blade", 80, "Crumbling Farum Azula"),
            O("Radagon of the Golden Order", 90, "Leyndell, Ashen Capital"),
            O("Elden Beast", 100, "Leyndell, Ashen Capital"),
        ]);
}
