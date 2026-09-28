namespace Soulsjwa.Api.Common;

/// <summary>
/// Converts an inbound timestamp to a UTC <see cref="DateTime"/> for a
/// <c>timestamp with time zone</c> column. Request DTOs should use
/// <see cref="DateTimeOffset"/>: an offset-bearing value round-trips exactly,
/// and <see cref="UtcJsonConverters"/> binds a zone-less wire value as UTC —
/// never with the server's local offset. Use the <see cref="DateTimeOffset"/>
/// overload at every API boundary; the <see cref="DateTime"/> one is only for
/// values already in memory as a <see cref="DateTime"/> that need
/// re-normalising.
/// </summary>
public static class UtcTime
{
    public static DateTime ToStorage(DateTimeOffset value) => value.UtcDateTime;

    public static DateTime ToStorage(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => throw new ArgumentException(
            $"DateTime must have Kind Utc or Local, not Unspecified — the caller must know which zone it's in. Value: {value:O}",
            nameof(value)),
    };
}
