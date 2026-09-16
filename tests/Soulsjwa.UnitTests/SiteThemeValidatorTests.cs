using FluentAssertions;
using Soulsjwa.Api.Features.Theme.Entities;
using Soulsjwa.Api.Features.Theme.Services;
using Xunit;

namespace Soulsjwa.UnitTests;

public class SiteThemeValidatorTests
{
    private static SiteTheme BuildValidTheme() => new()
    {
        LightDefault = "#f5f5f5",
        LightAccent = "#6d28d9",
        LightDanger = "#b91c1c",
        LightInfo = "#0369a1",
        LightSuccess = "#15803d",
        LightHighlight = "#b45309",
        DarkDefault = "#1e1e1e",
        DarkAccent = "#c4b5fd",
        DarkDanger = "#f87171",
        DarkInfo = "#38bdf8",
        DarkSuccess = "#4ade80",
        DarkHighlight = "#fbbf24",
    };

    [Theory]
    [InlineData("#ffffff", true)]
    [InlineData("#000000", true)]
    [InlineData("#AbC123", true)]
    [InlineData("ffffff", false)]
    [InlineData("#fff", false)]
    [InlineData("#gggggg", false)]
    [InlineData("#ffffffff", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidHex_ValidatesShape(string? value, bool expected)
    {
        SiteThemeValidator.IsValidHex(value).Should().Be(expected);
    }

    [Theory]
    [InlineData("SystemSansSerif", true)]
    // Enum names on the wire are exact-case (docs/agent-conventions/backend-endpoints.md).
    [InlineData("systemsansserif", false)]
    [InlineData("SystemSerif", true)]
    [InlineData("SystemMonospace", true)]
    [InlineData("ComicSans", false)]
    [InlineData("Arial", false)]
    [InlineData(null, false)]
    public void IsAllowedFont_ChecksAgainstAllowlist(string? font, bool expected)
    {
        SiteThemeValidator.IsAllowedFont(font).Should().Be(expected);
    }

    [Fact]
    public void ContrastRatio_BlackOnWhite_IsMaximal()
    {
        SiteThemeValidator.ContrastRatio("#000000", "#ffffff").Should().BeApproximately(21.0, 0.01);
    }

    [Fact]
    public void ContrastRatio_SameColour_IsOne()
    {
        SiteThemeValidator.ContrastRatio("#6d28d9", "#6d28d9").Should().BeApproximately(1.0, 0.001);
    }

    [Fact]
    public void ContrastRatio_IsSymmetric()
    {
        var a = SiteThemeValidator.ContrastRatio("#6d28d9", "#f5f5f5");
        var b = SiteThemeValidator.ContrastRatio("#f5f5f5", "#6d28d9");
        a.Should().BeApproximately(b, 0.0001);
    }

    [Fact]
    public void Validate_DefaultConstructorTheme_Passes()
    {
        // The entity's own property-initializer defaults (and the seeded
        // row in AppDbContext) must themselves be a valid theme.
        SiteThemeValidator.Validate(new SiteTheme()).Should().BeNull();
    }

    [Fact]
    public void Validate_WellFormedPalette_ReturnsNull()
    {
        SiteThemeValidator.Validate(BuildValidTheme()).Should().BeNull();
    }

    [Fact]
    public void ValidatePalette_MalformedHex_FailsAndNamesTheSlot()
    {
        var slots = new Dictionary<string, string>
        {
            ["Default"] = "#f5f5f5",
            ["Accent"] = "not-a-colour",
            ["Danger"] = "#b91c1c",
            ["Info"] = "#0369a1",
            ["Success"] = "#15803d",
            ["Highlight"] = "#b45309",
        };

        var error = SiteThemeValidator.ValidatePalette("Light", slots);

        error.Should().NotBeNull();
        error.Should().Contain("Light.Accent");
        error.Should().Contain("not-a-colour");
    }

    [Fact]
    public void ValidatePalette_LowContrastAccentAgainstDefault_FailsAndNamesTheFailingPair()
    {
        // Both near-white — well below 4.5:1 against each other.
        var slots = new Dictionary<string, string>
        {
            ["Default"] = "#f5f5f5",
            ["Accent"] = "#f8f8f8",
            ["Danger"] = "#b91c1c",
            ["Info"] = "#0369a1",
            ["Success"] = "#15803d",
            ["Highlight"] = "#b45309",
        };

        var error = SiteThemeValidator.ValidatePalette("Light", slots);

        error.Should().NotBeNull();
        error.Should().Contain("Light.Accent");
        error.Should().Contain("Light.Default");
        error.Should().Contain("#f8f8f8");
        error.Should().Contain("#f5f5f5");
    }

    [Fact]
    public void Validate_LowContrastInDarkPalette_ReturnsDarkModeError()
    {
        var theme = BuildValidTheme();
        theme.DarkAccent = "#1f1f1f"; // near-identical to DarkDefault (#1e1e1e)

        var error = SiteThemeValidator.Validate(theme);

        error.Should().NotBeNull();
        error.Should().Contain("Dark.Accent");
    }

    [Fact]
    public void ValidatePalette_HexIsCaseInsensitive()
    {
        var slots = new Dictionary<string, string>
        {
            ["Default"] = "#F5F5F5",
            ["Accent"] = "#6D28D9",
            ["Danger"] = "#B91C1C",
            ["Info"] = "#0369A1",
            ["Success"] = "#15803D",
            ["Highlight"] = "#B45309",
        };

        SiteThemeValidator.ValidatePalette("Light", slots).Should().BeNull();
    }
}
