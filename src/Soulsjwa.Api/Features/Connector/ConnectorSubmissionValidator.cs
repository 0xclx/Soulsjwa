using System.Text;
using System.Text.Json;
using Soulsjwa.Api.Features.Games.Definitions;
using Soulsjwa.Shared;

namespace Soulsjwa.Api.Features.Connector;

/// <summary>
/// Validates a connector submission payload before
/// <see cref="Soulsjwa.Api.Features.Games.Services.RuleEvaluator"/> sees it, so a
/// bad payload is rejected with 400 rather than silently making every rule that
/// references the bad data evaluate to false.
/// </summary>
public static class ConnectorSubmissionValidator
{
    /// <summary>
    /// Bounds an oversized body before <see cref="JsonDocument.Parse"/> is ever
    /// reached. The connector submits a game's entire catalog on each read, not
    /// just the data points an event's objectives reference, so this is sized
    /// against the largest catalog (Elden Ring Memory, ~7,550 points ≈ 310 KiB)
    /// rather than a "typical" payload.
    /// </summary>
    public const int MaxDataBytes = 512 * 1024;

    /// <summary>
    /// Backstop for custom games, which skip the catalog check entirely (a known
    /// game's catalog already constrains its key set).
    /// </summary>
    public const int MaxDataPoints = 8192;

    /// <summary>
    /// With a <paramref name="knownGameId"/>, every property key must be an id
    /// advertised by <see cref="GameDataDefinitions.ForGame"/> and flag-kind
    /// points are constrained to 0/1. With null (custom game, no catalog) only
    /// the game-agnostic shape checks run.
    /// </summary>
    public static Dictionary<string, string[]> Validate(string? submissionData, int? knownGameId)
    {
        var errors = Validate(submissionData, knownGameId, out var document);
        document?.Dispose();
        return errors;
    }

    /// <summary>
    /// Same validation, but hands the parsed <paramref name="document"/> back so
    /// a caller that also needs the parsed shape doesn't parse the payload twice.
    /// It is <c>null</c> when validation failed; otherwise the caller owns
    /// disposing it.
    /// </summary>
    public static Dictionary<string, string[]> Validate(string? submissionData, int? knownGameId, out JsonDocument? document)
    {
        document = null;
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(submissionData))
        {
            errors["Data"] = ["Data is required."];
            return errors;
        }

        if (Encoding.UTF8.GetByteCount(submissionData) > MaxDataBytes)
        {
            errors["Data"] = [$"Data must be {MaxDataBytes:N0} bytes or fewer."];
            return errors;
        }

        try
        {
            document = JsonDocument.Parse(submissionData);
        }
        catch (JsonException)
        {
            errors["Data"] = ["Data must be valid JSON."];
            return errors;
        }

        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            errors["Data"] = ["Data must be a JSON object."];
            document.Dispose();
            document = null;
            return errors;
        }

        if (document.RootElement.EnumerateObject().Count() > MaxDataPoints)
        {
            errors["Data"] = [$"Data must contain {MaxDataPoints:N0} properties or fewer."];
            document.Dispose();
            document = null;
            return errors;
        }

        // Custom games have no server-side catalog to check ids against, so they
        // skip the catalog + flag-range checks and stay compatible with
        // arbitrary user-defined data-point ids.
        var catalog = knownGameId.HasValue
            ? GameDataDefinitions.ForGame(knownGameId.Value).ToDictionary(dp => dp.Id, StringComparer.Ordinal)
            : null;

        foreach (var property in document.RootElement.EnumerateObject())
        {
            GameDataPoint? dataPoint = null;
            if (catalog is not null)
            {
                if (!catalog.TryGetValue(property.Name, out var known))
                {
                    errors[property.Name] = [$"'{property.Name}' is not a known data point for this game."];
                    continue;
                }
                dataPoint = known;
            }

            if (dataPoint is { ValueKind: GameDataValueKind.Decimal })
            {
                if (property.Value.ValueKind != JsonValueKind.Number
                    || !property.Value.TryGetDouble(out var decimalNumber)
                    || !double.IsFinite(decimalNumber))
                {
                    errors[property.Name] = [$"'{property.Name}' must be a finite number."];
                }
                continue;
            }

            if (property.Value.ValueKind != JsonValueKind.Number || !property.Value.TryGetInt64(out var number))
            {
                errors[property.Name] = [$"'{property.Name}' must be a finite integer number."];
                continue;
            }

            if (dataPoint is { ValueKind: GameDataValueKind.Flag } && number is not (0 or 1))
            {
                errors[property.Name] = [$"'{property.Name}' is a flag and must be 0 or 1."];
            }
        }

        if (errors.Count > 0)
        {
            document.Dispose();
            document = null;
        }

        return errors;
    }
}
