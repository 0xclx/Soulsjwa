using System.Linq;
using Newtonsoft.Json.Linq;

namespace Soulsjwa.Api.Features.Games.Services;

/// <summary>
/// Static analysis over a JsonLogic fail-rule tree: does it depend ONLY on
/// <see cref="RuleEvaluator.CompetitorCompletionsVariable"/>? Such "count-only"
/// rules can be re-evaluated for other pending competitors as soon as a new
/// completion changes that count, instead of waiting for those competitors' own
/// next submission (required for any rule reading per-competitor game state).
/// </summary>
public static class FailRuleAnalyzer
{
    /// <summary>
    /// True when every <c>{"var": ...}</c> reference names
    /// <see cref="RuleEvaluator.CompetitorCompletionsVariable"/>, or the rule
    /// references no variable at all. False for null/empty/invalid input.
    /// </summary>
    public static bool IsCountOnly(string? failRuleJson)
    {
        if (string.IsNullOrWhiteSpace(failRuleJson)) return false;

        JToken root;
        try
        {
            root = JToken.Parse(failRuleJson);
        }
        catch (Newtonsoft.Json.JsonException)
        {
            return false;
        }

        var variables = new HashSet<string>(StringComparer.Ordinal);
        CollectVariables(root, variables);

        return variables.Count == 0
            || (variables.Count == 1 && variables.Contains(RuleEvaluator.CompetitorCompletionsVariable));
    }

    private static void CollectVariables(JToken token, HashSet<string> variables)
    {
        switch (token)
        {
            case JObject obj:
                foreach (var property in obj.Properties())
                {
                    if (property.Name == "var")
                    {
                        // "var" takes either a bare string id or an array
                        // form: {"var": ["id", default]}.
                        var idToken = property.Value.Type == JTokenType.Array
                            ? ((JArray)property.Value).FirstOrDefault()
                            : property.Value;
                        if (idToken is { Type: JTokenType.String })
                        {
                            variables.Add(idToken.Value<string>() ?? string.Empty);
                            continue;
                        }
                    }
                    CollectVariables(property.Value, variables);
                }
                break;
            case JArray array:
                foreach (var item in array)
                    CollectVariables(item, variables);
                break;
        }
    }
}
