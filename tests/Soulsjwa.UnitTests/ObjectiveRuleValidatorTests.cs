using FluentAssertions;
using Soulsjwa.Api.Features.Games.Services;
using Xunit;

namespace Soulsjwa.UnitTests;

public class ObjectiveRuleValidatorTests
{
    [Fact]
    public void ValidateMetadata_Null_IsValid()
    {
        ObjectiveRuleValidator.ValidateMetadata(null).Should().BeNull();
    }

    [Fact]
    public void ValidateMetadata_WellFormedJson_IsValid()
    {
        ObjectiveRuleValidator.ValidateMetadata("""{"area":"Undead Burg"}""").Should().BeNull();
    }

    [Fact]
    public void ValidateMetadata_MalformedJson_IsInvalid()
    {
        ObjectiveRuleValidator.ValidateMetadata("{").Should().NotBeNull();
    }

    [Fact]
    public void ValidateRule_Null_IsValid()
    {
        ObjectiveRuleValidator.ValidateRule(null).Should().BeNull();
    }

    [Fact]
    public void ValidateRule_MalformedJson_IsInvalid()
    {
        ObjectiveRuleValidator.ValidateRule("not json").Should().NotBeNull();
    }

    [Fact]
    public void ValidateRule_WellFormedButWrongRootType_IsInvalid()
    {
        ObjectiveRuleValidator.ValidateRule("[1,2,3]").Should().NotBeNull();
    }

    [Fact]
    public void ValidateRule_SimpleComparison_IsValid()
    {
        ObjectiveRuleValidator.ValidateRule("""{"==":[1,1]}""").Should().BeNull();
    }

    [Fact]
    public void ValidateRule_ComparisonMissingOperand_DoesNotThrowSoIsAcceptedAsValid()
    {
        // JsonLogic.Net treats a missing operand as null rather than throwing,
        // so this is the "well-formed but never matches" case the smoke test
        // cannot catch — RuleEvaluator.Evaluate returning false is the library's
        // deliberate backstop. The smoke test only rejects rules that throw.
        var error = ObjectiveRuleValidator.ValidateRule("""{"and":[{"var":"100"},{">=":[{"var":"death_count"}]}]}""");
        error.Should().BeNull();
    }

    [Fact]
    public void ValidateRule_UnknownOperator_IsInvalid()
    {
        // JsonLogic.Net throws KeyNotFoundException for an operator it doesn't
        // recognise — not one of the three types RuleEvaluator.Evaluate
        // swallows, so this would otherwise be an uncaught exception on a
        // live connector submission. The smoke test catches it at write time.
        ObjectiveRuleValidator.ValidateRule("""{"notARealOperator":[1,2]}""").Should().NotBeNull();
    }

    [Fact]
    public void ValidateRule_StringFunctionOnMissingVariable_IsInvalid()
    {
        // Another shape JsonLogic.Net throws (NullReferenceException) rather
        // than returning false for.
        ObjectiveRuleValidator.ValidateRule("""{"substr":[{"var":"missing"},0,3]}""").Should().NotBeNull();
    }

    [Fact]
    public void ValidateRule_TooLarge_IsInvalid()
    {
        var huge = "{\"==\":[1," + new string('1', ObjectiveRuleValidator.MaxRuleBytes) + "]}";
        ObjectiveRuleValidator.ValidateRule(huge).Should().NotBeNull();
    }

    [Fact]
    public void ValidateRule_DoesNotRejectAnythingRuleEvaluatorEdgeCaseTestsConsidersEvaluable()
    {
        string[] rules =
        [
            """{"or":[{">":[{"var":"100"},0]},{">":[{"var":"101"},0]}]}""",
            """{"<":[{"var":"time"},100]}""",
            """{">=":[{"var":"score"},100]}""",
            """{"<=":[{"var":"deaths"},5]}""",
            """{"==":[{"var":"level"},50]}""",
            """{">":[{"var":"nonexistent"},0]}""",
            """{"and":[{">":[{"var":"100"},0]},{">":[{"var":"101"},0]},{">":[{"var":"102"},0]}]}""",
        ];

        foreach (var rule in rules)
            ObjectiveRuleValidator.ValidateRule(rule).Should().BeNull(because: $"rule {rule} is evaluable per RuleEvaluatorEdgeCaseTests");
    }
}
