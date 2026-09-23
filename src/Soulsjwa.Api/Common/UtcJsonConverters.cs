using System.Text.Json;
using System.Text.Json.Serialization;

namespace Soulsjwa.Api.Common;

/// <summary>
/// Every time the API reads or writes over JSON is UTC. System.Text.Json on
/// its own reads a zone-less timestamp as the machine's local time (for
/// <see cref="DateTimeOffset"/>) or as <see cref="DateTimeKind.Unspecified"/>
/// (for <see cref="DateTime"/>); these converters read it as UTC instead, so
/// the server's time zone never changes what a request means. Parsing stays
/// System.Text.Json's strict ISO 8601. Only browsers show local time.
/// </summary>
public static class UtcJsonConverters
{
    public static void AddTo(IList<JsonConverter> converters)
    {
        converters.Add(new UtcDateTimeOffsetConverter());
        converters.Add(new UtcDateTimeConverter());
    }

    /// <summary>Reads a zone-less value as UTC; keeps an explicit offset; writes UTC.</summary>
    private sealed class UtcDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
    {
        public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String || !reader.TryGetDateTimeOffset(out var value))
                throw new JsonException("Expected an ISO 8601 timestamp.");
            // TryGetDateTime reports a zone-less string as Unspecified, the one
            // case TryGetDateTimeOffset would have given the local offset.
            return reader.TryGetDateTime(out var wallClock) && wallClock.Kind == DateTimeKind.Unspecified
                ? new DateTimeOffset(wallClock, TimeSpan.Zero)
                : value;
        }

        public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.ToUniversalTime());
    }

    /// <summary>Reads every form as a UTC-kind instant; writes UTC with a Z.</summary>
    private sealed class UtcDateTimeConverter : JsonConverter<DateTime>
    {
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String || !reader.TryGetDateTime(out var value))
                throw new JsonException("Expected an ISO 8601 timestamp.");
            return value.Kind switch
            {
                DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
                DateTimeKind.Utc => value,
                // An explicit offset: take the instant from the offset form
                // rather than round-tripping through local time.
                _ => reader.TryGetDateTimeOffset(out var instant)
                    ? instant.UtcDateTime
                    : value.ToUniversalTime(),
            };
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.Kind switch
            {
                DateTimeKind.Local => value.ToUniversalTime(),
                DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
                _ => value,
            });
    }
}
