import { createContext } from 'react'
import type { ResolvedThemeMode, ThemeMode } from './theme'

export interface ThemeModeContextValue {
  /** User-selected mode (light, dark, or system). */
  mode: ThemeMode
  /** Mode actually applied to the UI after resolving `system`. */
  resolvedMode: ResolvedThemeMode
  setMode: (mode: ThemeMode) => void
  /** Cycle: light → dark → system → light … (used by the toggle button). */
  cycleMode: () => void
}

export const ThemeModeContext = createContext<ThemeModeContextValue | null>(null)
