using System.Collections.Generic;
using Soulsjwa.Shared;

namespace Soulsjwa.Connector.Services;

/// <summary>
/// Unified read interface for all connector-supported (SoulMemory,
/// live-process) game data sources.
/// </summary>
public interface IGameDataReader
{
    /// <summary>
    /// Returns a JSON object string mapping data-point ids to their parsed
    /// values (e.g. <c>{"g3_f14000800":1,"g3_game_time_ms":123456}</c>).
    /// Unsupported data points are skipped, so JsonLogic rules referencing them
    /// evaluate to false rather than mis-reporting kills.
    /// </summary>
    string Read(IReadOnlyList<GameDataPoint> dataPoints);
}
