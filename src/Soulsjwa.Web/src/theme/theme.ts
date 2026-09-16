import { createTheme, type Theme } from '@mui/material/styles'
import type { ResolvedSiteThemePalette } from '../features/theme/siteTheme/paletteMapping'

const SYSTEM_FONT_FAMILY =
  'system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif'

/** Theme mode selectable by the user. `system` follows the OS preference. */
export type ThemeMode = 'light' | 'dark' | 'system'

/** The mode actually applied to the MUI theme (no `system` indirection). */
export type ResolvedThemeMode = 'light' | 'dark'

/**
 * Build the MUI theme for the resolved color mode, from the site theme's six
 * named palette slots and font stack. The mapping from slot to MUI palette
 * key mirrors `CalendarEntryColor`'s exactly, so the two systems can never
 * drift apart: Accent → primary, Info →
 * secondary, Danger → error, Success → success, Highlight → warning,
 * Default → the page background. Every value is a plain palette value MUI
 * consumes internally — never a CSS string injected into the page, and
 * never rendered as raw markup.
 */
export const createAppTheme = (
  mode: ResolvedThemeMode,
  palette?: ResolvedSiteThemePalette,
  fontFamily: string = SYSTEM_FONT_FAMILY,
): Theme =>
  createTheme({
    palette: {
      mode,
      ...(palette && {
        primary: { main: palette.Accent },
        secondary: { main: palette.Info },
        error: { main: palette.Danger },
        success: { main: palette.Success },
        warning: { main: palette.Highlight },
        background: { default: palette.Default },
      }),
    },
    typography: {
      fontFamily,
      h1: { fontSize: '2rem', lineHeight: 1.2, fontWeight: 700 },
      h2: { fontSize: '1.5rem', lineHeight: 1.25, fontWeight: 700 },
      h3: { fontSize: '1.25rem', lineHeight: 1.3, fontWeight: 600 },
      body1: { fontSize: '1rem', lineHeight: 1.5 },
      body2: { fontSize: '0.875rem', lineHeight: 1.5 },
      caption: { fontSize: '0.75rem', lineHeight: 1.5 },
      button: { fontSize: '0.875rem', lineHeight: 1.75, fontWeight: 600 },
    },
    components: {
      MuiCssBaseline: {
        styleOverrides: {
          '@media (prefers-reduced-motion: reduce)': {
            '*, *::before, *::after': {
              animationDuration: '0.01ms !important',
              animationIterationCount: '1 !important',
              scrollBehavior: 'auto !important',
              transitionDuration: '0.01ms !important',
            },
          },
        },
      },
    },
  })
