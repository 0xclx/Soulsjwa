using System.Globalization;

namespace Soulsjwa.Connector.Services.Adapters;

/// <summary>
/// Parses the numeric <see cref="Soulsjwa.Shared.GameDataPoint.SourceId"/>
/// strings (event-flag ids, boss type ids), so the decimal/hex convention
/// lives in one place.
/// </summary>
internal static class SourceIdParsing
{
    /// <summary>Parses a decimal (e.g. "16") or hex-prefixed (e.g. "0x10") integer.</summary>
    internal static bool TryParseLong(string text, out long value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var trimmed = text.Trim();
        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return long.TryParse(trimmed[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
        return long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>Parses a value produced by <see cref="TryParseLong"/> into a <see cref="uint"/> event-flag id, rejecting out-of-range values.</summary>
    internal static bool TryParseUInt32(string text, out uint value)
    {
        value = 0;
        if (!TryParseLong(text, out var asLong) || asLong < 0 || asLong > uint.MaxValue) return false;
        value = (uint)asLong;
        return true;
    }
}
