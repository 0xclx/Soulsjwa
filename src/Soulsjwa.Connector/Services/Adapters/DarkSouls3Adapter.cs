using System;
using SoulMemory;
using SoulMemory.DarkSouls3;
using Attribute = SoulMemory.DarkSouls3.Attribute;
using Soulsjwa.Shared;

namespace Soulsjwa.Connector.Services.Adapters;

/// <summary>
/// Dark Souls III adapter.
///
/// <c>ReadAttribute</c> can legitimately return <c>-1</c> while a menu (e.g.
/// the inventory/status screen) is open — documented SoulMemory behavior, not
/// a read failure, which is why
/// <see cref="Soulsjwa.Shared.GameDataValueKind.Counter"/> allows negatives.
/// </summary>
public sealed class DarkSouls3Adapter : SoulMemoryReaderBase<IGame>
{
    private readonly Func<uint, bool> _readEventFlag;
    private readonly Func<int> _readGameTime;
    private readonly Func<Attribute, int> _readAttribute;
    private readonly Func<bool> _isLoading;
    private readonly Func<bool> _isPlayerLoaded;
    private readonly Func<bool> _isBlackscreenActive;
    private readonly Func<Vector3f> _readPosition;
    private Vector3f? _positionSnapshot;

    public DarkSouls3Adapter(DarkSouls3 game)
        : this(
            game,
            game.ReadEventFlag,
            game.GetInGameTimeMilliseconds,
            game.ReadAttribute,
            game.IsLoading,
            game.IsPlayerLoaded,
            game.BlackscreenActive,
            game.GetPosition)
    {
    }

    internal DarkSouls3Adapter(
        IGame game,
        Func<uint, bool> readEventFlag,
        Func<int> readGameTime,
        Func<Attribute, int> readAttribute,
        Func<bool>? isLoading = null,
        Func<bool>? isPlayerLoaded = null,
        Func<bool>? isBlackscreenActive = null,
        Func<Vector3f>? readPosition = null)
        : base(game)
    {
        _readEventFlag = readEventFlag;
        _readGameTime = readGameTime;
        _readAttribute = readAttribute;
        _isLoading = isLoading ?? (() => false);
        _isPlayerLoaded = isPlayerLoaded ?? (() => false);
        _isBlackscreenActive = isBlackscreenActive ?? (() => false);
        _readPosition = readPosition ?? (() => new Vector3f());
    }

    protected override void PrepareSnapshot()
    {
        _positionSnapshot = null;
        try
        {
            _positionSnapshot = _readPosition();
        }
        catch (Exception)
        {
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

            case GameDataReaderCapability.MemoryAttribute:
                if (!Enum.TryParse<Attribute>(dataPoint.SourceId, out var attribute)) return false;
                value = _readAttribute(attribute);
                return true;

            case GameDataReaderCapability.MemoryBooleanState:
                if (!Enum.TryParse<MemoryBooleanStateSource>(dataPoint.SourceId, out var booleanState)) return false;
                value = booleanState switch
                {
                    MemoryBooleanStateSource.Loading => _isLoading() ? 1L : 0L,
                    MemoryBooleanStateSource.PlayerLoaded => _isPlayerLoaded() ? 1L : 0L,
                    MemoryBooleanStateSource.Blackscreen => _isBlackscreenActive() ? 1L : 0L,
                    _ => 0L,
                };
                return booleanState is MemoryBooleanStateSource.Loading
                    or MemoryBooleanStateSource.PlayerLoaded
                    or MemoryBooleanStateSource.Blackscreen;

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
}
