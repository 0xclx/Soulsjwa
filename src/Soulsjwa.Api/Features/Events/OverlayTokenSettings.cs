using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Soulsjwa.Api.Infrastructure.Data;

namespace Soulsjwa.Api.Features.Events;

/// <summary>
/// The overlay layouts the OBS route can render. String constants rather than
/// an enum because the wire values are the pre-existing lowercase
/// <c>?view=</c> query values the SPA's <c>OVERLAY_VIEWS</c> tuple mirrors —
/// an enum's <c>nameof</c> casing would have split the two.
/// </summary>
public static class OverlayViews
{
    public const string Objectives = "objectives";
    public const string Scores = "scores";
    public const string Games = "games";

    public static readonly IReadOnlyList<string> All = [Objectives, Scores, Games];
}

/// <summary>Colour schemes of the overlay panel; see <see cref="OverlayViews"/> for why these are strings.</summary>
public static class OverlayThemes
{
    public const string Dark = "dark";
    public const string Light = "light";

    public static readonly IReadOnlyList<string> All = [Dark, Light];
}

/// <summary>
/// The ranges every numeric overlay knob is clamped to. The SPA's
/// <c>OVERLAY_LIMITS</c> carries the same values for its query-string parser
/// and settings form; change both together.
/// </summary>
public static class OverlayTokenSettingsLimits
{
    public const int MinPageSize = 1;
    public const int MaxPageSize = 50;
    public const int MinCycleSeconds = 0;
    public const int MaxCycleSeconds = 600;
    public const int MinRefreshSeconds = 2;
    public const int MaxRefreshSeconds = 120;
    public const int MinHighlightSeconds = 1;
    public const int MaxHighlightSeconds = 60;
    public const int MinPanelOpacity = 0;
    public const int MaxPanelOpacity = 100;
    public const int MaxTitleLength = 100;
}

/// <summary>
/// How one overlay token's OBS browser source looks, saved server-side so a
/// source already on screen picks the change up on its next poll instead of
/// needing its URL re-pasted. Every knob mirrors a query-string parameter of
/// the overlay route (docs/streamer-overlay.md); once saved, these take
/// precedence over the URL for every knob they carry. The page background
/// (<c>bg</c>) is deliberately absent: it exists for previewing outside OBS,
/// not for the source itself, so it stays URL-only. Persisted as JSON in
/// <see cref="Entities.EventOverlayToken.SettingsJson"/>.
/// </summary>
/// <param name="GameIds">Event-game ids to restrict to; null (or empty) means every game.</param>
/// <param name="PlayerIds">Competitor user ids to restrict to; null (or empty) means everyone.</param>
/// <param name="Title">Title-row override; null means the event's name.</param>
public sealed record OverlayTokenSettings(
    string View,
    string Theme,
    List<Guid>? GameIds,
    List<Guid>? PlayerIds,
    int PageSize,
    int CycleSeconds,
    int RefreshSeconds,
    bool ShowTitle,
    bool ShowProgress,
    bool ShowPagination,
    bool Highlight,
    int HighlightSeconds,
    bool Animate,
    int PanelOpacity,
    string? Title)
{
    /// <summary>
    /// The form that is stored: an empty pin list is the same as no pin, and
    /// a blank title the same as none, so the overlay never has to treat the
    /// two spellings differently.
    /// </summary>
    public OverlayTokenSettings Normalized() => this with
    {
        GameIds = GameIds is { Count: > 0 } ? GameIds : null,
        PlayerIds = PlayerIds is { Count: > 0 } ? PlayerIds : null,
        Title = string.IsNullOrWhiteSpace(Title) ? null : Title.Trim(),
    };
}

/// <summary>
/// Validates settings before they are stored. The shape checks are pure so
/// the unit suite covers every range; the membership checks need the event's
/// rows and live in the integration suite.
/// </summary>
public static class OverlayTokenSettingsValidator
{
    /// <summary>Ranges, allowed views/themes, and title length — nothing that needs a database.</summary>
    public static Dictionary<string, string[]> ValidateShape(OverlayTokenSettings settings)
    {
        var errors = new Dictionary<string, string[]>();

        if (!OverlayViews.All.Contains(settings.View, StringComparer.Ordinal))
            errors[nameof(settings.View)] = [$"View must be one of: {string.Join(", ", OverlayViews.All)}."];
        if (!OverlayThemes.All.Contains(settings.Theme, StringComparer.Ordinal))
            errors[nameof(settings.Theme)] = [$"Theme must be one of: {string.Join(", ", OverlayThemes.All)}."];

        Range(errors, nameof(settings.PageSize), settings.PageSize,
            OverlayTokenSettingsLimits.MinPageSize, OverlayTokenSettingsLimits.MaxPageSize);
        Range(errors, nameof(settings.CycleSeconds), settings.CycleSeconds,
            OverlayTokenSettingsLimits.MinCycleSeconds, OverlayTokenSettingsLimits.MaxCycleSeconds);
        Range(errors, nameof(settings.RefreshSeconds), settings.RefreshSeconds,
            OverlayTokenSettingsLimits.MinRefreshSeconds, OverlayTokenSettingsLimits.MaxRefreshSeconds);
        Range(errors, nameof(settings.HighlightSeconds), settings.HighlightSeconds,
            OverlayTokenSettingsLimits.MinHighlightSeconds, OverlayTokenSettingsLimits.MaxHighlightSeconds);
        Range(errors, nameof(settings.PanelOpacity), settings.PanelOpacity,
            OverlayTokenSettingsLimits.MinPanelOpacity, OverlayTokenSettingsLimits.MaxPanelOpacity);

        if (settings.Title is { } title && title.Trim().Length > OverlayTokenSettingsLimits.MaxTitleLength)
            errors[nameof(settings.Title)] =
                [$"Title must be {OverlayTokenSettingsLimits.MaxTitleLength} characters or fewer."];

        return errors;
    }

    /// <summary>
    /// Every pinned game must belong to the event and every pinned player
    /// must compete in it: a pin that matches nothing would render an empty
    /// overlay with no hint why, and the URL form never had anywhere to say so.
    /// </summary>
    public static async Task ValidateMembershipAsync(
        OverlayTokenSettings settings,
        Guid eventId,
        AppDbContext db,
        Dictionary<string, string[]> errors,
        CancellationToken ct)
    {
        if (settings.GameIds is { Count: > 0 } gameIds)
        {
            var known = await db.EventGames
                .Where(g => g.EventId == eventId && gameIds.Contains(g.Id))
                .Select(g => g.Id)
                .ToListAsync(ct);
            if (gameIds.Any(id => !known.Contains(id)))
                errors[nameof(settings.GameIds)] = ["Every pinned game must belong to this event."];
        }

        if (settings.PlayerIds is { Count: > 0 } playerIds)
        {
            var known = await db.EventCompetitors
                .Where(c => c.EventId == eventId && playerIds.Contains(c.UserId))
                .Select(c => c.UserId)
                .ToListAsync(ct);
            if (playerIds.Any(id => !known.Contains(id)))
                errors[nameof(settings.PlayerIds)] = ["Every pinned player must be a competitor in this event."];
        }
    }

    /// <summary>Shape first, membership only when the shape is sound; empty means valid.</summary>
    public static async Task<Dictionary<string, string[]>> ValidateAsync(
        OverlayTokenSettings settings, Guid eventId, AppDbContext db, CancellationToken ct)
    {
        var errors = ValidateShape(settings);
        if (errors.Count == 0)
            await ValidateMembershipAsync(settings, eventId, db, errors, ct);
        return errors;
    }

    private static void Range(Dictionary<string, string[]> errors, string field, int value, int min, int max)
    {
        if (value < min || value > max)
            errors[field] = [$"{field} must be between {min} and {max}."];
    }
}

/// <summary>
/// The one place the settings meet their stored JSON, so the column and the
/// wire never drift: the same web-default (camelCase) options the API's
/// responses use.
/// </summary>
public static class OverlayTokenSettingsJson
{
    public static string Serialize(OverlayTokenSettings settings) =>
        JsonSerializer.Serialize(settings, JsonSerializerOptions.Web);

    /// <summary>Null for a token that never had settings saved — the URL alone drives it.</summary>
    public static OverlayTokenSettings? Deserialize(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? null
            : JsonSerializer.Deserialize<OverlayTokenSettings>(json, JsonSerializerOptions.Web);
}
