using System;
using SoulMemory;
using SoulMemory.EldenRing;
using Soulsjwa.Shared;
using EldenRingItemCategory = SoulMemory.EldenRing.Category;

namespace Soulsjwa.Connector.Services.Adapters;

/// <summary>
/// Elden Ring (Memory) adapter. Inventory is presence-only: SoulMemory's
/// public <see cref="Item"/> result carries category/id/name but no quantity.
///
/// Refreshing Elden Ring installs an in-game-time precision fix and injects the
/// <c>soulmods</c> helper module (also required for anti-cheat compatibility) —
/// upstream SoulMemory behavior triggered by attaching at all; this adapter
/// never calls a write API.
/// </summary>
public sealed class EldenRingMemoryAdapter : SoulMemoryReaderBase<IGame>
{
    private readonly Func<uint, bool> _readEventFlag;
    private readonly Func<int> _readGameTime;
    private readonly Func<int> _readNgLevel;
    private readonly Func<IReadOnlyList<Item>> _readInventory;
    private readonly Func<bool> _isPlayerLoaded;
    private readonly Func<bool> _isBlackscreenActive;
    private readonly Func<ScreenState> _readScreenState;
    private readonly Func<Position> _readPosition;
    private IReadOnlySet<(EldenRingItemCategory Category, uint Id)>? _inventorySnapshot;
    private Position? _positionSnapshot;

    public EldenRingMemoryAdapter(EldenRing game)
        : this(
            game,
            game.ReadEventFlag,
            game.GetInGameTimeMilliseconds,
            game.ReadNgLevel,
            game.ReadInventory,
            game.IsPlayerLoaded,
            game.IsBlackscreenActive,
            game.GetScreenState,
            game.GetPosition)
    {
    }

    internal EldenRingMemoryAdapter(
        IGame game,
        Func<uint, bool> readEventFlag,
        Func<int> readGameTime,
        Func<int> readNgLevel,
        Func<IReadOnlyList<Item>>? readInventory = null,
        Func<bool>? isPlayerLoaded = null,
        Func<bool>? isBlackscreenActive = null,
        Func<ScreenState>? readScreenState = null,
        Func<Position>? readPosition = null)
        : base(game)
    {
        _readEventFlag = readEventFlag;
        _readGameTime = readGameTime;
        _readNgLevel = readNgLevel;
        _readInventory = readInventory ?? (() => []);
        _isPlayerLoaded = isPlayerLoaded ?? (() => false);
        _isBlackscreenActive = isBlackscreenActive ?? (() => false);
        _readScreenState = readScreenState ?? (() => ScreenState.Unknown);
        _readPosition = readPosition ?? (() => new Position());
    }

    protected override void PrepareSnapshot()
    {
        _inventorySnapshot = null;
        _positionSnapshot = null;
        try
        {
            _inventorySnapshot = _readInventory()
                .Select(item => (item.Category, item.Id))
                .ToHashSet();
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

    protected override bool TryReadValue(GameDataPoint dataPoint, out object value)
    {
        value = 0;
        switch (dataPoint.ReaderCapability)
        {
            case GameDataReaderCapability.MemoryEventFlag:
                if (!SourceIdParsing.TryParseUInt32(dataPoint.SourceId, out var flag)) return false;
                value = _readEventFlag(flag) ? 1 : 0;
                return true;

            case GameDataReaderCapability.MemoryInGameTimeMilliseconds:
                value = _readGameTime();
                return true;

            case GameDataReaderCapability.MemoryNgLevel:
                value = _readNgLevel();
                return true;

            case GameDataReaderCapability.MemoryEldenRingInventoryItemPresence:
                if (_inventorySnapshot is null
                    || !TryParseItemKey(dataPoint.SourceId, out var category, out var itemId))
                    return false;
                value = _inventorySnapshot.Contains((category, itemId)) ? 1L : 0L;
                return true;

            case GameDataReaderCapability.MemoryBooleanState:
                if (!Enum.TryParse<MemoryBooleanStateSource>(dataPoint.SourceId, out var booleanState)) return false;
                value = booleanState switch
                {
                    MemoryBooleanStateSource.PlayerLoaded => _isPlayerLoaded() ? 1L : 0L,
                    MemoryBooleanStateSource.Blackscreen => _isBlackscreenActive() ? 1L : 0L,
                    _ => 0L,
                };
                return booleanState is MemoryBooleanStateSource.PlayerLoaded
                    or MemoryBooleanStateSource.Blackscreen;

            case GameDataReaderCapability.MemoryIntegerState:
                if (!Enum.TryParse<MemoryIntegerStateSource>(dataPoint.SourceId, out var integerState)
                    || integerState != MemoryIntegerStateSource.ScreenState)
                    return false;
                value = (int)_readScreenState();
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
                    MemoryPositionComponentSource.Area => _positionSnapshot.Area,
                    MemoryPositionComponentSource.Block => _positionSnapshot.Block,
                    MemoryPositionComponentSource.Region => _positionSnapshot.Region,
                    MemoryPositionComponentSource.Size => _positionSnapshot.Size,
                    _ => 0,
                };
                return true;

            default:
                return false;
        }
    }

    private static bool TryParseItemKey(
        string sourceId,
        out EldenRingItemCategory category,
        out uint itemId)
    {
        category = default;
        itemId = 0;
        var separatorIndex = sourceId.IndexOf(':');
        return separatorIndex > 0
            && Enum.TryParse(sourceId[..separatorIndex], out category)
            && uint.TryParse(sourceId[(separatorIndex + 1)..], out itemId);
    }
}
