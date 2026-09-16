using System;
using SoulMemory;
using SoulMemory.Sekiro;
using Attribute = SoulMemory.Sekiro.Attribute;
using Soulsjwa.Shared;

namespace Soulsjwa.Connector.Services.Adapters;

/// <summary>
/// Sekiro: Shadows Die Twice adapter. SoulMemory exposes only two attributes
/// here, Vitality and Attack Power; <c>GetAttribute</c> throws
/// <see cref="ArgumentException"/> for anything else, which the base
/// <see cref="SoulMemoryReaderBase{TGame}.Read"/> loop isolates per data point.
///
/// Refreshing Sekiro installs "no logo" / "no tutorial" mods and an
/// in-game-time precision fix — upstream SoulMemory behavior triggered by
/// attaching at all; this adapter never calls a write API.
/// </summary>
public sealed class SekiroAdapter : SoulMemoryReaderBase<IGame>
{
    private readonly Func<uint, bool> _readEventFlag;
    private readonly Func<int> _readGameTime;
    private readonly Func<Attribute, int> _readAttribute;
    private readonly Func<bool> _isPlayerLoaded;
    private readonly Func<bool> _isBlackscreenActive;
    private readonly Func<bool> _isBitBlt;
    private readonly Func<Vector3f> _readPosition;
    private Vector3f? _positionSnapshot;

    public SekiroAdapter(Sekiro game)
        : this(
            game,
            game.ReadEventFlag,
            game.GetInGameTimeMilliseconds,
            game.GetAttribute,
            game.IsPlayerLoaded,
            game.IsBlackscreenActive,
            () => game.BitBlt,
            game.GetPlayerPosition)
    {
    }

    internal SekiroAdapter(
        IGame game,
        Func<uint, bool> readEventFlag,
        Func<int> readGameTime,
        Func<Attribute, int> readAttribute,
        Func<bool>? isPlayerLoaded = null,
        Func<bool>? isBlackscreenActive = null,
        Func<bool>? isBitBlt = null,
        Func<Vector3f>? readPosition = null)
        : base(game)
    {
        _readEventFlag = readEventFlag;
        _readGameTime = readGameTime;
        _readAttribute = readAttribute;
        _isPlayerLoaded = isPlayerLoaded ?? (() => false);
        _isBlackscreenActive = isBlackscreenActive ?? (() => false);
        _isBitBlt = isBitBlt ?? (() => false);
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
                    MemoryBooleanStateSource.PlayerLoaded => _isPlayerLoaded() ? 1L : 0L,
                    MemoryBooleanStateSource.Blackscreen => _isBlackscreenActive() ? 1L : 0L,
                    MemoryBooleanStateSource.BitBlt => _isBitBlt() ? 1L : 0L,
                    _ => 0L,
                };
                return booleanState is MemoryBooleanStateSource.PlayerLoaded
                    or MemoryBooleanStateSource.Blackscreen
                    or MemoryBooleanStateSource.BitBlt;

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
