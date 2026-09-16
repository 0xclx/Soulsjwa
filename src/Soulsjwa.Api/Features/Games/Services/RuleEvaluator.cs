using JsonLogic.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Soulsjwa.Api.Diagnostics;

namespace Soulsjwa.Api.Features.Games.Services;

public static class RuleEvaluator
{
    /// <summary>
    /// Variable name fail rules use for the number of OTHER event competitors who
    /// have already completed the objective. Injected into the evaluation data
    /// alongside the connector's submitted fields; never present in the payload.
    /// </summary>
    public const string CompetitorCompletionsVariable = "competitorCompletions";

    private static readonly JsonLogicEvaluator Evaluator = new(EvaluateOperators.Default);

    public static bool Evaluate(string ruleJson, string dataJson) =>
        Evaluate(ruleJson, dataJson, competitorCompletions: null);

    /// <summary>
    /// For callers with a single evaluation to perform. A caller evaluating many
    /// rules against the same data (the connector submission path) should parse
    /// once and use <see cref="Evaluate(JObject, JObject, int?)"/> instead.
    /// </summary>
    public static bool Evaluate(string ruleJson, string dataJson, int? competitorCompletions)
    {
        JObject rule, data;
        try
        {
            rule = JObject.Parse(ruleJson);
            data = JObject.Parse(dataJson);
        }
        catch (JsonException)
        {
            return false;
        }
        return Evaluate(rule, data, competitorCompletions);
    }

    /// <summary>
    /// <paramref name="competitorCompletions"/> is injected into
    /// <paramref name="data"/> under <see cref="CompetitorCompletionsVariable"/>
    /// for the duration of the call and removed again before returning (a
    /// pre-existing value is put back), so one parsed data object can be
    /// reused across many objectives without one's count leaking into the
    /// next. The injection is in place rather than on a deep clone: the
    /// connector path evaluates every pending fail rule against the same
    /// multi-thousand-property payload, and cloning it per rule dominated the
    /// request. Consequently a data instance must not be evaluated on two
    /// threads at once.
    /// </summary>
    /// <remarks>
    /// Deliberately no activity or business-operation scope per call: a
    /// submission evaluates every pending rule, so per-rule spans were the
    /// bulk of the trace volume while saying nothing a per-submission span
    /// (which carries the rule count) does not. Outcomes are counted on
    /// <see cref="DiagnosticsConfig.RuleEvaluations"/> instead.
    /// </remarks>
    public static bool Evaluate(JObject rule, JObject data, int? competitorCompletions)
    {
        JToken? previous = null;
        var injected = false;
        try
        {
            if (competitorCompletions.HasValue)
            {
                previous = data[CompetitorCompletionsVariable];
                data[CompetitorCompletionsVariable] = competitorCompletions.Value;
                injected = true;
            }
            var result = Evaluator.Apply(rule, data);
            var isMatch = result is true;
            DiagnosticsConfig.RecordRuleEvaluation(isMatch ? "match" : "no_match");
            return isMatch;
        }
        catch (JsonException)
        {
            DiagnosticsConfig.RecordRuleEvaluation(DiagnosticsConfig.OperationStatuses.InvalidJson);
            return false;
        }
        catch (InvalidOperationException)
        {
            DiagnosticsConfig.RecordRuleEvaluation(DiagnosticsConfig.OperationStatuses.InvalidOperation);
            return false;
        }
        catch (ArgumentException)
        {
            DiagnosticsConfig.RecordRuleEvaluation(DiagnosticsConfig.OperationStatuses.InvalidArgument);
            return false;
        }
        finally
        {
            if (injected)
            {
                if (previous is null) data.Remove(CompetitorCompletionsVariable);
                else data[CompetitorCompletionsVariable] = previous;
            }
        }
    }
}
