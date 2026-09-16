using Soulsjwa.Api.Features.Media.Entities;

namespace Soulsjwa.Api.Features.Theme.Entities;

/// <summary>
/// Singleton row (fixed <see cref="Id"/> = 1) applied to every page. The six
/// named palette slots — <c>Default</c>, <c>Accent</c>,
/// <c>Danger</c>, <c>Info</c>, <c>Success</c>, <c>Highlight</c> — are the
/// theme's public vocabulary that <c>event-calendar</c>'s
/// <c>CalendarEntryColor</c> later resolves against 1:1, so adding or
/// removing a slot here is a breaking change for that module.
///
/// <c>Default</c> is the page-background reference colour for its mode;
/// the other five slots are validated for WCAG AA (4.5:1) contrast against
/// their mode's <c>Default</c>, since they're the colours actually used as
/// text/accents on top of it (see <see cref="Services.SiteThemeValidator"/>).
/// </summary>
public class SiteTheme
{
    /// <summary>Always 1 — this table holds exactly one row.</summary>
    public int Id { get; set; } = 1;

    /// <summary>Never a URL — an uploaded, self-hosted <see cref="MediaAsset"/>.</summary>
    public Guid? BackgroundAssetId { get; set; }
    public MediaAsset? BackgroundAsset { get; set; }
    public BackgroundTreatment BackgroundTreatment { get; set; } = BackgroundTreatment.None;

    public SiteFont Font { get; set; } = SiteFont.SystemSansSerif;

    public string LightDefault { get; set; } = "#f5f5f5";
    public string LightAccent { get; set; } = "#6d28d9";
    public string LightDanger { get; set; } = "#b91c1c";
    public string LightInfo { get; set; } = "#0369a1";
    public string LightSuccess { get; set; } = "#15803d";
    public string LightHighlight { get; set; } = "#b45309";

    public string DarkDefault { get; set; } = "#1e1e1e";
    public string DarkAccent { get; set; } = "#c4b5fd";
    public string DarkDanger { get; set; } = "#f87171";
    public string DarkInfo { get; set; } = "#38bdf8";
    public string DarkSuccess { get; set; } = "#4ade80";
    public string DarkHighlight { get; set; } = "#fbbf24";

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
