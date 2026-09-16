import type { CSSProperties } from 'react'

/** OBS browser-source sizes a streamer is likely to start from, in CSS pixels. */
export const OVERLAY_PREVIEW_SIZES = [
  { id: 'corner', label: 'Corner box · 480×270', width: 480, height: 270 },
  { id: 'sidebar', label: 'Sidebar · 400×720', width: 400, height: 720 },
  { id: 'bar', label: 'Wide bar · 960×220', width: 960, height: 220 },
  { id: 'full', label: 'Full canvas · 1920×1080', width: 1920, height: 1080 },
] as const

export const CUSTOM_SIZE_ID = 'custom'
export type OverlayPreviewSizeId =
  (typeof OVERLAY_PREVIEW_SIZES)[number]['id'] | typeof CUSTOM_SIZE_ID
export const CUSTOM_SIZE_LABEL = 'Custom'
export const OVERLAY_PREVIEW_SIZE_LIMITS = { min: 100, max: 3840 } as const

/** What is drawn behind the transparent frame, so panel opacity and theme can be judged. */
export const OVERLAY_PREVIEW_BACKDROPS = ['checker', 'dark', 'light'] as const
export type OverlayPreviewBackdrop = (typeof OVERLAY_PREVIEW_BACKDROPS)[number]
export const OVERLAY_PREVIEW_BACKDROP_LABELS: Record<OverlayPreviewBackdrop, string> = {
  checker: 'Checkerboard',
  dark: 'Dark scene',
  light: 'Light scene',
}
export const OVERLAY_PREVIEW_BACKDROP_STYLES: Record<OverlayPreviewBackdrop, CSSProperties> = {
  checker: {
    backgroundColor: '#9a9a9a',
    backgroundImage:
      'linear-gradient(45deg, #6f6f6f 25%, transparent 25%, transparent 75%, #6f6f6f 75%), linear-gradient(45deg, #6f6f6f 25%, transparent 25%, transparent 75%, #6f6f6f 75%)',
    backgroundSize: '24px 24px',
    backgroundPosition: '0 0, 12px 12px',
  },
  dark: { backgroundColor: '#14161c' },
  light: { backgroundColor: '#e9ecf1' },
}
