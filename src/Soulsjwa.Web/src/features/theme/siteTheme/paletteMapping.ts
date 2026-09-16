import type { ResolvedThemeMode } from '../../../theme'
import type { BackgroundTreatment, SiteFont, SiteTheme, SiteThemePaletteSlot } from '../../../types'

/** Resolved value for every one of the six named slots, for one colour mode. */
export type ResolvedSiteThemePalette = Record<SiteThemePaletteSlot, string>

// Mirrors the backend's SiteTheme entity defaults (seeded row, Id=1) — used
// whenever no theme has loaded yet, so the app never renders with an
// undefined colour before the first fetch resolves.
const FALLBACK_LIGHT: ResolvedSiteThemePalette = {
  Default: '#f5f5f5',
  Accent: '#6d28d9',
  Danger: '#b91c1c',
  Info: '#0369a1',
  Success: '#15803d',
  Highlight: '#b45309',
}

const FALLBACK_DARK: ResolvedSiteThemePalette = {
  Default: '#1e1e1e',
  Accent: '#c4b5fd',
  Danger: '#f87171',
  Info: '#38bdf8',
  Success: '#4ade80',
  Highlight: '#fbbf24',
}

const HEX_COLOR_PATTERN = /^#([0-9a-fA-F]{3}|[0-9a-fA-F]{4}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$/

/**
 * Resolves the six named slots for one colour mode from a fetched
 * {@link SiteTheme}, or from the built-in fallback when no theme has loaded
 * yet. Every slot is guaranteed to be a real hex string — never `undefined`
 * — so a consumer like `event-calendar`'s `CalendarEntryColor` mapping can
 * never render an undefined colour.
 *
 * A slot that isn't a valid hex colour (a bad admin edit, a partially
 * migrated row) is substituted with the matching fallback slot rather than
 * passed through — MUI's `augmentColor` throws on an unparsable value, and
 * this call sits above the router in `ThemeModeProvider`, so an uncaught
 * throw here would take down the whole app for every visitor.
 */
export const resolveSiteThemePalette = (
  theme: SiteTheme | undefined,
  mode: ResolvedThemeMode,
): ResolvedSiteThemePalette => {
  const fallback = mode === 'dark' ? FALLBACK_DARK : FALLBACK_LIGHT
  if (!theme) return fallback

  const candidate: ResolvedSiteThemePalette =
    mode === 'dark'
      ? {
          Default: theme.darkDefault,
          Accent: theme.darkAccent,
          Danger: theme.darkDanger,
          Info: theme.darkInfo,
          Success: theme.darkSuccess,
          Highlight: theme.darkHighlight,
        }
      : {
          Default: theme.lightDefault,
          Accent: theme.lightAccent,
          Danger: theme.lightDanger,
          Info: theme.lightInfo,
          Success: theme.lightSuccess,
          Highlight: theme.lightHighlight,
        }

  const resolved = {} as ResolvedSiteThemePalette
  for (const slot of Object.keys(candidate) as SiteThemePaletteSlot[]) {
    const value = candidate[slot]
    resolved[slot] = HEX_COLOR_PATTERN.test(value) ? value : fallback[slot]
  }
  return resolved
}

const SYSTEM_SANS_SERIF_STACK =
  'system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif'
const SYSTEM_SERIF_STACK = 'Georgia, Cambria, "Times New Roman", Times, serif'
const SYSTEM_MONOSPACE_STACK =
  '"SF Mono", SFMono-Regular, Consolas, "Liberation Mono", Menlo, monospace'

/**
 * Server-side font allowlist, mirrored here as CSS font
 * stacks — self-hosted/system fonts only, never a remote `@font-face`.
 */
export const SITE_FONT_STACKS: Record<SiteFont, string> = {
  SystemSansSerif: SYSTEM_SANS_SERIF_STACK,
  SystemSerif: SYSTEM_SERIF_STACK,
  SystemMonospace: SYSTEM_MONOSPACE_STACK,
}

export const DEFAULT_SITE_FONT: SiteFont = 'SystemSansSerif'

export interface BackgroundTreatmentStyle {
  backgroundSize: string
  backgroundRepeat: string
  backgroundPosition: string
}

/** Concrete CSS values for each `BackgroundTreatment` option. */
export const BACKGROUND_TREATMENT_STYLES: Record<BackgroundTreatment, BackgroundTreatmentStyle> = {
  Cover: { backgroundSize: 'cover', backgroundRepeat: 'no-repeat', backgroundPosition: 'center' },
  Contain: {
    backgroundSize: 'contain',
    backgroundRepeat: 'no-repeat',
    backgroundPosition: 'center',
  },
  Tile: { backgroundSize: 'auto', backgroundRepeat: 'repeat', backgroundPosition: 'top left' },
  None: { backgroundSize: 'auto', backgroundRepeat: 'no-repeat', backgroundPosition: 'top left' },
}
