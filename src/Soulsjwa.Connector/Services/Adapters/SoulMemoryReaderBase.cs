using System;
using System.Collections.Generic;
using System.Text.Json;
using SoulMemory;
using Soulsjwa.Shared;

namespace Soulsjwa.Connector.Services.Adapters;

/// <summary>
/// Adds attachment status to <see cref="IGameDataReader"/> so the UI can
/// distinguish "game not running" from "nothing to report".
/// </summary>
public interface ISoulMemoryGameAdapter : IGameDataReader
{
    /// <summary>True iff the most recent <see cref="IGameDataReader.Read"/> call successfully attached to the running game.</summary>
    bool IsAttached { get; }

    /// <summary>Most recent <c>TryRefresh</c> error message, or null on success.</summary>
    string? LastRefreshError { get; }
}

/// <summary>
/// Common base for the read-only SoulMemory adapters, one subclass per game.
/// Each subclass switches on the typed
/// <see cref="GameDataPoint.ReaderCapability"/> and implements only the
/// capabilities its game actually supports.
///
/// Process attachment: <see cref="IGame.TryRefresh"/> runs on every
/// <see cref="Read"/>, so the reader recovers transparently when the player
/// launches or restarts the game between submissions. A failed attach returns
/// an empty JSON object rather than throwing, so downstream JsonLogic rules
/// evaluate to false and the user gets a status message instead of a crash.
///
/// Per-data-point failure isolation: each data point is read in its own
/// try/catch, so one bad read (an unsupported attribute, a transient failure
/// while a scene unloads) is omitted instead of failing the whole submission.
///
/// This reader never writes to game memory — subclasses must only call
/// read-only SoulMemory APIs. Some games' <c>TryRefresh</c> does itself install
/// one-time patches, upstream SoulMemory behavior triggered by attaching at
/// all: Sekiro installs "no logo" / "no tutorial" mods and an in-game-time
/// precision fix; Elden Ring installs an in-game-time precision fix and injects
/// the <c>soulmods</c> helper module. DS1 / DS2 / DS3 do nothing beyond
/// attaching.
/// </summary>
public abstract class SoulMemoryReaderBase<TGame> : ISoulMemoryGameAdapter where TGame : IGame
{
    protected readonly TGame Game;

    protected SoulMemoryReaderBase(TGame game)
    {
        Game = game ?? throw new ArgumentNullException(nameof(game));
    }

    public bool IsAttached { get; private set; }

    public string? LastRefreshError { get; private set; }

    public string Read(IReadOnlyList<GameDataPoint> dataPoints)
    {
        ArgumentNullException.ThrowIfNull(dataPoints);

        var values = new Dictionary<string, JsonElement>(StringComparer.Ordinal);

        var refresh = Game.TryRefresh();
        if (refresh.IsErr)
        {
            var err = refresh.GetErr();
            IsAttached = false;
            LastRefreshError = $"{err.Reason}: {err.Message}";
            return JsonSerializer.Serialize(values);
        }

        IsAttached = true;
        LastRefreshError = null;

        // Some capabilities (e.g. DS1 inventory) are cheaper and more internally
        // consistent read once per poll than once per data point.
        try
        {
            PrepareSnapshot();
        }
        catch (Exception)
        {
            // A failed bulk read must not stop unrelated data points such as
            // flags or time from being reported.
        }

        foreach (var dp in dataPoints)
        {
            try
            {
                if (TryReadValue(dp, out var value))
                    values[dp.Id] = JsonSerializer.SerializeToElement(value);
            }
            catch (Exception)
            {
                // Best-effort: an unsupported data point or a transient memory
                // read failure must not fail the whole submission.
            }
        }

        return JsonSerializer.Serialize(values);
    }

    /// <summary>
    /// Called once per <see cref="Read"/>, after a successful attach and before
    /// any data point is read. Override to snapshot state that would otherwise
    /// be re-fetched per data point (e.g. the DS1 inventory list).
    /// </summary>
    protected virtual void PrepareSnapshot()
    {
    }

    /// <summary>
    /// Implementations switch on <see cref="GameDataPoint.ReaderCapability"/>
    /// (not the legacy <see cref="GameDataPoint.DataType"/>) and read the value
    /// named by <see cref="GameDataPoint.SourceId"/>. Return false when this
    /// game does not support the capability — <see cref="Read"/> then omits the
    /// data point from the payload.
    /// </summary>
    protected abstract bool TryReadValue(GameDataPoint dataPoint, out object value);
}
