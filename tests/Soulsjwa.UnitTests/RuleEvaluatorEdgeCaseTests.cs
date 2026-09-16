using FluentAssertions;
using Soulsjwa.Api.Features.Games.Services;
using Xunit;

namespace Soulsjwa.UnitTests;

public class RuleEvaluatorEdgeCaseTests
{
    [Fact]
    public void Evaluate_OrLogic_ReturnsTrueWhenOneConditionMet()
    {
        var rule = """{"or":[{">":[{"var":"100"},0]},{">":[{"var":"101"},0]}]}""";
        var data = """{"100":0,"101":1}""";
        RuleEvaluator.Evaluate(rule, data).Should().BeTrue();
    }

    [Fact]
    public void Evaluate_OrLogic_ReturnsFalseWhenNoConditionMet()
    {
        var rule = """{"or":[{">":[{"var":"100"},0]},{">":[{"var":"101"},0]}]}""";
        var data = """{"100":0,"101":0}""";
        RuleEvaluator.Evaluate(rule, data).Should().BeFalse();
    }

    [Fact]
    public void Evaluate_LessThanOperator_Works()
    {
        var rule = """{"<":[{"var":"time"},100]}""";
        RuleEvaluator.Evaluate(rule, """{"time":50}""").Should().BeTrue();
        RuleEvaluator.Evaluate(rule, """{"time":150}""").Should().BeFalse();
    }

    [Fact]
    public void Evaluate_GreaterThanOrEqual_Works()
    {
        var rule = """{">=":[{"var":"score"},100]}""";
        RuleEvaluator.Evaluate(rule, """{"score":100}""").Should().BeTrue();
        RuleEvaluator.Evaluate(rule, """{"score":99}""").Should().BeFalse();
    }

    [Fact]
    public void Evaluate_LessThanOrEqual_Works()
    {
        var rule = """{"<=":[{"var":"deaths"},5]}""";
        RuleEvaluator.Evaluate(rule, """{"deaths":5}""").Should().BeTrue();
        RuleEvaluator.Evaluate(rule, """{"deaths":6}""").Should().BeFalse();
    }

    [Fact]
    public void Evaluate_EqualOperator_Works()
    {
        var rule = """{"==":[{"var":"level"},50]}""";
        RuleEvaluator.Evaluate(rule, """{"level":50}""").Should().BeTrue();
        RuleEvaluator.Evaluate(rule, """{"level":49}""").Should().BeFalse();
    }

    [Fact]
    public void Evaluate_MissingVariable_ReturnsFalse()
    {
        var rule = """{">":[{"var":"nonexistent"},0]}""";
        var data = """{"other":1}""";
        RuleEvaluator.Evaluate(rule, data).Should().BeFalse();
    }

    [Fact]
    public void Evaluate_EmptyData_ReturnsFalse()
    {
        var rule = """{">":[{"var":"111"},0]}""";
        RuleEvaluator.Evaluate(rule, "{}").Should().BeFalse();
    }

    [Fact]
    public void Evaluate_EmptyRule_ReturnsFalse()
    {
        RuleEvaluator.Evaluate("{}", """{"111":1}""").Should().BeFalse();
    }

    [Fact]
    public void Evaluate_NestedAndOr_Works()
    {
        // (A > 0 AND B > 0) OR C > 0
        var rule = """{"or":[{"and":[{">":[{"var":"a"},0]},{">":[{"var":"b"},0]}]},{">":[{"var":"c"},0]}]}""";

        // Both A and B met → true
        RuleEvaluator.Evaluate(rule, """{"a":1,"b":1,"c":0}""").Should().BeTrue();
        // Only C met → true
        RuleEvaluator.Evaluate(rule, """{"a":0,"b":0,"c":1}""").Should().BeTrue();
        // None met → false
        RuleEvaluator.Evaluate(rule, """{"a":0,"b":0,"c":0}""").Should().BeFalse();
        // Only A met (not both A and B) → false
        RuleEvaluator.Evaluate(rule, """{"a":1,"b":0,"c":0}""").Should().BeFalse();
    }

    [Fact]
    public void Evaluate_InvalidDataJson_ReturnsFalse()
    {
        var rule = """{">":[{"var":"111"},0]}""";
        RuleEvaluator.Evaluate(rule, "not json").Should().BeFalse();
    }

    [Fact]
    public void Evaluate_NegativeValues_Works()
    {
        var rule = """{">":[{"var":"health"},-1]}""";
        RuleEvaluator.Evaluate(rule, """{"health":0}""").Should().BeTrue();
        RuleEvaluator.Evaluate(rule, """{"health":-2}""").Should().BeFalse();
    }

    [Fact]
    public void Evaluate_LargeNumericValues_Works()
    {
        var rule = """{">":[{"var":"time"},100000]}""";
        RuleEvaluator.Evaluate(rule, """{"time":999999}""").Should().BeTrue();
    }

    [Fact]
    public void Evaluate_MultipleAndConditions_AllMustBeTrue()
    {
        // All three bosses must be slain
        var rule = """{"and":[{">":[{"var":"100"},0]},{">":[{"var":"101"},0]},{">":[{"var":"102"},0]}]}""";
        RuleEvaluator.Evaluate(rule, """{"100":1,"101":1,"102":1}""").Should().BeTrue();
        RuleEvaluator.Evaluate(rule, """{"100":1,"101":1,"102":0}""").Should().BeFalse();
    }

    [Fact]
    public void Evaluate_ParsedOverload_CompetitorCompletionsDoesNotLeakAcrossIterations()
    {
        // Simulates SubmitGameData's loop: one shared parsed data object reused
        // across two fail-rule evaluations with different competitorCompletions
        // counts. The first evaluation's injected count must not be visible to
        // the second.
        var data = Newtonsoft.Json.Linq.JObject.Parse("""{"deaths":3}""");

        var ruleRequiringThreeCompetitors = Newtonsoft.Json.Linq.JObject.Parse(
            """{">=":[{"var":"competitorCompletions"},3]}""");
        var ruleRequiringOneCompetitor = Newtonsoft.Json.Linq.JObject.Parse(
            """{">=":[{"var":"competitorCompletions"},1]}""");

        RuleEvaluator.Evaluate(ruleRequiringThreeCompetitors, data, competitorCompletions: 3).Should().BeTrue();
        // If the first call's mutation leaked, this would still see 3 and pass
        // even though only 0 other competitors have completed this objective.
        RuleEvaluator.Evaluate(ruleRequiringOneCompetitor, data, competitorCompletions: 0).Should().BeFalse();

        data.ContainsKey(RuleEvaluator.CompetitorCompletionsVariable).Should().BeFalse();
    }
}
