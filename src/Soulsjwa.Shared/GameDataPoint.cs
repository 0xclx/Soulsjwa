namespace Soulsjwa.Shared;

/// <summary>
/// A single data point the connector reads from the running game's process
/// memory (via SoulMemory). Lives in the shared assembly so the API
/// (which serves them) and the connector (which consumes them) bind to the
/// exact same shape.
///
/// Backward compatibility: <see cref="Offset"/> and <see cref="DataType"/> are
/// the original (legacy) fields, kept as-is so existing readers and serialized
/// payloads keep working unmodified. Every field below them is new named, typed
/// metadata.
/// </summary>
/// <param name="Id">Stable identifier used to report state (e.g. "111", "g1_f16").</param>
/// <param name="Offset">
/// Legacy positional value: a repurposed "key" string (flag id, boss type id,
/// attribute name, "{category}:{id}" for inventory items, ...) for
/// SoulMemory-backed games. Kept for backward compatibility — new adapters
/// should prefer <see cref="SourceId"/>.
/// </param>
/// <param name="DataType">
/// Legacy free-string data type/marker (e.g. "event_flag", "boss_kill_count",
/// "attribute", "ng_count", "inventory_item_quantity"). Kept for backward
/// compatibility — new adapters should prefer <see cref="ReaderCapability"/>.
/// </param>
/// <param name="ValueKind">Shape of the value — drives default allowed comparisons and submission validation.</param>
/// <param name="ReaderCapability">
/// Typed identifier of the read operation an adapter performs to produce this
/// value. Replaces switching on the legacy <paramref name="DataType"/> string.
/// </param>
/// <param name="SourceId">
/// Semantic source identifier an adapter parses to perform the read (e.g. an
/// Attribute enum member name, or "{ItemCategory}:{ItemId}"). Decoupled from
/// the legacy <paramref name="Offset"/> so its meaning never has to be inferred
/// from a byte-offset-shaped string; defaults to it when not provided.
/// </param>
/// <param name="AllowedComparisons">
/// Comparison operators meaningful for this point's <paramref name="ValueKind"/>.
/// Defaults to <see cref="GameDataComparisonOperators.DefaultsFor"/>.
/// </param>
public sealed record GameDataPoint(
    string Id,
    string DisplayName,
    string Offset,
    string DataType,
    string Description = "",
    string Unit = "",
    GameDataCategory Category = GameDataCategory.Progression,
    GameDataValueKind ValueKind = GameDataValueKind.Counter,
    GameDataReaderCapability ReaderCapability = GameDataReaderCapability.MemoryEventFlag,
    string? SourceId = null,
    IReadOnlyList<GameDataComparisonOperator>? AllowedComparisons = null)
{
    /// <inheritdoc cref="GameDataPoint" path="/param[@name='SourceId']"/>
    public string SourceId { get; init; } = SourceId ?? Offset;

    /// <inheritdoc cref="GameDataPoint" path="/param[@name='AllowedComparisons']"/>
    public IReadOnlyList<GameDataComparisonOperator> AllowedComparisons { get; init; } =
        AllowedComparisons ?? GameDataComparisonOperators.DefaultsFor(ValueKind);
}
