import { describe, it, expect } from 'vitest'
import { resolveSiteThemePalette, SITE_FONT_STACKS, DEFAULT_SITE_FONT } from './paletteMapping'
import { SITE_THEME_PALETTE_SLOTS, type SiteFont, type SiteTheme } from '../../../types'

const makeTheme = (overrides: Partial<SiteTheme> = {}): SiteTheme => ({
  backgroundAssetId: null,
  backgroundUrl: null,
  backgroundTreatment: 'None',
  font: 'SystemSansSerif',
  lightDefault: '#111111',
  lightAccent: '#222222',
  lightDanger: '#333333',
  lightInfo: '#444444',
  lightSuccess: '#555555',
  lightHighlight: '#666666',
  darkDefault: '#aaaaaa',
  darkAccent: '#bbbbbb',
  darkDanger: '#cccccc',
  darkInfo: '#dddddd',
  darkSuccess: '#eeeeee',
  darkHighlight: '#ffffff',
  updatedAt: '2026-01-01T00:00:00Z',
  ...overrides,
})

describe('resolveSiteThemePalette', () => {
  it('resolves every one of the six slots to a defined value when no theme has loaded', () => {
    for (const mode of ['light', 'dark'] as const) {
      const palette = resolveSiteThemePalette(undefined, mode)
      for (const slot of SITE_THEME_PALETTE_SLOTS) {
        expect(palette[slot]).toBeTruthy()
      }
    }
  })

  it('resolves every one of the six slots to a defined value from a fetched theme', () => {
    const theme = makeTheme()
    for (const mode of ['light', 'dark'] as const) {
      const palette = resolveSiteThemePalette(theme, mode)
      for (const slot of SITE_THEME_PALETTE_SLOTS) {
        expect(palette[slot]).toBeTruthy()
      }
    }
  })

  it('maps light slots from the theme’s Light* fields', () => {
    const theme = makeTheme()
    const palette = resolveSiteThemePalette(theme, 'light')
    expect(palette).toEqual({
      Default: theme.lightDefault,
      Accent: theme.lightAccent,
      Danger: theme.lightDanger,
      Info: theme.lightInfo,
      Success: theme.lightSuccess,
      Highlight: theme.lightHighlight,
    })
  })

  it('maps dark slots from the theme’s Dark* fields', () => {
    const theme = makeTheme()
    const palette = resolveSiteThemePalette(theme, 'dark')
    expect(palette).toEqual({
      Default: theme.darkDefault,
      Accent: theme.darkAccent,
      Danger: theme.darkDanger,
      Info: theme.darkInfo,
      Success: theme.darkSuccess,
      Highlight: theme.darkHighlight,
    })
  })

  it('substitutes the fallback value for a slot that is not a valid hex colour', () => {
    const theme = makeTheme({ darkAccent: 'not-a-color' })
    const palette = resolveSiteThemePalette(theme, 'dark')
    expect(palette.Accent).not.toBe('not-a-color')
    expect(palette.Accent).toMatch(/^#[0-9a-fA-F]{6}$/)
    // Every other slot is unaffected.
    expect(palette.Default).toBe(theme.darkDefault)
  })

  it('substitutes the fallback value for an empty or javascript: slot', () => {
    const theme = makeTheme({ lightDanger: '', lightHighlight: 'javascript:alert(1)' })
    const palette = resolveSiteThemePalette(theme, 'light')
    expect(palette.Danger).toMatch(/^#[0-9a-fA-F]{6}$/)
    expect(palette.Highlight).toMatch(/^#[0-9a-fA-F]{6}$/)
  })

  it('accepts short (#rgb), alpha (#rrggbbaa) and (#rgba) hex forms', () => {
    const theme = makeTheme({ darkAccent: '#abc', darkDanger: '#aabbccdd', darkInfo: '#abcd' })
    const palette = resolveSiteThemePalette(theme, 'dark')
    expect(palette.Accent).toBe('#abc')
    expect(palette.Danger).toBe('#aabbccdd')
    expect(palette.Info).toBe('#abcd')
  })
})

describe('SITE_FONT_STACKS', () => {
  it('defines a CSS stack for every allowed font, none of them a remote URL', () => {
    const fonts: readonly SiteFont[] = ['SystemSansSerif', 'SystemSerif', 'SystemMonospace']
    for (const font of fonts) {
      const stack = SITE_FONT_STACKS[font]
      expect(stack).toBeTruthy()
      expect(stack).not.toMatch(/url\(|https?:\/\//i)
    }
  })

  it('has a default font present in the stack table', () => {
    expect(SITE_FONT_STACKS[DEFAULT_SITE_FONT]).toBeTruthy()
  })
})
