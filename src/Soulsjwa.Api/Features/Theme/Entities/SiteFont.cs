namespace Soulsjwa.Api.Features.Theme.Entities;

/// <summary>
/// Server-side font allowlist: self-hosted/system stacks only —
/// no arbitrary font name and no remote font URL, so no third-party
/// `@font-face` request is ever reachable from a visitor's browser. The
/// actual CSS stack string per value lives in the frontend theme provider
/// (`features/theme/siteTheme/`), not here — this enum is only the wire
/// vocabulary.
/// </summary>
public enum SiteFont
{
    SystemSansSerif = 0,
    SystemSerif = 1,
    SystemMonospace = 2,
}
