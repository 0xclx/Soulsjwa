using System.Globalization;
using System.Text.Json;
using Soulsjwa.Api.Features.Games.Definitions;
using Soulsjwa.Shared;

namespace Soulsjwa.Api.Features.Connector;

public static class ConnectorIngameTime
{
    /// <summary>
    /// SoulMemory-backed games report in-game time under a per-game prefixed id
    /// (e.g. <c>g1_game_time_ms</c>), already in milliseconds. Null when the data
    /// point is missing, unparseable, or the game has none.
    /// </summary>
    public static long? ExtractMilliseconds(string? submissionData, int? knownGameId)
    {
        if (string.IsNullOrWhiteSpace(submissionData) || !knownGameId.HasValue) return null;

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(submissionData);
        }
        catch (JsonException)
        {
            return null;
        }

        using (doc)
        {
            return ExtractMilliseconds(doc, knownGameId);
        }
    }

    /// <summary>
    /// Same extraction against an already-parsed document, so a submission's
    /// payload isn't parsed twice. Does not dispose it — the caller owns its
    /// lifetime.
    /// </summary>
    public static long? ExtractMilliseconds(JsonDocument document, int? knownGameId)
    {
        if (!knownGameId.HasValue) return null;
        if (document.RootElement.ValueKind != JsonValueKind.Object) return null;

        var msId = GameDataDefinitions.SoulMemoryGameTimeId(knownGameId.Value);
        if (document.RootElement.TryGetProperty(msId, out var ms))
        {
            return TryReadInt64(ms);
        }

        return null;
    }

    private static long? TryReadInt64(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Number when element.TryGetInt64(out var l) => l,
        JsonValueKind.Number when element.TryGetDouble(out var d) => (long)d,
        JsonValueKind.String when long.TryParse(
            element.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var l) => l,
        _ => null,
    };
}
