using System.Text.RegularExpressions;
using Soulsjwa.Api.Features.Theme.Entities;

namespace Soulsjwa.Api.Features.Theme.Services;

/// <summary>
/// Validates a <see cref="SiteTheme"/>'s hex colours, font, and contrast. Pure
/// functions — no DB access, no HTTP concerns — so the endpoint layer composes
/// them with its own request-shape and foreign-key checks.
/// </summary>
public static class SiteThemeValidator
{
    private static readonly Regex HexPattern = new("^#[0-9a-fA-F]{6}$", RegexOptions.Compiled);

    /// <summary>Slot name used as the contrast reference for the other five slots in its mode.</summary>
    internal const string BackgroundSlotName = "Default";

    public static bool IsValidHex(string? value) => value is not null && HexPattern.IsMatch(value);

    /// <summary>True if <paramref name="font"/> names one of the <see cref="SiteFont"/> allowlist values.</summary>
    public static bool IsAllowedFont(string? font) =>
        font is not null && Enum.TryParse<SiteFont>(font, ignoreCase: false, out var parsed) && Enum.IsDefined(parsed);

    public static IReadOnlyDictionary<string, string> LightSlots(SiteTheme theme) => new Dictionary<string, string>
    {
        [BackgroundSlotName] = theme.LightDefault,
        ["Accent"] = theme.LightAccent,
        ["Danger"] = theme.LightDanger,
        ["Info"] = theme.LightInfo,
        ["Success"] = theme.LightSuccess,
        ["Highlight"] = theme.LightHighlight,
    };

    public static IReadOnlyDictionary<string, string> DarkSlots(SiteTheme theme) => new Dictionary<string, string>
    {
        [BackgroundSlotName] = theme.DarkDefault,
        ["Accent"] = theme.DarkAccent,
        ["Danger"] = theme.DarkDanger,
        ["Info"] = theme.DarkInfo,
        ["Success"] = theme.DarkSuccess,
        ["Highlight"] = theme.DarkHighlight,
    };

    /// <summary>
    /// Validates one mode's six slots: every value must be a well-formed
    /// #rrggbb colour, and every slot but <see cref="BackgroundSlotName"/>
    /// must reach 4.5:1 contrast (WCAG AA) against that mode's
    /// <see cref="BackgroundSlotName"/> slot. Returns <c>null</c> when the
    /// whole palette is valid, or a message naming the failing pair.
    /// </summary>
    public static string? ValidatePalette(string modeName, IReadOnlyDictionary<string, string> slots)
    {
        foreach (var (slotName, hex) in slots)
        {
            if (!IsValidHex(hex))
                return $"{modeName}.{slotName}: '{hex}' is not a valid #rrggbb colour.";
        }

        var background = slots[BackgroundSlotName];
        foreach (var (slotName, hex) in slots)
        {
            if (slotName == BackgroundSlotName) continue;

            var ratio = ContrastRatio(hex, background);
            if (ratio < 4.5)
            {
                return $"{modeName}.{slotName} ('{hex}') and {modeName}.{BackgroundSlotName} ('{background}') " +
                       $"have a contrast ratio of {ratio:F2}:1, below the required 4.5:1 (WCAG AA).";
            }
        }

        return null;
    }

    /// <summary>Validates both the light and dark palettes. Returns the first failing message, or <c>null</c>.</summary>
    public static string? Validate(SiteTheme theme) =>
        ValidatePalette("Light", LightSlots(theme)) ?? ValidatePalette("Dark", DarkSlots(theme));

    /// <summary>WCAG 2.1 contrast ratio between two #rrggbb colours, in [1, 21].</summary>
    public static double ContrastRatio(string hex1, string hex2)
    {
        var l1 = RelativeLuminance(hex1);
        var l2 = RelativeLuminance(hex2);
        var lighter = Math.Max(l1, l2);
        var darker = Math.Min(l1, l2);
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double RelativeLuminance(string hex)
    {
        var (r, g, b) = ParseHex(hex);
        return 0.2126 * Linearize(r) + 0.7152 * Linearize(g) + 0.0722 * Linearize(b);
    }

    private static double Linearize(int channel)
    {
        var s = channel / 255.0;
        return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
    }

    private static (int r, int g, int b) ParseHex(string hex)
    {
        var v = hex.TrimStart('#');
        return (
            Convert.ToInt32(v[..2], 16),
            Convert.ToInt32(v.Substring(2, 2), 16),
            Convert.ToInt32(v.Substring(4, 2), 16));
    }
}
