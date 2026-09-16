const OVERLAY_ACCENT = '#9146ff'

export interface OverlayPalette {
  panelBg: string
  panelBorder: string
  text: string
  subText: string
  rowBg: string
  rowAltBg: string
  accent: string
  highlight: string
  progressTrack: string
}

export const DARK_THEME: OverlayPalette = {
  panelBg: 'rgba(18, 19, 24, 0.78)',
  panelBorder: 'rgba(255, 255, 255, 0.08)',
  text: '#ffffff',
  subText: 'rgba(255, 255, 255, 0.65)',
  rowBg: 'rgba(255, 255, 255, 0.04)',
  rowAltBg: 'rgba(255, 255, 255, 0.02)',
  accent: OVERLAY_ACCENT,
  highlight: 'rgba(145, 70, 255, 0.35)',
  progressTrack: 'rgba(255, 255, 255, 0.2)',
}

export const LIGHT_THEME: OverlayPalette = {
  panelBg: 'rgba(255, 255, 255, 0.85)',
  panelBorder: 'rgba(0, 0, 0, 0.08)',
  text: '#16171d',
  subText: 'rgba(22, 23, 29, 0.65)',
  rowBg: 'rgba(0, 0, 0, 0.03)',
  rowAltBg: 'rgba(0, 0, 0, 0.015)',
  accent: OVERLAY_ACCENT,
  highlight: 'rgba(145, 70, 255, 0.22)',
  progressTrack: 'rgba(0, 0, 0, 0.16)',
}

/**
 * Re-alphas a theme's `panelBg` (always an `rgba(r, g, b, a)` literal here)
 * to the streamer-chosen opacity, keeping the theme's own panel color. Falls
 * back to the color unchanged if it isn't in that form.
 */
export function withPanelOpacity(panelBg: string, opacityPercent: number): string {
  const match = panelBg.match(/^rgba\(\s*([\d.]+)\s*,\s*([\d.]+)\s*,\s*([\d.]+)\s*,\s*[\d.]+\s*\)$/)
  if (!match) return panelBg
  const alpha = Math.min(1, Math.max(0, opacityPercent / 100))
  return `rgba(${match[1]}, ${match[2]}, ${match[3]}, ${alpha.toFixed(2)})`
}

export const COMPLETED_STATUS_MARK = '✓'

export const FAILED_STATUS_MARK = '✗'
