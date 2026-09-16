using System.Text.Json.Serialization;

namespace Soulsjwa.Shared;

/// <summary>
/// Shape of a <see cref="GameDataPoint"/>'s value. Drives which
/// <see cref="GameDataComparisonOperator"/>s are meaningful and how connector
/// submission payloads are validated.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<GameDataValueKind>))]
public enum GameDataValueKind
{
    /// <summary>Boolean-shaped 0/1 value (e.g. an event flag).</summary>
    Flag,

    /// <summary>An arbitrary integer count (attribute level, NG cycle, item quantity, kill count). May be negative for values SoulMemory documents as sentinel-while-loading (e.g. DS3 attributes read -1 while a menu is open).</summary>
    Counter,

    /// <summary>An elapsed-time integer. <see cref="GameDataPoint.Unit"/> distinguishes milliseconds from seconds.</summary>
    Duration,

    /// <summary>A finite floating-point measurement, such as a world-position coordinate.</summary>
    Decimal,
}

/// <summary>
/// UI grouping bucket for a <see cref="GameDataPoint"/>. The Blockly rule
/// builder renders one toolbox category per value actually present in a game's
/// catalog.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<GameDataCategory>))]
public enum GameDataCategory
{
    /// <summary>Boss defeat flags / kill counters.</summary>
    Bosses,

    /// <summary>General inventory item quantities (weapons, armor, consumables, etc.).</summary>
    Inventory,

    /// <summary>Inventory item quantities for items the game categorizes as key items.</summary>
    KeyItems,

    /// <summary>Player attributes / stats (Vitality, Strength, player health, ...).</summary>
    Stats,

    /// <summary>Soul / humanity / coin style currency items.</summary>
    Currency,

    /// <summary>Run progression counters not tied to a specific stat (NG cycle, NG level).</summary>
    Progression,

    /// <summary>In-game time.</summary>
    Time,

    /// <summary>Named world locations such as bonfires, idols, and sites of grace.</summary>
    Locations,

    /// <summary>Transient game state such as loading, player-loaded, credits, or screen state.</summary>
    State,
}

/// <summary>
/// A comparison operator a Blockly rule can use against a
/// <see cref="GameDataPoint"/>. The member name is not a JsonLogic operator —
/// use <see cref="GameDataComparisonOperators.ToJsonLogicOperator"/> to map it.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<GameDataComparisonOperator>))]
public enum GameDataComparisonOperator
{
    Equal,
    NotEqual,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual,
}

/// <summary>
/// Identifies exactly which read operation a connector adapter must perform to
/// produce a <see cref="GameDataPoint"/>'s value. Typed replacement for the
/// legacy free-string <see cref="GameDataPoint.DataType"/>; per-game adapters
/// switch on this enum.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<GameDataReaderCapability>))]
public enum GameDataReaderCapability
{
    /// <summary><c>SoulMemory.IGame.ReadEventFlag(uint)</c> — boss/event defeat bit read from live process memory.</summary>
    MemoryEventFlag,

    /// <summary><c>SoulMemory.DarkSouls2.DarkSouls2.GetBossKillCount(BossType)</c> — NG-cycle-aware kill counter (DS2 only; <c>ReadEventFlag</c> is not reliable there).</summary>
    MemoryBossKillCount,

    /// <summary>Per-game <c>GetAttribute</c>/<c>ReadAttribute(Attribute)</c> — a player stat. <see cref="GameDataPoint.SourceId"/> holds the game's Attribute enum member name.</summary>
    MemoryAttribute,

    /// <summary><c>SoulMemory.IGame.GetInGameTimeMilliseconds()</c>.</summary>
    MemoryInGameTimeMilliseconds,

    /// <summary><c>SoulMemory.DarkSouls1.Remastered.NgCount()</c> (DS1 Remastered only).</summary>
    MemoryNgCount,

    /// <summary><c>SoulMemory.DarkSouls1.Remastered.GetPlayerHealth()</c> (DS1 Remastered only).</summary>
    MemoryPlayerHealth,

    /// <summary><c>SoulMemory.DarkSouls1.Remastered.GetInventory()</c> item quantity lookup. <see cref="GameDataPoint.SourceId"/> holds <c>"{ItemCategory}:{ItemId}"</c>.</summary>
    MemoryInventoryItemQuantity,

    /// <summary><c>SoulMemory.EldenRing.EldenRing.ReadNgLevel()</c> (Elden Ring Memory only).</summary>
    MemoryNgLevel,

    /// <summary>A named SoulMemory location state (DS1 bonfire state, or a location event flag in other games).</summary>
    MemoryLocationState,

    /// <summary>A boolean process/game state selected by <see cref="GameDataPoint.SourceId"/>.</summary>
    MemoryBooleanState,

    /// <summary>An integer process/game state selected by <see cref="GameDataPoint.SourceId"/>.</summary>
    MemoryIntegerState,

    /// <summary>A component of the player's current position selected by <see cref="GameDataPoint.SourceId"/>.</summary>
    MemoryPositionComponent,

    /// <summary>Elden Ring inventory presence. <see cref="GameDataPoint.SourceId"/> holds <c>"{Category}:{ItemId}"</c>; SoulMemory does not expose stack quantities.</summary>
    MemoryEldenRingInventoryItemPresence,
}

public enum MemoryBooleanStateSource
{
    PlayerLoaded,
    Loading,
    Blackscreen,
    CreditsRolling,
    WarpRequested,
    BitBlt,
}

public enum MemoryIntegerStateSource
{
    CurrentSaveSlot,
    ScreenState,
}

public enum MemoryPositionComponentSource
{
    X,
    Y,
    Z,
    Area,
    Block,
    Region,
    Size,
}

public static class GameDataComparisonOperators
{
    /// <summary>
    /// Comparisons meaningful for a 0/1 <see cref="GameDataValueKind.Flag"/>.
    /// Includes GreaterThan / GreaterThanOrEqual because existing predefined
    /// objectives encode "defeated" as <c>{"&gt;":[{"var":id},0]}</c>.
    /// </summary>
    public static readonly IReadOnlyList<GameDataComparisonOperator> FlagDefaults =
    [
        GameDataComparisonOperator.Equal,
        GameDataComparisonOperator.NotEqual,
        GameDataComparisonOperator.GreaterThan,
        GameDataComparisonOperator.GreaterThanOrEqual,
    ];

    public static readonly IReadOnlyList<GameDataComparisonOperator> NumericDefaults =
    [
        GameDataComparisonOperator.Equal,
        GameDataComparisonOperator.NotEqual,
        GameDataComparisonOperator.GreaterThan,
        GameDataComparisonOperator.GreaterThanOrEqual,
        GameDataComparisonOperator.LessThan,
        GameDataComparisonOperator.LessThanOrEqual,
    ];

    public static IReadOnlyList<GameDataComparisonOperator> DefaultsFor(GameDataValueKind kind) => kind switch
    {
        GameDataValueKind.Flag => FlagDefaults,
        _ => NumericDefaults,
    };

    public static string ToJsonLogicOperator(this GameDataComparisonOperator op) => op switch
    {
        GameDataComparisonOperator.Equal => "==",
        GameDataComparisonOperator.NotEqual => "!=",
        GameDataComparisonOperator.GreaterThan => ">",
        GameDataComparisonOperator.GreaterThanOrEqual => ">=",
        GameDataComparisonOperator.LessThan => "<",
        GameDataComparisonOperator.LessThanOrEqual => "<=",
        _ => throw new ArgumentOutOfRangeException(nameof(op), op, "Unknown comparison operator."),
    };
}
