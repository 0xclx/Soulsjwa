using System.Globalization;
using Soulsjwa.Shared;

namespace Soulsjwa.Api.Features.Games.Definitions;

/// <summary>
/// Hardcoded definitions of what the connector reads from live process memory
/// via SoulMemory. NOT stored in the database — served directly from C# to the
/// connector. Bump <c>ConnectorConstants.Version</c> when definitions change.
/// See <c>docs/connector/README.md</c> for the per-game capability matrix.
///
/// Upstream SoulMemory constraints that shape these catalogs:
/// - DS2 Scholar: <c>ReadEventFlag</c> is not reliable, hence boss kill counts
///   instead of event flags. <c>GetInGameTimeMilliseconds()</c> is hardcoded to
///   return 0 (SoulMemory 1.8.5), so DS2 deliberately advertises no in-game-time
///   data point — it would silently report "0 ms elapsed" forever.
/// - DS3: attribute reads can return <c>-1</c> while a menu is open (documented
///   SoulMemory behavior, not an error); <see cref="GameDataValueKind.Counter"/>
///   allows negative values for exactly this reason.
/// - Elden Ring: inventory is presence-only — SoulMemory's public <c>Item</c>
///   result does not expose stack quantity.
///
/// What <c>Offset</c> (and <c>SourceId</c>) hold per data type:
/// - <c>event_flag</c> — decimal SoulMemory flag id; 1 once set, 0 otherwise.
/// - <c>boss_kill_count</c> — DS2-only; the <c>BossType</c> id, read via
///   <c>GetBossKillCount(BossType)</c> (NG-cycle-aware).
/// - <c>attribute</c> — the game's <c>Attribute</c> enum member name.
/// - <c>inventory_item_quantity</c> — DS1-only;
///   <c>"{SoulMemory ItemCategory}:{ItemId}"</c>.
/// - <c>game_time</c> — per-game slot_stat id, in MILLISECONDS. Predefined
///   objectives encode the units in their rule constants.
/// - <c>ng_count</c> / <c>ng_level</c> — NG cycle counter (DS1 / ER Memory
///   respectively); each game exposes at most one.
/// </summary>
public static class GameDataDefinitions
{
    public static IReadOnlyList<GameDataPoint> ForGame(int gameId) => gameId switch
    {
        GameIds.DarkSouls1Remastered => DarkSouls1RemasteredDataPoints.Value,
        GameIds.DarkSouls2Scholar => DarkSouls2ScholarDataPoints.Value,
        GameIds.DarkSouls3 => DarkSouls3DataPoints.Value,
        GameIds.Sekiro => SekiroDataPoints.Value,
        GameIds.EldenRingMemory => EldenRingMemoryDataPoints.Value,
        _ => []
    };

    public static IReadOnlyList<int> SupportedGameIds =>
    [
        GameIds.DarkSouls1Remastered,
        GameIds.DarkSouls2Scholar,
        GameIds.DarkSouls3,
        GameIds.Sekiro,
        GameIds.EldenRingMemory,
    ];

    public static string SoulMemoryFlagId(int gameId, long flag) =>
        // Game id prefix avoids collisions when two SoulMemory games happen
        // to share a numeric flag value (DS2 uses small ints, DS3 uses huge
        // ones, but the prefix makes the dataset self-describing).
        $"g{gameId}_f{flag}";

    public static string SoulMemoryGameTimeId(int gameId) => $"g{gameId}_game_time_ms";

    private static string SoulMemoryNgCountId(int gameId) => $"g{gameId}_ng_count";
    private static string SoulMemoryNgLevelId(int gameId) => $"g{gameId}_ng_level";
    private static string SoulMemoryPlayerHealthId(int gameId) => $"g{gameId}_player_health";

    private static string SoulMemoryAttributeId(int gameId, string attributeName) => $"g{gameId}_attr_{attributeName}";

    private static string SoulMemoryInventoryItemId(int gameId, string catalogKey) =>
        $"g{gameId}_item_{catalogKey.Replace(":", "_", StringComparison.Ordinal).ToLowerInvariant()}";

    public static string SoulMemoryCatalogId(int gameId, string kind, string member) =>
        $"g{gameId}_{kind}_{member}".ToLowerInvariant();

    /// <summary>
    /// ER boss kills use the same event-flag mechanism as every other data point
    /// here, keyed by <see cref="SoulMemoryFlagId"/> so the ids match what
    /// <see cref="Definitions.PredefinedObjectives"/> derives from the same
    /// <see cref="SoulMemoryCatalogData.EldenRingBosses"/> table. Inventory is
    /// presence-only: SoulMemory's <c>EldenRing.Item</c> lookup table is private
    /// (only <c>Item.FromLookupTable(category, id)</c> is public), so stack
    /// quantities cannot be enumerated without depending on private internals.
    /// </summary>
    private static readonly Lazy<IReadOnlyList<GameDataPoint>> EldenRingMemoryDataPoints =
        new(() =>
        {
            const int gameId = GameIds.EldenRingMemory;

            // A handful of bosses share a FlagId with another named encounter
            // (e.g. either Crucible Knight in a duo fight sets the same
            // flag) — one data point per underlying flag.
            var list = SoulMemoryCatalogData.EldenRingBosses
                .GroupBy(b => b.Id)
                .Select(group => group.First())
                .Select(b => MemoryEventFlagBossPoint(gameId, b.Id, b.Name))
                .ToList();

            list.AddRange(SoulMemoryCatalogData.EldenRingGraces.Select(entry =>
                MemoryCatalogEventFlagPoint(gameId, SoulMemoryCatalogKinds.Grace, entry, GameDataCategory.Locations)));
            list.AddRange(SoulMemoryCatalogData.EldenRingKnownFlags.Select(entry =>
                MemoryCatalogEventFlagPoint(gameId, SoulMemoryCatalogKinds.Event, entry, GameDataCategory.Progression)));
            list.AddRange(SoulMemoryCatalogData.EldenRingItemPickups.Select(entry =>
                MemoryCatalogEventFlagPoint(gameId, SoulMemoryCatalogKinds.Pickup, entry, GameDataCategory.Inventory)));
            list.AddRange(SoulMemoryCatalogData.EldenRingInventory.Select(entry =>
                EldenRingInventoryItemPoint(gameId, entry)));

            list.Add(SoulMemoryGameTimePoint(gameId));
            list.AddRange(BooleanStatePoints(
                gameId,
                MemoryBooleanStateSource.PlayerLoaded,
                MemoryBooleanStateSource.Blackscreen));
            list.Add(IntegerStatePoint(gameId, MemoryIntegerStateSource.ScreenState, "Screen State"));
            list.AddRange(PositionPoints(
                gameId,
                MemoryPositionComponentSource.X,
                MemoryPositionComponentSource.Y,
                MemoryPositionComponentSource.Z,
                MemoryPositionComponentSource.Area,
                MemoryPositionComponentSource.Block,
                MemoryPositionComponentSource.Region,
                MemoryPositionComponentSource.Size));

            var ngLevelId = SoulMemoryNgLevelId(gameId);
            list.Add(new GameDataPoint(
                ngLevelId, "New Game Cycle", ngLevelId, "ng_level",
                Description: "Current New Game+ level (0 = first playthrough), via ReadNgLevel().",
                Unit: "cycle",
                Category: GameDataCategory.Progression,
                ValueKind: GameDataValueKind.Counter,
                ReaderCapability: GameDataReaderCapability.MemoryNgLevel));

            return list;
        });

    /// <summary>SoulMemory.DarkSouls1.Attribute member names + display names.</summary>
    private static readonly (string SourceId, string DisplayName)[] DarkSouls1Attributes =
    [
        ("Vitality", "Vitality"),
        ("Attunement", "Attunement"),
        ("Endurance", "Endurance"),
        ("Strength", "Strength"),
        ("Dexterity", "Dexterity"),
        ("Resistance", "Resistance"),
        ("Intelligence", "Intelligence"),
        ("Faith", "Faith"),
        ("Humanity", "Humanity"),
        ("SoulLevel", "Soul Level"),
    ];

    private static readonly Lazy<IReadOnlyList<GameDataPoint>> DarkSouls1RemasteredDataPoints =
        new(() =>
        {
            const int gameId = GameIds.DarkSouls1Remastered;
            var list = DarkSouls1RemasteredBossData.All
                .Select(b => MemoryEventFlagBossPoint(gameId, b.Id, b.Name))
                .ToList();

            list.AddRange(SoulMemoryCatalogData.DarkSouls1KnownFlags.Select(entry =>
                MemoryCatalogEventFlagPoint(gameId, SoulMemoryCatalogKinds.Event, entry, GameDataCategory.Progression)));
            list.AddRange(SoulMemoryCatalogData.DarkSouls1Bonfires.Select(entry =>
                LocationStatePoint(gameId, SoulMemoryCatalogKinds.Bonfire, entry)));

            list.Add(SoulMemoryGameTimePoint(gameId));
            list.AddRange(BooleanStatePoints(
                gameId,
                MemoryBooleanStateSource.PlayerLoaded,
                MemoryBooleanStateSource.CreditsRolling,
                MemoryBooleanStateSource.WarpRequested));
            list.Add(IntegerStatePoint(gameId, MemoryIntegerStateSource.CurrentSaveSlot, "Current Save Slot"));
            list.AddRange(PositionPoints(
                gameId,
                MemoryPositionComponentSource.X,
                MemoryPositionComponentSource.Y,
                MemoryPositionComponentSource.Z));
            list.AddRange(DarkSouls1Attributes.Select(a => AttributePoint(
                gameId, a.SourceId, a.DisplayName,
                $"Current {a.DisplayName} attribute level, via GetAttribute().")));

            var ngCountId = SoulMemoryNgCountId(gameId);
            list.Add(new GameDataPoint(
                ngCountId, "New Game Cycle", ngCountId, "ng_count",
                Description: "Current NG+ cycle (0 = first playthrough), via NgCount().",
                Unit: "cycle",
                Category: GameDataCategory.Progression,
                ValueKind: GameDataValueKind.Counter,
                ReaderCapability: GameDataReaderCapability.MemoryNgCount));

            var healthId = SoulMemoryPlayerHealthId(gameId);
            list.Add(new GameDataPoint(
                healthId, "Player Health", healthId, "player_health",
                Description: "Player's current HP, via GetPlayerHealth().",
                Unit: "hp",
                Category: GameDataCategory.Stats,
                ValueKind: GameDataValueKind.Counter,
                ReaderCapability: GameDataReaderCapability.MemoryPlayerHealth));

            list.AddRange(DarkSouls1RemasteredItemData.All.Select(item => InventoryItemPoint(gameId, item)));

            return list;
        });

    /// <summary>SoulMemory.DarkSouls3.Attribute member names + display names.</summary>
    private static readonly (string SourceId, string DisplayName)[] DarkSouls3Attributes =
    [
        ("Vigor", "Vigor"),
        ("Attunement", "Attunement"),
        ("Endurance", "Endurance"),
        ("Vitality", "Vitality"),
        ("Strength", "Strength"),
        ("Dexterity", "Dexterity"),
        ("Intelligence", "Intelligence"),
        ("Faith", "Faith"),
        ("Luck", "Luck"),
        ("SoulLevel", "Soul Level"),
        ("Humanity", "Humanity"),
    ];

    private static readonly Lazy<IReadOnlyList<GameDataPoint>> DarkSouls3DataPoints =
        new(() =>
        {
            const int gameId = GameIds.DarkSouls3;
            var list = DarkSouls3BossData.All
                .Select(b => MemoryEventFlagBossPoint(gameId, b.Id, b.Name))
                .ToList();

            list.AddRange(SoulMemoryCatalogData.DarkSouls3Bonfires.Select(entry =>
                MemoryCatalogEventFlagPoint(gameId, SoulMemoryCatalogKinds.Bonfire, entry, GameDataCategory.Locations)));
            list.AddRange(SoulMemoryCatalogData.DarkSouls3ItemPickups.Select(entry =>
                MemoryCatalogEventFlagPoint(gameId, SoulMemoryCatalogKinds.Pickup, entry, GameDataCategory.Inventory)));

            list.Add(SoulMemoryGameTimePoint(gameId));
            list.AddRange(BooleanStatePoints(
                gameId,
                MemoryBooleanStateSource.Loading,
                MemoryBooleanStateSource.PlayerLoaded,
                MemoryBooleanStateSource.Blackscreen));
            list.AddRange(PositionPoints(
                gameId,
                MemoryPositionComponentSource.X,
                MemoryPositionComponentSource.Y,
                MemoryPositionComponentSource.Z));
            list.AddRange(DarkSouls3Attributes.Select(a => AttributePoint(
                gameId, a.SourceId, a.DisplayName,
                $"Current {a.DisplayName} attribute level, via ReadAttribute(). " +
                "May read -1 momentarily while a menu (e.g. inventory) is open — this is " +
                "documented SoulMemory behavior, not a read failure.")));

            return list;
        });

    /// <summary>SoulMemory.Sekiro.Attribute exposes exactly these two members — GetAttribute throws for any other value.</summary>
    private static readonly (string SourceId, string DisplayName)[] SekiroAttributes =
    [
        ("Vitality", "Vitality"),
        ("AttackPower", "Attack Power"),
    ];

    private static readonly Lazy<IReadOnlyList<GameDataPoint>> SekiroDataPoints =
        new(() =>
        {
            const int gameId = GameIds.Sekiro;
            var list = SekiroBossData.All
                .Select(b => MemoryEventFlagBossPoint(gameId, b.Id, b.Name))
                .ToList();

            list.AddRange(SoulMemoryCatalogData.SekiroIdols.Select(entry =>
                MemoryCatalogEventFlagPoint(gameId, SoulMemoryCatalogKinds.Idol, entry, GameDataCategory.Locations)));

            list.Add(SoulMemoryGameTimePoint(gameId));
            list.AddRange(BooleanStatePoints(
                gameId,
                MemoryBooleanStateSource.PlayerLoaded,
                MemoryBooleanStateSource.Blackscreen,
                MemoryBooleanStateSource.BitBlt));
            list.AddRange(PositionPoints(
                gameId,
                MemoryPositionComponentSource.X,
                MemoryPositionComponentSource.Y,
                MemoryPositionComponentSource.Z));
            list.AddRange(SekiroAttributes.Select(a => AttributePoint(
                gameId, a.SourceId, a.DisplayName,
                $"Current {a.DisplayName} attribute level, via GetAttribute().")));

            return list;
        });

    /// <summary>SoulMemory.DarkSouls2.Attribute member names + display names.</summary>
    private static readonly (string SourceId, string DisplayName)[] DarkSouls2Attributes =
    [
        ("SoulLevel", "Soul Level"),
        ("Vigor", "Vigor"),
        ("Endurance", "Endurance"),
        ("Vitality", "Vitality"),
        ("Attunement", "Attunement"),
        ("Strength", "Strength"),
        ("Dexterity", "Dexterity"),
        ("Adaptability", "Adaptability"),
        ("Intelligence", "Intelligence"),
        ("Faith", "Faith"),
    ];

    /// <summary>
    /// DS2 is the odd one out: <c>ReadEventFlag</c> is not reliable for DS2 (per
    /// upstream README), so bosses are exposed as <c>boss_kill_count</c> points.
    /// And <c>GetInGameTimeMilliseconds()</c> is hardcoded to return 0 for both
    /// DS2 implementations, so no in-game-time point is advertised here.
    /// </summary>
    private static readonly Lazy<IReadOnlyList<GameDataPoint>> DarkSouls2ScholarDataPoints =
        new(() =>
        {
            const int gameId = GameIds.DarkSouls2Scholar;
            var list = DarkSouls2ScholarBossData.All
                .Select(b => BossKillCountPoint(gameId, b.Id, b.Name))
                .ToList();

            list.AddRange(BooleanStatePoints(gameId, MemoryBooleanStateSource.Loading));
            list.AddRange(PositionPoints(
                gameId,
                MemoryPositionComponentSource.X,
                MemoryPositionComponentSource.Y,
                MemoryPositionComponentSource.Z));
            list.AddRange(DarkSouls2Attributes.Select(a => AttributePoint(
                gameId, a.SourceId, a.DisplayName,
                $"Current {a.DisplayName} attribute level, via GetAttribute().")));

            return list;
        });

    private static GameDataPoint MemoryEventFlagBossPoint(int gameId, long flagId, string name)
    {
        var id = SoulMemoryFlagId(gameId, flagId);
        var offset = flagId.ToString(CultureInfo.InvariantCulture);
        return new GameDataPoint(
            id, name, offset, "event_flag",
            Description: $"1 once \"{name}\" has been defeated, 0 otherwise.",
            Category: GameDataCategory.Bosses,
            ValueKind: GameDataValueKind.Flag,
            ReaderCapability: GameDataReaderCapability.MemoryEventFlag);
    }

    private static GameDataPoint BossKillCountPoint(int gameId, long bossTypeId, string name)
    {
        var id = SoulMemoryFlagId(gameId, bossTypeId);
        var offset = bossTypeId.ToString(CultureInfo.InvariantCulture);
        return new GameDataPoint(
            id, name, offset, "boss_kill_count",
            Description: $"NG-cycle-aware kill count for \"{name}\" (greater than 0 once defeated at least once), via GetBossKillCount().",
            Category: GameDataCategory.Bosses,
            ValueKind: GameDataValueKind.Counter,
            ReaderCapability: GameDataReaderCapability.MemoryBossKillCount);
    }

    private static GameDataPoint AttributePoint(int gameId, string attributeSourceId, string displayName, string description)
    {
        var id = SoulMemoryAttributeId(gameId, attributeSourceId);
        return new GameDataPoint(
            id, displayName, attributeSourceId, "attribute",
            Description: description,
            Unit: "level",
            Category: GameDataCategory.Stats,
            ValueKind: GameDataValueKind.Counter,
            ReaderCapability: GameDataReaderCapability.MemoryAttribute);
    }

    private static GameDataPoint SoulMemoryGameTimePoint(int gameId)
    {
        var id = SoulMemoryGameTimeId(gameId);
        return new GameDataPoint(
            id, "Game Time (milliseconds)", id, "slot_stat",
            Description: "Elapsed play time for the current character, in milliseconds, via GetInGameTimeMilliseconds().",
            Unit: "ms",
            Category: GameDataCategory.Time,
            ValueKind: GameDataValueKind.Duration,
            ReaderCapability: GameDataReaderCapability.MemoryInGameTimeMilliseconds);
    }

    private static GameDataPoint InventoryItemPoint(int gameId, DarkSouls1RemasteredItemData.Entry item)
    {
        var id = SoulMemoryInventoryItemId(gameId, item.Key);
        var category = item.Group switch
        {
            nameof(GameDataCategory.KeyItems) => GameDataCategory.KeyItems,
            nameof(GameDataCategory.Currency) => GameDataCategory.Currency,
            _ => GameDataCategory.Inventory,
        };
        return new GameDataPoint(
            id, item.Name, item.Key, "inventory_item_quantity",
            Description: $"Quantity of \"{item.Name}\" currently held in the player's inventory, via GetInventory().",
            Unit: "qty",
            Category: category,
            ValueKind: GameDataValueKind.Counter,
            ReaderCapability: GameDataReaderCapability.MemoryInventoryItemQuantity);
    }

    private static GameDataPoint MemoryCatalogEventFlagPoint(
        int gameId,
        string kind,
        SoulMemoryCatalogData.CatalogEntry entry,
        GameDataCategory category) =>
        new(
            SoulMemoryCatalogId(gameId, kind, entry.Member),
            entry.Name,
            entry.Id.ToString(CultureInfo.InvariantCulture),
            "event_flag",
            Description: $"1 once \"{entry.Name}\" has been observed, 0 otherwise. SoulMemory event flag {entry.Id}.",
            Category: category,
            ValueKind: GameDataValueKind.Flag,
            ReaderCapability: GameDataReaderCapability.MemoryEventFlag,
            SourceId: entry.Id.ToString(CultureInfo.InvariantCulture));

    private static GameDataPoint LocationStatePoint(
        int gameId,
        string kind,
        SoulMemoryCatalogData.CatalogEntry entry) =>
        new(
            SoulMemoryCatalogId(gameId, kind, entry.Member),
            entry.Name,
            entry.Member,
            "location_state",
            Description: $"Current state of \"{entry.Name}\" via SoulMemory.",
            Category: GameDataCategory.Locations,
            ValueKind: GameDataValueKind.Counter,
            ReaderCapability: GameDataReaderCapability.MemoryLocationState,
            SourceId: entry.Member);

    private static IEnumerable<GameDataPoint> BooleanStatePoints(
        int gameId,
        params MemoryBooleanStateSource[] sources) =>
        sources.Select(source => new GameDataPoint(
            SoulMemoryCatalogId(gameId, SoulMemoryCatalogKinds.State, source.ToString()),
            source switch
            {
                MemoryBooleanStateSource.PlayerLoaded => "Player Loaded",
                MemoryBooleanStateSource.Loading => "Loading Screen Visible",
                MemoryBooleanStateSource.Blackscreen => "Blackscreen Active",
                MemoryBooleanStateSource.CreditsRolling => "Credits Rolling",
                MemoryBooleanStateSource.WarpRequested => "Warp Requested",
                MemoryBooleanStateSource.BitBlt => "BitBlt Mode",
                _ => source.ToString(),
            },
            source.ToString(),
            "state_flag",
            Description: $"Current {source} state reported by SoulMemory.",
            Category: GameDataCategory.State,
            ValueKind: GameDataValueKind.Flag,
            ReaderCapability: GameDataReaderCapability.MemoryBooleanState,
            SourceId: source.ToString()));

    private static GameDataPoint IntegerStatePoint(
        int gameId,
        MemoryIntegerStateSource source,
        string displayName) =>
        new(
            SoulMemoryCatalogId(gameId, SoulMemoryCatalogKinds.State, source.ToString()),
            displayName,
            source.ToString(),
            "state",
            Description: $"{displayName} reported by SoulMemory.",
            Category: GameDataCategory.State,
            ValueKind: GameDataValueKind.Counter,
            ReaderCapability: GameDataReaderCapability.MemoryIntegerState,
            SourceId: source.ToString());

    private static IEnumerable<GameDataPoint> PositionPoints(
        int gameId,
        params MemoryPositionComponentSource[] components) =>
        components.Select(component =>
        {
            var coordinate = component is MemoryPositionComponentSource.X
                or MemoryPositionComponentSource.Y
                or MemoryPositionComponentSource.Z;
            return new GameDataPoint(
                SoulMemoryCatalogId(gameId, SoulMemoryCatalogKinds.Position, component.ToString()),
                $"Position {component}",
                component.ToString(),
                "position_component",
                Description: $"Current player position {component} component reported by SoulMemory.",
                Unit: coordinate ? "world units" : "",
                Category: GameDataCategory.State,
                ValueKind: coordinate ? GameDataValueKind.Decimal : GameDataValueKind.Counter,
                ReaderCapability: GameDataReaderCapability.MemoryPositionComponent,
                SourceId: component.ToString());
        });

    private static GameDataPoint EldenRingInventoryItemPoint(
        int gameId,
        SoulMemoryCatalogData.InventoryEntry item)
    {
        var sourceId = $"{item.Category}:{item.Id}";
        var category = item.Group == "Key Items"
            ? GameDataCategory.KeyItems
            : GameDataCategory.Inventory;
        return new GameDataPoint(
            SoulMemoryInventoryItemId(gameId, sourceId),
            item.Name,
            sourceId,
            "inventory_item_presence",
            Description: $"1 when \"{item.Name}\" is present in SoulMemory's readable inventory list, 0 otherwise. Stack quantity is not exposed upstream.",
            Category: category,
            ValueKind: GameDataValueKind.Flag,
            ReaderCapability: GameDataReaderCapability.MemoryEldenRingInventoryItemPresence,
            SourceId: sourceId);
    }

}

public static class SoulMemoryCatalogKinds
{
    public const string Boss = "boss";
    public const string Bonfire = "bonfire";
    public const string Event = "event";
    public const string Grace = "grace";
    public const string Idol = "idol";
    public const string Pickup = "pickup";
    public const string Position = "position";
    public const string State = "state";
}
