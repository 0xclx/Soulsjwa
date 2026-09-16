using System.Text;
using System.Text.Json;

namespace Soulsjwa.Api.Features.Games.Services;

/// <summary>
/// Validates <c>Objective.Rule</c>, <c>Objective.FailRule</c> and
/// <c>Objective.Metadata</c> before they reach the <c>jsonb</c> columns those
/// properties map to. Malformed JSON would otherwise surface as an uncaught
/// Postgres <c>22P02</c> error (a 500); a well-formed-but-nonsensical rule
/// would silently never complete during a live event.
/// </summary>
public static class ObjectiveRuleValidator
{
    public const int MaxRuleBytes = 32 * 1024;

    /// <summary>
    /// Null when acceptable, otherwise a message for a 400 ValidationProblem.
    /// </summary>
    public static string? ValidateMetadata(string? json)
    {
        if (string.IsNullOrEmpty(json))
            return null;

        try
        {
            using var _ = JsonDocument.Parse(json);
            return null;
        }
        catch (JsonException)
        {
            return "Must be valid JSON.";
        }
    }

    /// <summary>
    /// Null when acceptable, otherwise a message for a 400 ValidationProblem.
    /// The smoke evaluation against empty data rejects a rule that *throws* (e.g.
    /// a comparison operator missing an operand); one that merely evaluates to
    /// false is fine, since most rules don't match empty data.
    /// </summary>
    public static string? ValidateRule(string? json)
    {
        if (string.IsNullOrEmpty(json))
            return null;

        if (Encoding.UTF8.GetByteCount(json) > MaxRuleBytes)
            return $"Must be {MaxRuleBytes} bytes or fewer.";

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return "Must be a JSON object.";
        }
        catch (JsonException)
        {
            return "Must be valid JSON.";
        }

        try
        {
            RuleEvaluator.Evaluate(json, "{}");
        }
        catch (Exception)
        {
            // RuleEvaluator.Evaluate already swallows JsonException,
            // InvalidOperationException and ArgumentException by returning
            // false — anything that still reaches here is a structurally
            // broken rule (e.g. JsonLogic.Net indexing a missing operand).
            return "Not a valid rule expression.";
        }

        return null;
    }
}
