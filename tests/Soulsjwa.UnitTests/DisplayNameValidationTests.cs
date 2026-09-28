using FluentAssertions;
using Soulsjwa.Api.Features.Users;
using Xunit;

namespace Soulsjwa.UnitTests;

/// <summary>
/// What a user may type as their display name: free text, trimmed, 1–50
/// characters, no control characters; blank clears the override.
/// </summary>
public class DisplayNameValidationTests
{
    [Theory]
    [InlineData("Solaire", "Solaire")]
    [InlineData("  solaire of ASTORA  ", "solaire of ASTORA")]
    [InlineData("ソラール ☀", "ソラール ☀")]
    public void AcceptsFreeTextAndTrimsIt(string raw, string expected)
    {
        DisplayNames.TryNormalize(raw, out var normalized, out var error).Should().BeTrue();

        normalized.Should().Be(expected);
        error.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TreatsBlankAsClearingTheOverride(string? raw)
    {
        DisplayNames.TryNormalize(raw, out var normalized, out _).Should().BeTrue();

        normalized.Should().BeNull();
    }

    [Fact]
    public void AcceptsExactly50Characters_AfterTrimming()
    {
        var fifty = new string('x', 50);

        DisplayNames.TryNormalize($"  {fifty}  ", out var normalized, out _).Should().BeTrue();

        normalized.Should().Be(fifty);
    }

    [Fact]
    public void Rejects51Characters()
    {
        DisplayNames.TryNormalize(new string('x', 51), out _, out var error).Should().BeFalse();

        error.Should().Contain("50");
    }

    [Theory]
    [InlineData("Sol\taire")]
    [InlineData("Sol\naire")]
    [InlineData("Sol\u0000aire")]
    [InlineData("Sol\u007Faire")]
    public void RejectsControlCharacters(string raw)
    {
        DisplayNames.TryNormalize(raw, out _, out var error).Should().BeFalse();

        error.Should().NotBeNullOrEmpty();
    }
}
