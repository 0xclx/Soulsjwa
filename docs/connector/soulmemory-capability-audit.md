# SoulMemory capability audit

## Source pin and scope

This implementation was audited against
[`FrankvdStam/SoulSplitter`](https://github.com/FrankvdStam/SoulSplitter/tree/aeb7cbee11ffd3c0bcf3bf5eac4a861b267b5db4/src/SoulMemory)
commit `aeb7cbee11ffd3c0bcf3bf5eac4a861b267b5db4` (the tip of `main`
on 2026-08-01). Every file under `src/SoulMemory` was classified as:

- a public, read-only observable game-state API or catalog;
- internal memory/process infrastructure used by those reads; or
- a write, patch, warp, mod, process-handle, or debugging operation excluded
  from connector data.

The connector uses NuGet `SoulMemory` 1.8.5. The pinned commit has the same
public C# read API and catalogs. GitHub release 1.8.6 is not published to
NuGet; its C# difference is a DS1 drop-mod write change. Later `main` changes
are internal Rust quantity-flag handling and expose no new C# read method.

## File coverage

| Area | Files reviewed | Result |
|------|----------------|--------|
| Common | `AnnotationsAttribute.cs`, `Extensions.cs`, `FlagWatcher.cs`, `IGame.cs`, `RefreshError.cs`, `Result.cs`, `Vector3f.cs`, `VersionAttribute.cs`, `SoulMemory.csproj` | `IGame` event flags/time are exposed where reliable; utility/process APIs are not data points |
| DS1 | `Attribute.cs`, `Bonfire.cs`, `Boss.cs`, `DarkSouls1.cs`, `DropMod.cs`, `DropModType.cs`, `IDarkSouls1.cs`, `Item.cs`, `ItemReader.cs`, `KnownFlag.cs`, `Ptde.cs`, `Remastered.cs`, `Sl2Reader.cs`, `Parameters/*` | All live read APIs and named catalogs exposed; write/drop-mod/parameter mutation excluded |
| DS2 | `Attribute.cs`, `BossType.cs`, `DarkSouls2.cs`, `Data.cs`, `IDarkSouls2.cs`, `WarpType.cs`, `scholar.cs`, `vanilla.cs` | All reliable live reads exposed; event flags and constant-zero time excluded |
| DS3 | `Attributes.cs`, `Bonfire.cs`, `Boss.cs`, `DarkSouls3.cs`, `ItemPickup.cs` | All public live reads and named catalogs exposed; time write excluded |
| Sekiro | `Attribute.cs`, `Boss.cs`, `Idol.cs`, `Sekiro.cs` | All public read state and catalogs exposed; event/time writes excluded |
| Elden Ring | `Boss.cs`, `EldenRing.cs`, `Grace.cs`, `Item.cs`, `ItemPickup.cs`, `KnownFlag.cs`, `Position.cs`, `ScreenState.cs` | All public read state and catalogs exposed; HUD/time/FPS writes and FPS process settings excluded |
| Armored Core VI | `ArmoredCore6.cs` | Not advertised: no named event catalog, automatic process patch/injection, and pinned `TryRefresh()` always returns `ModLoadFailed` after injection |
| Infrastructure | `Memory/*`, `MemoryV2/*`, `Native/*`, shared `Parameters/*`, `soulmods/Soulmods.cs` | Pointer scanning, native process access, parameter decoding, and injection internals; no additional named game-state contract |
| Assets | `soulmods/*` binaries and `soulsplitter.ico` | Non-source assets; no data-point definitions |

## Implemented matrix

| Game | Named catalogs | Other read-only state |
|------|----------------|-----------------------|
| DS1 Remastered | 26 bosses, 52 known flags, 43 bonfires, 685 inventory items | 10 attributes, time, NG cycle, health, save slot, player loaded, warp requested, credits, XYZ |
| DS2 Scholar | 41 boss counters | 10 attributes, loading, XYZ |
| DS3 | 25 bosses, 77 bonfires, 1,144 item-pickup flags | 11 attributes, time, loading, player loaded, blackscreen, XYZ |
| Sekiro | 15 bosses, 55 idols | 2 attributes, time, player loaded, blackscreen, BitBlt mode, XYZ |
| Elden Ring Memory | 211 upstream boss entries, 419 graces, 50 known progression flags, 4,209 item-pickup flags, 2,652 inventory items | time, NG cycle, player loaded, blackscreen, screen state, map ID components, XYZ |

Catalog data is generated into `SoulMemoryCatalogData.cs` from the pinned
source. Stable data-point IDs use the game, catalog kind, and upstream enum
member. Existing boss IDs remain unchanged.

## Intentional exclusions and limits

- No explicit write API is called: event/time writes, warps, HUD/FPS changes,
  inventory-index resets, item-lot/text modifications, and drop mods are excluded.
- SoulMemory itself patches/injects during attachment for Sekiro, Elden Ring,
  and Armored Core VI. This is upstream `TryRefresh` behavior and remains
  documented in the connector.
- Elden Ring `ReadInventory()` returns item identity but no stack quantity.
  Rules therefore receive a `0/1` presence value. Key items that SoulMemory
  stores only as event flags remain available through the complete
  `ItemPickup`/`KnownFlag` catalogs.
- DS2 `GetInGameTimeMilliseconds()` is hardcoded to zero and DS2 event-flag
  reads are documented unreliable, so neither is advertised. `Data.Bonfires`
  and `WarpType` contain warp metadata but no reliable observable bonfire state.
- Floating-point world coordinates are finite decimal data points; all other
  submitted values remain integers.
- FPS patch/limit getters describe injected process configuration rather than
  game progress and are outside the confirmed observable-game-state boundary.
- Armored Core VI exists in the Soulsjwa catalog, but cannot be safely enabled:
  its pinned `TryRefresh()` callback always returns `ModLoadFailed` even after a
  successful injection, and attachment automatically patches/injects the
  process. It remains explicitly reported rather than falsely advertised.
