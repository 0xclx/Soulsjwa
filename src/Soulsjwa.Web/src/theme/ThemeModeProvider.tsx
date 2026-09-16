import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import { ThemeProvider as MuiThemeProvider } from '@mui/material/styles'
import CssBaseline from '@mui/material/CssBaseline'
import useMediaQuery from '@mui/material/useMediaQuery'
import { createAppTheme, type ResolvedThemeMode, type ThemeMode } from './theme'
import { ThemeModeContext, type ThemeModeContextValue } from './ThemeModeContext'
import { useSiteTheme } from '../features/theme/siteTheme/hooks/useSiteTheme'
import {
  resolveSiteThemePalette,
  SITE_FONT_STACKS,
  DEFAULT_SITE_FONT,
  BACKGROUND_TREATMENT_STYLES,
} from '../features/theme/siteTheme/paletteMapping'

const STORAGE_KEY = 'soulsjwa.themeMode'
const VALID_MODES: readonly ThemeMode[] = ['light', 'dark', 'system']

/** Quote a URL for use inside a `url(...)` CSS value so it cannot break out
 * of the declaration (an admin-uploaded asset URL is trusted, but this is
 * one property assignment, not string-built CSS, so the fix is cheap). */
const cssUrlValue = (url: string): string =>
  `url("${url.replace(/\\/g, '\\\\').replace(/"/g, '\\"')}")`

const readStoredMode = (): ThemeMode => {
  if (typeof window === 'undefined') return 'dark'
  try {
    const stored = window.localStorage.getItem(STORAGE_KEY)
    if (stored && (VALID_MODES as readonly string[]).includes(stored)) {
      return stored as ThemeMode
    }
  } catch {
    // Access to localStorage can throw (e.g. privacy mode); fall through.
  }
  return 'dark'
}

interface ThemeModeProviderProps {
  children: ReactNode
  /** Fallback when the device preference cannot be determined. */
  defaultMode?: ResolvedThemeMode
}

/**
 * Provides the user's chosen theme mode, persists it to localStorage, and
 * resolves it against the device's `prefers-color-scheme` when system mode is
 * chosen. Defaults to dark when no preference has been saved.
 */
export const ThemeModeProvider = ({ children, defaultMode = 'dark' }: ThemeModeProviderProps) => {
  const [mode, setModeState] = useState<ThemeMode>(readStoredMode)
  const prefersDark = useMediaQuery('(prefers-color-scheme: dark)', {
    defaultMatches: defaultMode === 'dark',
  })

  const resolvedMode: ResolvedThemeMode = useMemo(() => {
    if (mode === 'system') return prefersDark ? 'dark' : 'light'
    return mode
  }, [mode, prefersDark])

  const setMode = useCallback((next: ThemeMode) => {
    setModeState(next)
  }, [])

  const { data: siteTheme } = useSiteTheme()

  const cycleMode = useCallback(() => {
    setModeState((current) => {
      const idx = VALID_MODES.indexOf(current)
      // Index is always within bounds because (idx + 1) % length wraps; the
      // non-null assertion is safe here and required only because
      // noUncheckedIndexedAccess widens array indexing to `T | undefined`.
      return VALID_MODES[(idx + 1) % VALID_MODES.length]!
    })
  }, [])

  useEffect(() => {
    try {
      window.localStorage.setItem(STORAGE_KEY, mode)
    } catch {
      // Ignore storage write failures (private mode, quota, etc.).
    }
  }, [mode])

  const theme = useMemo(() => {
    const palette = resolveSiteThemePalette(siteTheme, resolvedMode)
    const fontFamily = SITE_FONT_STACKS[siteTheme?.font ?? DEFAULT_SITE_FONT]
    try {
      return createAppTheme(resolvedMode, palette, fontFamily)
    } catch {
      // A palette MUI can't parse must never take down the whole app (this
      // runs above the router, so no route errorElement can catch it) —
      // degrade to the built-in default look instead of a white screen.
      return createAppTheme(resolvedMode)
    }
  }, [resolvedMode, siteTheme])

  const backgroundUrl = siteTheme?.backgroundUrl ?? null
  const backgroundTreatment = siteTheme?.backgroundTreatment ?? 'Cover'

  useEffect(() => {
    document.documentElement.dataset.colorScheme = resolvedMode
    document.documentElement.style.colorScheme = resolvedMode
    document.documentElement.style.backgroundColor = theme.palette.background.default

    if (backgroundUrl) {
      const treatmentStyle = BACKGROUND_TREATMENT_STYLES[backgroundTreatment]
      document.documentElement.style.backgroundImage = cssUrlValue(backgroundUrl)
      document.documentElement.style.backgroundSize = treatmentStyle.backgroundSize
      document.documentElement.style.backgroundRepeat = treatmentStyle.backgroundRepeat
      document.documentElement.style.backgroundPosition = treatmentStyle.backgroundPosition
    } else {
      document.documentElement.style.backgroundImage = ''
      document.documentElement.style.backgroundSize = ''
      document.documentElement.style.backgroundRepeat = ''
      document.documentElement.style.backgroundPosition = ''
    }
  }, [resolvedMode, theme, backgroundUrl, backgroundTreatment])

  const value = useMemo<ThemeModeContextValue>(
    () => ({ mode, resolvedMode, setMode, cycleMode }),
    [mode, resolvedMode, setMode, cycleMode],
  )

  return (
    <ThemeModeContext.Provider value={value}>
      <MuiThemeProvider theme={theme}>
        <CssBaseline />
        {children}
      </MuiThemeProvider>
    </ThemeModeContext.Provider>
  )
}
