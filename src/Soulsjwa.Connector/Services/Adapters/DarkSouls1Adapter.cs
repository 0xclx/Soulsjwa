using System;
using System.Collections.Generic;
using System.Globalization;
using SoulMemory;
using SoulMemory.DarkSouls1;
using Attribute = SoulMemory.DarkSouls1.Attribute;
using Soulsjwa.Shared;

namespace Soulsjwa.Connector.Services.Adapters;

/// <summary>
/// Dark Souls: Remastered adapter. Inventory quantities for the whole
/// <see cref="Item.AllItems"/> catalog come from a single
/// <see cref="Remastered.GetInventory"/> snapshot per poll.
/// </summary>
public sealed class DarkSouls1Adapter : SoulMemoryReaderBase<IGame>
{
    private readonly Func<uint, bool> _readEventFlag;
    private readonly Func<Attribute, int> _readAttribute;
    private readonly Func<int> _readGameTime;
    private readonly Func<int> _readNgCount;
    private readonly Func<int> _readPlayerHealth;
    private readonly Func<IReadOnlyDictionary<(ItemCategory Category, int Id), int>> _readInventory;
    private readonly Func<Bonfire, BonfireState> _readBonfireState;
    private readonly Func<bool> _isPlayerLoaded;
    private readonly Func<bool> _areCreditsRolling;
    private readonly Func<bool> _isWarpRequested;
    private readonly Func<int> _readCurrentSaveSlot;
    private readonly Func<Vector3f> _readPosition;
    private IReadOnlyDictionary<(ItemCategory Category, int Id), int>? _inventorySnapshot;
    private Vector3f? _positionSnapshot;

    public DarkSouls1Adapter(Remastered game)
        : this(
            game,
            game.ReadEventFlag,
            game.GetAttribute,
            game.GetInGameTimeMilliseconds,
            game.NgCount,
            game.GetPlayerHealth,
            () => SnapshotInventory(game),
            game.GetBonfireState,
            game.IsPlayerLoaded,
            game.AreCreditsRolling,
            game.IsWarpRequested,
            game.GetCurrentSaveSlot,
            game.GetPosition)
    {
    }

    internal DarkSouls1Adapter(
        IGame game,
        Func<uint, bool> readEventFlag,
        Func<Attribute, int> readAttribute,
        Func<int> readGameTime,
        Func<int> readNgCount,
        Func<int> readPlayerHealth,
        Func<IReadOnlyDictionary<(ItemCategory Category, int Id), int>> readInventory,
        Func<Bonfire, BonfireState>? readBonfireState = null,
        Func<bool>? isPlayerLoaded = null,
        Func<bool>? areCreditsRolling = null,
        Func<bool>? isWarpRequested = null,
        Func<int>? readCurrentSaveSlot = null,
        Func<Vector3f>? readPosition = null)
        : base(game)
    {
        _readEventFlag = readEventFlag;
        _readAttribute = readAttribute;
        _readGameTime = readGameTime;
        _readNgCount = readNgCount;
        _readPlayerHealth = readPlayerHealth;
        _readInventory = readInventory;
        _readBonfireState = readBonfireState ?? (_ => BonfireState.Unknown);
        _isPlayerLoaded = isPlayerLoaded ?? (() => false);
        _areCreditsRolling = areCreditsRolling ?? (() => false);
        _isWarpRequested = isWarpRequested ?? (() => false);
        _readCurrentSaveSlot = readCurrentSaveSlot ?? (() => -1);
        _readPosition = readPosition ?? (() => new Vector3f());
    }

    /// <summary>
    /// One <see cref="Remastered.GetInventory"/> call per poll, regardless of
    /// how many inventory data points are requested.
    /// </summary>
    protected override void PrepareSnapshot()
    {
        _inventorySnapshot = null;
        _positionSnapshot = null;
        try
        {
            _inventorySnapshot = _readInventory();
        }
        catch (Exception)
        {
            // Position/state reads should survive a transient inventory failure.
        }

        try
        {
            _positionSnapshot = _readPosition();
        }
        catch (Exception)
        {
            // Inventory/event reads should survive a transient position failure.
        }
    }

    private static IReadOnlyDictionary<(ItemCategory Category, int Id), int> SnapshotInventory(Remastered game)
    {
        var snapshot = new Dictionary<(ItemCategory, int), int>();
        foreach (var item in game.GetInventory())
        {
            var key = (item.Category, item.Id);
            snapshot[key] = snapshot.GetValueOrDefault(key) + item.Quantity;
        }
        return snapshot;
    }

    protected override bool TryReadValue(GameDataPoint dataPoint, out object value)
    {
        value = 0;
        switch (dataPoint.ReaderCapability)
        {
            case GameDataReaderCapability.MemoryEventFlag:
                if (!SourceIdParsing.TryParseUInt32(dataPoint.SourceId, out var flag)) return false;
                value = _readEventFlag(flag) ? 1 : 0;
                return true;

            case GameDataReaderCapability.MemoryAttribute:
                if (!Enum.TryParse<Attribute>(dataPoint.SourceId, out var attribute)) return false;
                value = _readAttribute(attribute);
                return true;

            case GameDataReaderCapability.MemoryInGameTimeMilliseconds:
                value = _readGameTime();
                return true;

            case GameDataReaderCapability.MemoryNgCount:
                value = _readNgCount();
                return true;

            case GameDataReaderCapability.MemoryPlayerHealth:
                value = _readPlayerHealth();
                return true;

            case GameDataReaderCapability.MemoryInventoryItemQuantity:
                // Absent from the snapshot means "0 held", not "unknown":
                // rules check for possession, so always report a value.
                if (_inventorySnapshot is null || !TryParseItemKey(dataPoint.SourceId, out var category, out var itemId))
                    return false;
                value = _inventorySnapshot.TryGetValue((category, itemId), out var quantity) ? quantity : 0;
                return true;

            case GameDataReaderCapability.MemoryLocationState:
                if (!Enum.TryParse<Bonfire>(dataPoint.SourceId, out var bonfire)) return false;
                value = (int)_readBonfireState(bonfire);
                return true;

            case GameDataReaderCapability.MemoryBooleanState:
                if (!Enum.TryParse<MemoryBooleanStateSource>(dataPoint.SourceId, out var booleanState)) return false;
                value = booleanState switch
                {
                    MemoryBooleanStateSource.PlayerLoaded => _isPlayerLoaded() ? 1L : 0L,
                    MemoryBooleanStateSource.CreditsRolling => _areCreditsRolling() ? 1L : 0L,
                    MemoryBooleanStateSource.WarpRequested => _isWarpRequested() ? 1L : 0L,
                    _ => 0L,
                };
                return booleanState is MemoryBooleanStateSource.PlayerLoaded
                    or MemoryBooleanStateSource.CreditsRolling
                    or MemoryBooleanStateSource.WarpRequested;

            case GameDataReaderCapability.MemoryIntegerState:
                if (!Enum.TryParse<MemoryIntegerStateSource>(dataPoint.SourceId, out var integerState)
                    || integerState != MemoryIntegerStateSource.CurrentSaveSlot)
                    return false;
                value = _readCurrentSaveSlot();
                return true;

            case GameDataReaderCapability.MemoryPositionComponent:
                if (_positionSnapshot is null
                    || !Enum.TryParse<MemoryPositionComponentSource>(dataPoint.SourceId, out var component))
                    return false;
                value = component switch
                {
                    MemoryPositionComponentSource.X => _positionSnapshot.X,
                    MemoryPositionComponentSource.Y => _positionSnapshot.Y,
                    MemoryPositionComponentSource.Z => _positionSnapshot.Z,
                    _ => 0f,
                };
                return component is MemoryPositionComponentSource.X
                    or MemoryPositionComponentSource.Y
                    or MemoryPositionComponentSource.Z;

            default:
                return false;
        }
    }

    /// <summary>Parses the <c>"{ItemCategory}:{ItemId}"</c> composite <see cref="GameDataPoint.SourceId"/> produced by <c>GameDataDefinitions</c>.</summary>
    private static bool TryParseItemKey(string sourceId, out ItemCategory category, out int itemId)
    {
        category = default;
        itemId = 0;
        var separatorIndex = sourceId.IndexOf(':');
        if (separatorIndex < 0) return false;

        var categoryText = sourceId[..separatorIndex];
        var idText = sourceId[(separatorIndex + 1)..];
        return Enum.TryParse(categoryText, out category)
            && int.TryParse(idText, NumberStyles.Integer, CultureInfo.InvariantCulture, out itemId);
    }
}
