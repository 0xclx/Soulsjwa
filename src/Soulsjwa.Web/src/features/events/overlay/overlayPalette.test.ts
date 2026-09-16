import { describe, expect, it } from 'vitest'
import { DARK_THEME, LIGHT_THEME, withPanelOpacity } from './overlayPalette'

describe('withPanelOpacity', () => {
  it('replaces the alpha channel while keeping the theme panel color', () => {
    expect(withPanelOpacity(DARK_THEME.panelBg, 50)).toBe('rgba(18, 19, 24, 0.50)')
    expect(withPanelOpacity(LIGHT_THEME.panelBg, 100)).toBe('rgba(255, 255, 255, 1.00)')
  })

  it('clamps out-of-range percentages to 0–1', () => {
    expect(withPanelOpacity(DARK_THEME.panelBg, -10)).toBe('rgba(18, 19, 24, 0.00)')
    expect(withPanelOpacity(DARK_THEME.panelBg, 250)).toBe('rgba(18, 19, 24, 1.00)')
  })

  it('leaves an unrecognized color unchanged', () => {
    expect(withPanelOpacity('#123456', 50)).toBe('#123456')
  })
})
