using System;
using SoulMemory.DarkSouls1;
using SoulMemory.DarkSouls2;
using SoulMemory.DarkSouls3;
using SoulMemory.EldenRing;
using SoulMemory.Sekiro;
using Soulsjwa.Connector.Services.Adapters;
using Soulsjwa.Shared;

namespace Soulsjwa.Connector.Services;

/// <summary>
/// Routes a supported game id to its <see cref="IGameDataReader"/>. Adapters
/// are constructed up front, which does not attach to a running game —
/// attachment only happens when a read triggers <c>TryRefresh</c>.
/// </summary>
public sealed class GameDataReaderFactory
{
    private readonly IGameDataReader _darkSouls1Reader = new DarkSouls1Adapter(new Remastered());
    private readonly IGameDataReader _darkSouls2Reader = new DarkSouls2Adapter(new DarkSouls2());
    private readonly IGameDataReader _darkSouls3Reader = new DarkSouls3Adapter(new DarkSouls3());
    private readonly IGameDataReader _sekiroReader = new SekiroAdapter(new Sekiro());
    private readonly IGameDataReader _eldenRingMemoryReader = new EldenRingMemoryAdapter(new EldenRing());

    /// <summary>
    /// Adapters are reused for the factory's lifetime: every read still calls
    /// <c>TryRefresh</c>, but keeping the instance preserves attachment/error
    /// state and avoids rebuilding SoulMemory pointer trees on every poll.
    /// </summary>
    public IGameDataReader Create(int gameId) => gameId switch
    {
        GameIds.DarkSouls1Remastered => _darkSouls1Reader,
        GameIds.DarkSouls2Scholar => _darkSouls2Reader,
        GameIds.DarkSouls3 => _darkSouls3Reader,
        GameIds.Sekiro => _sekiroReader,
        GameIds.EldenRingMemory => _eldenRingMemoryReader,

        _ => throw new ArgumentOutOfRangeException(nameof(gameId), gameId, "Unsupported game id."),
    };
}
