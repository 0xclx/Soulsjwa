namespace Soulsjwa.Api.Features.Calendar.Entities;

/// <summary>
/// Semantic colour slot for a calendar entry. Deliberately an enum of slot NAMES
/// rather than a hex value: the concrete colour is resolved client-side from the
/// active site theme (see the `site-theme` module), so entries restyle with the
/// theme instead of drifting from it — and an author cannot pick a colour that
/// renders an entry unreadable or invisible in one of the two themes. The six
/// values are exactly the site theme's six named palette slots, so this enum
/// can never drift apart from `SiteTheme`'s vocabulary.
/// </summary>
public enum CalendarEntryColor { Default, Accent, Danger, Info, Success, Highlight }
