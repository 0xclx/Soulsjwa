using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Newtonsoft.Json.Linq;

namespace Soulsjwa.Api.Features.Games.Services;

/// <summary>
/// Two things every rule consumer needs and used to do ad hoc: a canonical
/// form for comparing rule text, and parsed rules that are not re-parsed on
/// every submission.
/// </summary>
public static class RuleJson
{
    /// <summary>
    /// The rule's identity as text: object keys sorted, no insignificant
    /// whitespace. Rules are stored as <c>jsonb</c>, which Postgres normalises
    /// on the way in (key order, spacing), so a rule read back never equals
    /// the C# literal it was seeded from byte-for-byte. Comparing canonical
    /// forms answers "same rule" without a round trip to the database.
    /// Returns the input unchanged when it is not valid JSON.
    /// </summary>
    public static string Canonicalize(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var buffer = new StringBuilder(json.Length);
            Write(document.RootElement, buffer);
            return buffer.ToString();
        }
        catch (JsonException)
        {
            return json;
        }
    }

    private static void Write(JsonElement element, StringBuilder buffer)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                buffer.Append('{');
                var first = true;
                foreach (var property in element.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
                {
                    if (!first) buffer.Append(',');
                    first = false;
                    buffer.Append(JsonSerializer.Serialize(property.Name)).Append(':');
                    Write(property.Value, buffer);
                }
                buffer.Append('}');
                break;
            case JsonValueKind.Array:
                buffer.Append('[');
                var firstItem = true;
                foreach (var item in element.EnumerateArray())
                {
                    if (!firstItem) buffer.Append(',');
                    firstItem = false;
                    Write(item, buffer);
                }
                buffer.Append(']');
                break;
            default:
                buffer.Append(element.GetRawText());
                break;
        }
    }

    /// <summary>
    /// Bounded, content-addressed cache of parsed rules. Keyed by the rule
    /// text itself, so it needs no invalidation: a changed rule is a new key.
    /// Predefined objectives share their rule text across every event that
    /// imported them, which is why a single process-wide cache is enough.
    /// The returned <see cref="JObject"/> is shared and must be treated as
    /// read-only; JsonLogic.Net's <c>Apply</c> does not mutate it.
    /// </summary>
    public static class ParsedRuleCache
    {
        /// <summary>
        /// Above this many distinct rule texts the cache is simply cleared:
        /// the catalog is about a thousand rules, so this is only reached by
        /// a flood of one-off custom rules, and refilling is cheap.
        /// </summary>
        internal const int MaxEntries = 10_000;

        private static readonly ConcurrentDictionary<string, JObject> Entries = new(StringComparer.Ordinal);

        /// <summary>Parses on a miss; throws <see cref="Newtonsoft.Json.JsonException"/> for invalid JSON like <see cref="JObject.Parse(string)"/>.</summary>
        public static JObject Get(string ruleJson)
        {
            if (Entries.TryGetValue(ruleJson, out var cached))
                return cached;

            var parsed = JObject.Parse(ruleJson);
            if (Entries.Count >= MaxEntries)
                Entries.Clear();
            Entries.TryAdd(ruleJson, parsed);
            return parsed;
        }

        internal static int Count => Entries.Count;

        internal static void Clear() => Entries.Clear();
    }
}
