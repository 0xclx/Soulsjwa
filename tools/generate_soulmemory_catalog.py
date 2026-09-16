#!/usr/bin/env python3
"""Regenerates the two SoulMemory-derived catalog files:

  src/Soulsjwa.Api/Features/Games/Definitions/SoulMemoryCatalogData.cs
  src/Soulsjwa.Api/Features/Games/Definitions/DarkSouls1RemasteredItemData.cs

Both carry a "do not edit by hand" header but had no generator checked in,
so a SoulMemory bump meant re-deriving ~9,000 lines by hand. This script
reads the SoulMemory *source* (FrankvdStam/SoulSplitter, src/SoulMemory) at
the pinned commit — either a local checkout via --source, or fetched from
raw.githubusercontent.com — and emits the files deterministically.

    python3 tools/generate_soulmemory_catalog.py            # rewrite both files
    python3 tools/generate_soulmemory_catalog.py --check    # exit 1 if they differ

What is generated from what:

  SoulMemoryCatalogData.<List>   <- one enum each; every member carrying an
                                   [Annotation(Name=..., Description=...)] becomes
                                   CatalogEntry(Member, Name, Id, Group=Description)
  SoulMemoryCatalogData.EldenRingInventory
                                 <- EldenRing/Item.cs LookupTable rows
  DarkSouls1RemasteredItemData   <- DarkSouls1/Item.cs AllItems rows, grouped:
                                   ItemCategory.Key -> KeyItems, the curated
                                   currency ids below -> Currency, else Inventory

To move to a newer SoulMemory: change COMMIT (and DATE) below, run the
script, review the diff, and bump ConnectorConstants.Version if any id or
name changed. The data is GPL-3 (see THIRD-PARTY-NOTICES.md).
"""

from __future__ import annotations

import argparse
import re
import sys
import urllib.request
from pathlib import Path

COMMIT = "aeb7cbee11ffd3c0bcf3bf5eac4a861b267b5db4"
DATE = "main on 2026-08-01"
RAW_BASE = f"https://raw.githubusercontent.com/FrankvdStam/SoulSplitter/{COMMIT}/src/SoulMemory/"

REPO_ROOT = Path(__file__).resolve().parent.parent
OUT_DIR = REPO_ROOT / "src/Soulsjwa.Api/Features/Games/Definitions"
CATALOG_FILE = OUT_DIR / "SoulMemoryCatalogData.cs"
DS1_ITEMS_FILE = OUT_DIR / "DarkSouls1RemasteredItemData.cs"

# (C# list name, source file, enum name)
CATALOG_LISTS: list[tuple[str, str, str]] = [
    ("DarkSouls1Bonfires", "DarkSouls1/Bonfire.cs", "Bonfire"),
    ("DarkSouls1KnownFlags", "DarkSouls1/KnownFlag.cs", "KnownFlag"),
    ("DarkSouls3Bonfires", "DarkSouls3/Bonfire.cs", "Bonfire"),
    ("DarkSouls3ItemPickups", "DarkSouls3/ItemPickup.cs", "ItemPickup"),
    ("SekiroIdols", "Sekiro/Idol.cs", "Idol"),
    ("EldenRingBosses", "EldenRing/Boss.cs", "Boss"),
    ("EldenRingGraces", "EldenRing/Grace.cs", "Grace"),
    ("EldenRingKnownFlags", "EldenRing/KnownFlag.cs", "KnownFlag"),
    ("EldenRingItemPickups", "EldenRing/ItemPickup.cs", "ItemPickup"),
]

# DS1 Consumables ids that count as currency: coins, soul consumables,
# humanity, boss souls. Hand-curated; everything else non-Key is Inventory.
DS1_CURRENCY_IDS = set(range(381, 384)) | set(range(400, 410)) | {500, 501} | set(range(700, 712))

ANNOTATED_MEMBER = re.compile(
    r'\[Annotation\(Name\s*=\s*"((?:[^"\\]|\\.)*)"(?:\s*,\s*Description\s*=\s*"((?:[^"\\]|\\.)*)")?\)\]\s*'
    r'(\w+)\s*=\s*(-?\d+)\s*,?',
    re.S,
)


def load_source(rel: str, source_dir: Path | None) -> str:
    if source_dir is not None:
        return (source_dir / rel).read_text(encoding="utf-8-sig")
    with urllib.request.urlopen(RAW_BASE + rel, timeout=120) as response:
        return response.read().decode("utf-8-sig")


def enum_body(text: str, enum_name: str) -> str:
    match = re.search(rf"\benum\s+{enum_name}\b[^{{]*\{{", text)
    if match is None:
        raise SystemExit(f"enum {enum_name} not found")
    depth, i = 1, match.end()
    while depth:
        if text[i] == "{":
            depth += 1
        elif text[i] == "}":
            depth -= 1
        i += 1
    return text[match.end() : i - 1]


def catalog_entries(text: str, enum_name: str) -> list[tuple[str, str, int, str]]:
    return [
        (member, name, int(value), description or "")
        for name, description, member, value in ANNOTATED_MEMBER.findall(enum_body(text, enum_name))
    ]


ER_ITEM = re.compile(
    r'new Item \{ Category = Category\.(\w+),\s*GroupName = "((?:[^"\\]|\\.)*)",\s*Name = "((?:[^"\\]|\\.)*)",\s*Id = (\d+) \}'
)


def er_inventory(text: str) -> list[tuple[str, str, str, int]]:
    return [(category, group, name, int(item_id)) for category, group, name, item_id in ER_ITEM.findall(text)]


DS1_ITEM = re.compile(
    r'new Item\("((?:[^"\\]|\\.)*)"\s*,\s*(\d+)\s*,\s*ItemType\.\w+\s*,\s*ItemCategory\.(\w+)\s*,'
)


def ds1_items(text: str) -> list[tuple[str, str, str]]:
    rows = []
    for name, item_id, category in DS1_ITEM.findall(text):
        item_id = int(item_id)
        if category == "Key":
            group = "KeyItems"
        elif category == "Consumables" and item_id in DS1_CURRENCY_IDS:
            group = "Currency"
        else:
            group = "Inventory"
        rows.append((f"{category}:{item_id}", name, group))
    return rows


def render_catalog(source_dir: Path | None) -> str:
    out = [
        "// <auto-generated />",
        "// Source: FrankvdStam/SoulSplitter src/SoulMemory at commit",
        f"// {COMMIT} ({DATE}).",
        "// Generated from public enums and Elden Ring Item lookup data. Do not edit by hand.",
        "// Regenerate with tools/generate_soulmemory_catalog.py.",
        "",
        "namespace Soulsjwa.Api.Features.Games.Definitions;",
        "",
        "public static class SoulMemoryCatalogData",
        "{",
        "    public sealed record CatalogEntry(string Member, string Name, uint Id, string Group);",
        "    public sealed record InventoryEntry(string Category, string Group, string Name, uint Id);",
    ]
    for list_name, rel, enum_name in CATALOG_LISTS:
        entries = catalog_entries(load_source(rel, source_dir), enum_name)
        out += ["", f"    public static readonly IReadOnlyList<CatalogEntry> {list_name} =", "    ["]
        out += [f'        new("{member}", "{name}", {value}u, "{group}"),' for member, name, value, group in entries]
        out.append("    ];")
    out += ["", "    public static readonly IReadOnlyList<InventoryEntry> EldenRingInventory =", "    ["]
    out += [
        f'        new("{category}", "{group}", "{name}", {item_id}u),'
        for category, group, name, item_id in er_inventory(load_source("EldenRing/Item.cs", source_dir))
    ]
    out += ["    ];", "}", ""]
    return "\n".join(out)


def render_ds1_items(source_dir: Path | None) -> str:
    out = [
        "// <auto-generated />",
        "// Source snapshot: SoulMemory NuGet (GPL-3, FrankvdStam/SoulSplitter),",
        "// SoulMemory.DarkSouls1.Item.AllItems. Regenerate with tools/generate_soulmemory_catalog.py.",
        "namespace Soulsjwa.Api.Features.Games.Definitions;",
        "",
        "/// <summary>",
        "/// Full DS1 Remastered inventory catalog, extracted from SoulMemory's",
        "/// public <c>SoulMemory.DarkSouls1.Item.AllItems</c>. Each entry is one",
        "/// <c>inventory_item_quantity</c> data point; <see cref=\"Key\"/> is",
        "/// <c>\"{SoulMemoryCategory}:{Id}\"</c> — the same composite string used as",
        "/// both <c>Offset</c> and <c>SourceId</c> so per-game ids stay unique even",
        "/// though SoulMemory reuses numeric item ids across categories.",
        "/// <see cref=\"Group\"/> is the display bucket used by",
        "/// <see cref=\"Soulsjwa.Shared.GameDataCategory\"/> (KeyItems for",
        "/// <c>ItemCategory.Key</c>; Currency for curated soul/humanity/coin item",
        "/// types; Inventory for everything else).",
        "/// </summary>",
        "public static class DarkSouls1RemasteredItemData",
        "{",
        "    public sealed record Entry(string Key, string Name, string Group);",
        "",
        "    public static readonly IReadOnlyList<Entry> All =",
        "    [",
    ]
    out += [f'        new("{key}", "{name}", "{group}"),' for key, name, group in ds1_items(load_source("DarkSouls1/Item.cs", source_dir))]
    out += ["    ];", "}", ""]
    return "\n".join(out)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--source", type=Path, help="local SoulSplitter src/SoulMemory directory instead of fetching")
    parser.add_argument("--check", action="store_true", help="do not write; exit 1 if the committed files differ")
    args = parser.parse_args()

    rendered = {CATALOG_FILE: render_catalog(args.source), DS1_ITEMS_FILE: render_ds1_items(args.source)}
    stale = [path for path, text in rendered.items() if not path.exists() or path.read_text(encoding="utf-8") != text]
    if args.check:
        for path in stale:
            print(f"stale: {path.relative_to(REPO_ROOT)}", file=sys.stderr)
        return 1 if stale else 0
    for path, text in rendered.items():
        path.write_text(text, encoding="utf-8", newline="\n")
        print(f"wrote {path.relative_to(REPO_ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
