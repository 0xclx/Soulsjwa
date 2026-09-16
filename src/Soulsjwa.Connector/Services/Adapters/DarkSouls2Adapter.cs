using System;
using SoulMemory;
using SoulMemory.DarkSouls2;
using Attribute = SoulMemory.DarkSouls2.Attribute;
using Soulsjwa.Shared;

namespace Soulsjwa.Connector.Services.Adapters;

/// <summary>
/// Dark Souls II: Scholar of the First Sin adapter (auto-resolves
/// Vanilla/Scholar via <see cref="DarkSouls2"/>). Boss progress comes from
/// NG-cycle-aware <c>GetBossKillCount</c>.
///
/// Deliberately does NOT read in-game time or event flags: SoulMemory 1.8.5's
/// <c>GetInGameTimeMilliseconds()</c> is hardcoded to return 0 for both
/// Vanilla and Scholar, and <c>ReadEventFlag</c> is not reliable for DS2 (per
/// upstream README).
/// </summary>
public sealed class DarkSouls2Adapter : SoulMemoryReaderBase<IGame>
{
    private readonly Func<BossType, int> _readBossKillCount;
    private readonly Func<Attribute, int> _readAttribute;
    private readonly Func<bool> _isLoading;
    private readonly Func<Vector3f> _readPosition;
    private Vector3f? _positionSnapshot;

    public DarkSouls2Adapter(DarkSouls2 game)
        : this(game, game.GetBossKillCount, game.GetAttribute, game.IsLoading, game.GetPosition)
    {
    }

    internal DarkSouls2Adapter(
        IGame game,
        Func<BossType, int> readBossKillCount,
        Func<Attribute, int> readAttribute,
        Func<bool>? isLoading = null,
        Func<Vector3f>? readPosition = null)
        : base(game)
    {
        _readBossKillCount = readBossKillCount;
        _readAttribute = readAttribute;
        _isLoading = isLoading ?? (() => false);
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
            case GameDataReaderCapability.MemoryBossKillCount:
                if (!SourceIdParsing.TryParseLong(dataPoint.SourceId, out var bossTypeId)
                    || bossTypeId < int.MinValue || bossTypeId > int.MaxValue)
                    return false;
                value = _readBossKillCount((BossType)(int)bossTypeId);
                return true;

            case GameDataReaderCapability.MemoryAttribute:
                if (!Enum.TryParse<Attribute>(dataPoint.SourceId, out var attribute)) return false;
                value = _readAttribute(attribute);
                return true;

            case GameDataReaderCapability.MemoryBooleanState:
                if (!Enum.TryParse<MemoryBooleanStateSource>(dataPoint.SourceId, out var booleanState)
                    || booleanState != MemoryBooleanStateSource.Loading)
                    return false;
                value = _isLoading() ? 1L : 0L;
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
}
