using System.Text.Json;
using FluentAssertions;
using Soulsjwa.Api.Features.Events;
using Xunit;

namespace Soulsjwa.UnitTests;

public class OverlayTokenSettingsValidatorTests
{
    private static OverlayTokenSettings Valid() => new(
        View: OverlayViews.Objectives,
        Theme: OverlayThemes.Dark,
        GameIds: null,
        PlayerIds: null,
        PageSize: 10,
        CycleSeconds: 30,
        RefreshSeconds: 5,
        ShowTitle: true,
        ShowProgress: true,
        ShowPagination: true,
        Highlight: true,
        HighlightSeconds: 6,
        Animate: true,
        PanelOpacity: 80,
        Title: null);

    [Fact]
    public void ValidSettings_HaveNoErrors()
    {
        OverlayTokenSettingsValidator.ValidateShape(Valid()).Should().BeEmpty();
    }

    [Theory]
    [InlineData("Objectives")]
    [InlineData("table")]
    [InlineData("")]
    public void UnknownView_IsRejected(string view)
    {
        var errors = OverlayTokenSettingsValidator.ValidateShape(Valid() with { View = view });

        errors.Should().ContainKey(nameof(OverlayTokenSettings.View));
    }

    [Fact]
    public void UnknownTheme_IsRejected()
    {
        var errors = OverlayTokenSettingsValidator.ValidateShape(Valid() with { Theme = "Dark" });

        errors.Should().ContainKey(nameof(OverlayTokenSettings.Theme));
    }

    [Theory]
    [InlineData(nameof(OverlayTokenSettings.PageSize), OverlayTokenSettingsLimits.MinPageSize - 1)]
    [InlineData(nameof(OverlayTokenSettings.PageSize), OverlayTokenSettingsLimits.MaxPageSize + 1)]
    [InlineData(nameof(OverlayTokenSettings.CycleSeconds), OverlayTokenSettingsLimits.MinCycleSeconds - 1)]
    [InlineData(nameof(OverlayTokenSettings.CycleSeconds), OverlayTokenSettingsLimits.MaxCycleSeconds + 1)]
    [InlineData(nameof(OverlayTokenSettings.RefreshSeconds), OverlayTokenSettingsLimits.MinRefreshSeconds - 1)]
    [InlineData(nameof(OverlayTokenSettings.RefreshSeconds), OverlayTokenSettingsLimits.MaxRefreshSeconds + 1)]
    [InlineData(nameof(OverlayTokenSettings.HighlightSeconds), OverlayTokenSettingsLimits.MinHighlightSeconds - 1)]
    [InlineData(nameof(OverlayTokenSettings.HighlightSeconds), OverlayTokenSettingsLimits.MaxHighlightSeconds + 1)]
    [InlineData(nameof(OverlayTokenSettings.PanelOpacity), OverlayTokenSettingsLimits.MinPanelOpacity - 1)]
    [InlineData(nameof(OverlayTokenSettings.PanelOpacity), OverlayTokenSettingsLimits.MaxPanelOpacity + 1)]
    public void OutOfRangeKnob_IsRejectedUnderItsOwnName(string field, int value)
    {
        var settings = field switch
        {
            nameof(OverlayTokenSettings.PageSize) => Valid() with { PageSize = value },
            nameof(OverlayTokenSettings.CycleSeconds) => Valid() with { CycleSeconds = value },
            nameof(OverlayTokenSettings.RefreshSeconds) => Valid() with { RefreshSeconds = value },
            nameof(OverlayTokenSettings.HighlightSeconds) => Valid() with { HighlightSeconds = value },
            nameof(OverlayTokenSettings.PanelOpacity) => Valid() with { PanelOpacity = value },
            _ => throw new ArgumentOutOfRangeException(nameof(field)),
        };

        var errors = OverlayTokenSettingsValidator.ValidateShape(settings);

        errors.Should().ContainSingle().Which.Key.Should().Be(field);
    }

    [Theory]
    [InlineData(OverlayTokenSettingsLimits.MinPageSize, OverlayTokenSettingsLimits.MinCycleSeconds,
        OverlayTokenSettingsLimits.MinRefreshSeconds, OverlayTokenSettingsLimits.MinHighlightSeconds,
        OverlayTokenSettingsLimits.MinPanelOpacity)]
    [InlineData(OverlayTokenSettingsLimits.MaxPageSize, OverlayTokenSettingsLimits.MaxCycleSeconds,
        OverlayTokenSettingsLimits.MaxRefreshSeconds, OverlayTokenSettingsLimits.MaxHighlightSeconds,
        OverlayTokenSettingsLimits.MaxPanelOpacity)]
    public void Bounds_AreInclusive(int pageSize, int cycle, int refresh, int highlight, int opacity)
    {
        var settings = Valid() with
        {
            PageSize = pageSize,
            CycleSeconds = cycle,
            RefreshSeconds = refresh,
            HighlightSeconds = highlight,
            PanelOpacity = opacity,
        };

        OverlayTokenSettingsValidator.ValidateShape(settings).Should().BeEmpty();
    }

    [Fact]
    public void OverlongTitle_IsRejected_ButWhitespacePaddingDoesNotCount()
    {
        var atLimit = new string('x', OverlayTokenSettingsLimits.MaxTitleLength);

        OverlayTokenSettingsValidator.ValidateShape(Valid() with { Title = $"  {atLimit}  " }).Should().BeEmpty();
        OverlayTokenSettingsValidator.ValidateShape(Valid() with { Title = atLimit + "x" })
            .Should().ContainKey(nameof(OverlayTokenSettings.Title));
    }

    [Fact]
    public void Normalized_CollapsesEmptyPinsAndBlankTitle_ToNull()
    {
        var normalized = (Valid() with { GameIds = [], PlayerIds = [], Title = "   " }).Normalized();

        normalized.GameIds.Should().BeNull();
        normalized.PlayerIds.Should().BeNull();
        normalized.Title.Should().BeNull();
    }

    [Fact]
    public void Normalized_KeepsPinsAndTrimsTitle()
    {
        var game = Guid.NewGuid();
        var normalized = (Valid() with { GameIds = [game], Title = " Finals " }).Normalized();

        normalized.GameIds.Should().Equal(game);
        normalized.Title.Should().Be("Finals");
    }

    [Fact]
    public void Json_RoundTrips_UsingTheWireCasing()
    {
        // The column holds the same JSON the SPA sends and receives, so a
        // stored document must be readable as the wire shape and vice versa.
        var settings = Valid() with { View = OverlayViews.Scores, GameIds = [Guid.NewGuid()], Title = "Finals" };

        var json = OverlayTokenSettingsJson.Serialize(settings);

        using var document = JsonDocument.Parse(json);
        document.RootElement.TryGetProperty("pageSize", out _).Should().BeTrue("properties are camelCase on the wire");
        document.RootElement.TryGetProperty("PageSize", out _).Should().BeFalse();
        // Record equality compares the list by reference, so compare structurally.
        OverlayTokenSettingsJson.Deserialize(json).Should().BeEquivalentTo(settings);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Json_NoDocument_MeansNoSettings(string? json)
    {
        OverlayTokenSettingsJson.Deserialize(json).Should().BeNull();
    }
}
