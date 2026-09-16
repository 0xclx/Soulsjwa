import { describe, it, expect } from 'vitest'
import { createTheme } from '@mui/material/styles'
import { createAppTheme } from './theme'
import type { ResolvedSiteThemePalette } from '../features/theme/siteTheme/paletteMapping'

const LIGHT_PALETTE: ResolvedSiteThemePalette = {
  Default: '#f5f5f5',
  Accent: '#6d28d9',
  Danger: '#b91c1c',
  Info: '#0369a1',
  Success: '#15803d',
  Highlight: '#b45309',
}

const DARK_PALETTE: ResolvedSiteThemePalette = {
  Default: '#1e1e1e',
  Accent: '#c4b5fd',
  Danger: '#f87171',
  Info: '#38bdf8',
  Success: '#4ade80',
  Highlight: '#fbbf24',
}

describe('createAppTheme', () => {
  it('maps the site theme palette slots to their MUI palette keys', () => {
    const theme = createAppTheme('dark', DARK_PALETTE)
    expect(theme.palette.primary.main).toBe(DARK_PALETTE.Accent)
    expect(theme.palette.secondary.main).toBe(DARK_PALETTE.Info)
    expect(theme.palette.error.main).toBe(DARK_PALETTE.Danger)
    expect(theme.palette.success.main).toBe(DARK_PALETTE.Success)
    expect(theme.palette.warning.main).toBe(DARK_PALETTE.Highlight)
    expect(theme.palette.background.default).toBe(DARK_PALETTE.Default)
  })

  it('maps the light palette slots identically', () => {
    const theme = createAppTheme('light', LIGHT_PALETTE)
    expect(theme.palette.mode).toBe('light')
    expect(theme.palette.primary.main).toBe(LIGHT_PALETTE.Accent)
    expect(theme.palette.secondary.main).toBe(LIGHT_PALETTE.Info)
    expect(theme.palette.background.default).toBe(LIGHT_PALETTE.Default)
  })

  it('falls back to stock MUI colors when no palette has loaded yet', () => {
    const theme = createAppTheme('light')
    const baseline = createTheme({ palette: { mode: 'light' } })
    expect(theme.palette.primary.main).toBe(baseline.palette.primary.main)
    expect(theme.palette.background.default).toBe(baseline.palette.background.default)
  })

  it('accepts a custom font family', () => {
    const theme = createAppTheme('light', LIGHT_PALETTE, 'Georgia, serif')
    expect(theme.typography.fontFamily).toBe('Georgia, serif')
  })

  it('defines the Phase 1 typographic scale', () => {
    const theme = createAppTheme('light', LIGHT_PALETTE)
    expect(theme.typography.h1.fontSize).toBe('2rem')
    expect(theme.typography.h2.fontSize).toBe('1.5rem')
    expect(theme.typography.h3.fontSize).toBe('1.25rem')
  })

  it('stops nonessential motion when the user requests reduced motion', () => {
    const baseline = createAppTheme('light', LIGHT_PALETTE).components?.MuiCssBaseline
    expect(baseline?.styleOverrides).toMatchObject({
      '@media (prefers-reduced-motion: reduce)': {
        '*, *::before, *::after': {
          animationIterationCount: '1 !important',
          scrollBehavior: 'auto !important',
        },
      },
    })
  })

  it('retains the stock MUI spacing and shape', () => {
    for (const mode of ['light', 'dark'] as const) {
      const theme = createAppTheme(mode, mode === 'dark' ? DARK_PALETTE : LIGHT_PALETTE)
      const baseline = createTheme({ palette: { mode } })
      expect(theme.spacing(1)).toBe(baseline.spacing(1))
      expect(theme.shape.borderRadius).toBe(baseline.shape.borderRadius)
    }
  })
})
