"""Generates per-game boss-data C# files for SoulMemory-supported games.

Boss id / display name / "main boss" flag for each game is embedded below as
a frozen snapshot (BOSSES), taken from SoulMemory.dll's per-game boss enums
(SoulMemory.DarkSouls1.Boss, SoulMemory.DarkSouls2.BossType,
SoulMemory.DarkSouls3.Boss, SoulMemory.Sekiro.Boss) via the SoulMemory NuGet
package (GPL-3, FrankvdStam/SoulSplitter). This snapshot is intentionally
static — no NuGet package or ilspy/dnfile inspection is required to run this
script — because that identity data never changes: SoulMemory's enums are
stable per game version, and this project pins its game support to specific
titles, not a moving SoulMemory release.

What *is* curated and maintained here is LOCATIONS: a per-game, per-boss-id
table mapping each boss to the in-game area it is fought in. This is the
piece that replaced the placeholder "main"/"regular" categories. Every boss
id in BOSSES must have a LOCATIONS entry — the generator exits non-zero
otherwise, so a boss can never again regenerate with a placeholder category.
Locations the curator is not fully confident in are marked `uncertain=True`;
they still emit a real location (never a fallback), and are additionally
collected into tools/uncertain-boss-locations.md for later review.

Output: src/Soulsjwa.Api/Features/Games/Definitions/<Game>BossData.cs
"""

import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
OUT_DIR = REPO_ROOT / "src/Soulsjwa.Api/Features/Games/Definitions"
UNCERTAIN_REPORT = REPO_ROOT / "tools/uncertain-boss-locations.md"

# (Id, Name, IsMainBoss) — frozen snapshot per game, see module docstring.
BOSSES: dict[str, list[tuple[int, str, bool]]] = {
    "DarkSouls1Remastered": [
        (16, "Asylum Demon", False),
        (3, "Bell Gargoyles", False),
        (11010902, "Capra Demon", False),
        (11410900, "Ceaseless Discharge", False),
        (11410901, "Centipede Demon", False),
        (9, "Chaos Witch Quelaag", False),
        (4, "Crossbreed Priscilla", False),
        (11510900, "Dark Sun Gwyndolin", False),
        (11410410, "Demon Firesage", False),
        (13, "Four Kings", True),
        (2, "Gaping Dragon", False),
        (5, "Great Grey Wolf Sif", False),
        (15, "Gwyn Lord Of Cinder", True),
        (11, "Iron Golem", False),
        (11200900, "Moonlight Butterfly", False),
        (7, "Nito", True),
        (12, "Ornstein And Smough", True),
        (6, "Pinwheel", False),
        (14, "Seath The Scaleless", True),
        (11810900, "Stray Demon", False),
        (11010901, "Taurus Demon", False),
        (10, "Bed Of Chaos", True),
        (11210001, "Artorias The Abysswalker", True),
        (11210004, "Black Dragon Kalameet", True),
        (11210002, "Manus Father Of The Abyss", True),
        (11210000, "Sanctuary Guardian", False),
    ],
    "DarkSouls2Scholar": [
        (124, "The Last Giant", True),
        (112, "The Pursuer", False),
        (52, "Executioners Chariot", False),
        (72, "Looking Glass Knight", False),
        (56, "The Skeleton Lords", False),
        (84, "Flexile Sentry", False),
        (92, "Lost Sinner", True),
        (168, "Belfry Gargoyles", False),
        (88, "Ruin Sentinels", False),
        (100, "Royal Rat Vanguard", False),
        (108, "Royal Rat Authority", False),
        (68, "Scorpioness Najka", False),
        (44, "The Dukes Dear Freja", False),
        (64, "Mytha The Baneful Queen", False),
        (104, "The Rotten", True),
        (80, "Old Dragon Slayer", False),
        (60, "Covetous Demon", False),
        (96, "Smelter Demon", False),
        (48, "Old Iron King", True),
        (120, "Guardian Dragon", False),
        (40, "Demon Of Song", False),
        (140, "Velstadt The Royal Aegis", False),
        (152, "Vendrick", True),
        (156, "Darklurker", False),
        (76, "Dragonrider", False),
        (160, "Twin Dragonriders", False),
        (164, "Prowling Magnus And Congregation", False),
        (128, "Giant Lord", False),
        (148, "Ancient Dragon", False),
        (136, "Throne Watcher And Throne Defender", False),
        (132, "Nashandra", True),
        (280, "Aldia Scholar Of The First Sin", True),
        (200, "Elana Squalid Queen", False),
        (212, "Sinh The Slumbering Dragon", False),
        (244, "Afflicted Graverobber Ancient Soldier Varg Cerah The Old Explorer", False),
        (252, "Blue Smelter Demon", False),
        (204, "Fumeknight", True),
        (248, "Sir Alonne", True),
        (260, "Burnt Ivory King", True),
        (208, "Aava The Kings Pet", False),
        (264, "Lud And Zallen The Kings Pets", False),
    ],
    "DarkSouls3": [
        (14000800, "Iudex Gundyr", True),
        (13000800, "Vordt Of The Boreal Valley", False),
        (13100800, "Curse Rotted Greatwood", False),
        (13300850, "Crystal Sage", False),
        (13300800, "Abyss Watchers", False),
        (13500800, "Deacons Of The Deep", False),
        (13800800, "High Lord Wolnir", False),
        (13800830, "Old Demon King", False),
        (13700850, "Pontiff Sulyvahn", True),
        (13900800, "Yhorm The Giant", False),
        (13700800, "Aldrich Devourer Of Gods", True),
        (13000890, "Dancer Of The Boreal Valley", False),
        (13010800, "Dragonslayer Armour", False),
        (13000830, "Oceiros The Consumed King", False),
        (14000830, "Champion Gundyr", False),
        (13410830, "Lothric Younger Prince", False),
        (13200800, "Ancient Wyvern", False),
        (13200850, "Nameless King", True),
        (14100800, "Soul Of Cinder", True),
        (14500800, "Sister Friede", True),
        (14500860, "Champions Gravetender And Gravetender Greatwolf", False),
        (15000800, "Demon In Pain And Demon From Below Demon Prince", False),
        (15100800, "Halflight Spear Of The Church", False),
        (15100850, "Darkeater Midir", True),
        (15110800, "Slave Knight Gael", True),
    ],
    "Sekiro": [
        (9301, "Gyoubu Masataka Oniwa", False),
        (9302, "Lady Butterfly", False),
        (9303, "Genichiro Ashina", True),
        (9305, "Folding Screen Monkeys", False),
        (9304, "Guardian Ape", False),
        (9307, "Headless Ape", False),
        (9306, "Corrupted Monk Ghost", False),
        (9315, "Emma The Gentle Blade", True),
        (9316, "Isshin Ashina", False),
        (9308, "Great Shinobi Owl", True),
        (9309, "True Corrupted Monk", True),
        (9310, "Divine Dragon", True),
        (9317, "Owl Father", True),
        (9313, "Demon Of Hatred", True),
        (9312, "Isshin The Sword Saint", True),
    ],
}

# id -> (location, uncertain). Every id in BOSSES must appear here.
LOCATIONS: dict[str, dict[int, tuple[str, bool]]] = {
    "DarkSouls1Remastered": {
        16: ("Northern Undead Asylum", False),
        3: ("Undead Parish", False),
        11010902: ("Lower Undead Burg", False),
        11410900: ("Demon Ruins", False),
        11410901: ("Demon Ruins", False),
        9: ("Quelaag's Domain", False),
        4: ("Painted World of Ariamis", False),
        11510900: ("Anor Londo", False),
        11410410: ("Demon Ruins", False),
        13: ("New Londo Ruins", False),
        2: ("The Depths", False),
        5: ("Darkroot Garden", False),
        15: ("Kiln of the First Flame", False),
        11: ("Sen's Fortress", False),
        11200900: ("Darkroot Garden", False),
        7: ("Tomb of the Giants", False),
        12: ("Anor Londo", False),
        6: ("The Catacombs", False),
        14: ("Duke's Archives", False),
        11810900: ("Firelink Shrine", False),
        11010901: ("Undead Burg", False),
        10: ("Lost Izalith", False),
        11210001: ("Royal Wood", False),
        11210004: ("Royal Wood", True),
        11210002: ("Chasm of the Abyss", False),
        11210000: ("Oolacile Sanctuary", False),
    },
    "DarkSouls2Scholar": {
        124: ("Forest of Fallen Giants", False),
        112: ("Forest of Fallen Giants", False),
        52: ("Cave of the Dead", True),
        72: ("Drangleic Castle", False),
        56: ("Huntsman's Copse", False),
        84: ("Lost Bastille", False),
        92: ("Lost Bastille", True),
        168: ("Shaded Woods", False),
        88: ("Lost Bastille", False),
        100: ("The Gutter", True),
        108: ("Black Gulch", False),
        68: ("Harvest Valley", True),
        44: ("Brightstone Cove Tseldora", False),
        64: ("Earthen Peak", False),
        104: ("Black Gulch", False),
        80: ("Heide's Tower of Flame", True),
        60: ("Harvest Valley", True),
        96: ("Iron Keep", False),
        48: ("Iron Keep", False),
        120: ("Dragon Aerie", False),
        40: ("Brightstone Cove Tseldora", True),
        140: ("Drangleic Castle", False),
        152: ("Undead Crypt", False),
        156: ("Dark Chasm of Old", False),
        76: ("Heide's Tower of Flame", False),
        160: ("Drangleic Castle", False),
        164: ("Brightstone Cove Tseldora", False),
        128: ("Memory of the Giants", True),
        148: ("Dragon Shrine", False),
        136: ("Drangleic Castle", False),
        132: ("Drangleic Castle", False),
        280: ("Aldia's Keep", True),
        200: ("Shulva, Sanctum City", False),
        212: ("Dragon's Rest", False),
        244: ("Brume Tower", False),
        252: ("Brume Tower", False),
        204: ("Brume Tower", True),
        248: ("Iron Passage", True),
        260: ("Frozen Eleum Loyce", False),
        208: ("Frigid Outskirts", False),
        264: ("Frozen Eleum Loyce", False),
    },
    "DarkSouls3": {
        14000800: ("Cemetery of Ash", False),
        13000800: ("High Wall of Lothric", False),
        13100800: ("Undead Settlement", False),
        13300850: ("Road of Sacrifices", False),
        13300800: ("Farron Keep", False),
        13500800: ("Cathedral of the Deep", False),
        13800800: ("Catacombs of Carthus", False),
        13800830: ("Smouldering Lake", False),
        13700850: ("Irithyll of the Boreal Valley", False),
        13900800: ("Profaned Capital", False),
        13700800: ("Anor Londo", False),
        13000890: ("Lothric Castle", True),
        13010800: ("Lothric Castle", False),
        13000830: ("Consumed King's Garden", False),
        14000830: ("Untended Graves", False),
        13410830: ("Grand Archives", True),
        13200800: ("Archdragon Peak", False),
        13200850: ("Archdragon Peak", False),
        14100800: ("Kiln of the First Flame", False),
        14500800: ("Ariandel Chapel", False),
        14500860: ("Ariandel Chapel", True),
        15000800: ("Dreg Heap", False),
        15100800: ("The Ringed City", False),
        15100850: ("The Ringed City", False),
        15110800: ("Ringed City Streets", True),
    },
    "Sekiro": {
        9301: ("Ashina Castle", False),
        9302: ("Hirata Estate", False),
        9303: ("Ashina Castle", False),
        9305: ("Senpou Temple, Mt. Kongo", False),
        9304: ("Sunken Valley", False),
        9307: ("Bodhisattva Valley", False),
        9306: ("Ashina Depths", True),
        9315: ("Ashina Castle", True),
        9316: ("Ashina Castle", False),
        9308: ("Ashina Castle", False),
        9309: ("Fountainhead Palace", False),
        9310: ("Divine Realm of the Dragon's Homeland", False),
        9317: ("Ashina Castle", True),
        9313: ("Ashina Outskirts", True),
        9312: ("Ashina Castle", False),
    },
}

DISPLAY_NAMES = {
    "DarkSouls1Remastered": "DS1R",
    "DarkSouls2Scholar": "DS2 SOTFS",
    "DarkSouls3": "DS3",
    "Sekiro": "Sekiro",
}

SOURCE_ENUM = {
    "DarkSouls1Remastered": "SoulMemory.DarkSouls1.Boss",
    "DarkSouls2Scholar": "SoulMemory.DarkSouls2.BossType",
    "DarkSouls3": "SoulMemory.DarkSouls3.Boss",
    "Sekiro": "SoulMemory.Sekiro.Boss",
}


def emit(game_csid: str) -> tuple[str, str, list[tuple[str, str, str]]]:
    bosses = BOSSES[game_csid]
    locations = LOCATIONS.get(game_csid, {})
    short = DISPLAY_NAMES[game_csid]
    src_type = SOURCE_ENUM[game_csid]

    missing = [f"{name} (id {boss_id})" for boss_id, name, _ in bosses if boss_id not in locations]
    if missing:
        print(
            f"ERROR: {game_csid} has {len(missing)} boss(es) with no curated location: "
            + ", ".join(missing),
            file=sys.stderr,
        )
        sys.exit(1)

    cls_name = f"{game_csid}BossData"
    uncertain_entries: list[tuple[str, str, str]] = []
    lines = [
        "// <auto-generated />",
        "// Source: SoulMemory NuGet (GPL-3, FrankvdStam/SoulSplitter).",
        f"// Game: {short}. Regenerate via tools/generate_soulmemory_boss_data.py.",
        "namespace Soulsjwa.Api.Features.Games.Definitions;",
        "",
        "/// <summary>",
        f"/// Boss table for {short}, extracted from SoulMemory's",
        f"/// <c>{src_type}</c> enum. Each entry maps a SoulMemory id to a display",
        "/// name, its in-game location, and a \"main boss\" flag (used by",
        "/// predefined-objective scoring).",
        "/// </summary>",
        f"public static class {cls_name}",
        "{",
        "    public sealed record Entry(long Id, string Name, string Category, bool IsMainBoss);",
        "",
        "    public static readonly IReadOnlyList<Entry> All = new Entry[]",
        "    {",
    ]
    for boss_id, name, is_main in bosses:
        disp = name.replace('"', '\\"')
        location, uncertain = locations[boss_id]
        loc_escaped = location.replace('"', '\\"')
        is_main_lit = "true" if is_main else "false"
        lines.append(f'        new({boss_id}, "{disp}", "{loc_escaped}", {is_main_lit}),')
        if uncertain:
            uncertain_entries.append((short, disp, location))
    lines.append("    };")
    lines.append("}")
    lines.append("")
    return cls_name, "\n".join(lines), uncertain_entries


def write_uncertain_report(all_uncertain: list[tuple[str, str, str]]) -> None:
    lines = [
        "# Uncertain boss locations",
        "",
        "Generated by `tools/generate_soulmemory_boss_data.py`. These boss-to-location",
        "mappings are best-effort — the boss has a real, non-placeholder location, but",
        "the curator was not fully confident it is the precise in-game area name used",
        "by speedrunning/wiki convention. In case of wrong locations, we will update as we go.",
        "",
        "| Game | Boss | Current location |",
        "|---|---|---|",
    ]
    for game, boss, location in all_uncertain:
        lines.append(f"| {game} | {boss} | {location} |")
    lines.append("")
    UNCERTAIN_REPORT.write_text("\n".join(lines))


def main() -> None:
    all_uncertain: list[tuple[str, str, str]] = []
    for game_csid in BOSSES:
        cls, content, uncertain_entries = emit(game_csid)
        out_path = OUT_DIR / f"{cls}.cs"
        out_path.write_text(content)
        print(f"wrote {out_path} - {len(content.splitlines())} lines")
        all_uncertain.extend(uncertain_entries)

    write_uncertain_report(all_uncertain)
    print(f"wrote {UNCERTAIN_REPORT} - {len(all_uncertain)} uncertain entries")


if __name__ == "__main__":
    main()
