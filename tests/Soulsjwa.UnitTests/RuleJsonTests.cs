using FluentAssertions;
using Newtonsoft.Json.Linq;
using Soulsjwa.Api.Features.Games.Services;
using Xunit;

namespace Soulsjwa.UnitTests;

public class RuleJsonTests
{
    [Theory]
    [InlineData("""{">":[{"var":"g1_f16"},0]}""", """{">": [{"var": "g1_f16"}, 0]}""")]
    [InlineData("""{"b":1,"a":{"d":[1, 2],"c":null}}""", """{"a": {"c": null, "d": [1,2]}, "b": 1}""")]
    public void Canonicalize_IgnoresWhitespaceAndKeyOrder(string left, string right)
    {
        // What Postgres does to jsonb on the way in: the stored form of a
        // rule never matches its C# literal byte-for-byte.
        RuleJson.Canonicalize(left).Should().Be(RuleJson.Canonicalize(right));
    }

    [Fact]
    public void Canonicalize_TellsDifferentRulesApart()
    {
        RuleJson.Canonicalize("""{">":[{"var":"g1_f16"},0]}""")
            .Should().NotBe(RuleJson.Canonicalize("""{">":[{"var":"g1_f17"},0]}"""));
    }

    [Fact]
    public void Canonicalize_LeavesInvalidJsonAlone()
    {
        RuleJson.Canonicalize("not json").Should().Be("not json");
    }

    [Fact]
    public void ParsedRuleCache_ReturnsTheSameInstanceForTheSameText()
    {
        const string rule = """{"==":[{"var":"x"},1]}""";

        var first = RuleJson.ParsedRuleCache.Get(rule);
        var second = RuleJson.ParsedRuleCache.Get(rule);

        second.Should().BeSameAs(first);
        first["=="].Should().NotBeNull();
    }

    [Fact]
    public void ParsedRuleCache_ThrowsForInvalidJsonLikeParseDoes()
    {
        var act = () => RuleJson.ParsedRuleCache.Get("{not json");

        act.Should().Throw<Newtonsoft.Json.JsonException>();
    }

    [Fact]
    public void Evaluate_WithCompetitorCompletions_LeavesTheDataObjectAsItFound()
    {
        // One parsed payload is reused across every rule of a submission, and
        // the injected count must not leak from one objective into the next.
        var rule = JObject.Parse("""{">=":[{"var":"competitorCompletions"},2]}""");
        var data = JObject.Parse("""{"flag":1}""");

        RuleEvaluator.Evaluate(rule, data, competitorCompletions: 3).Should().BeTrue();

        data.Properties().Select(p => p.Name).Should().Equal("flag");
        RuleEvaluator.Evaluate(rule, data, competitorCompletions: null).Should().BeFalse(
            "with nothing injected the variable is absent, so the rule cannot match");
    }

    [Fact]
    public void Evaluate_WithCompetitorCompletions_RestoresAPreExistingValue()
    {
        var rule = JObject.Parse("""{"==":[{"var":"competitorCompletions"},9]}""");
        var data = JObject.Parse("""{"competitorCompletions":"kept"}""");

        RuleEvaluator.Evaluate(rule, data, competitorCompletions: 9).Should().BeTrue();

        data["competitorCompletions"]!.Value<string>().Should().Be("kept");
    }
}
