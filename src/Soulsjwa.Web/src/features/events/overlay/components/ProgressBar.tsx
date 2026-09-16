import type { OverlayPalette } from '../overlayPalette'

interface ProgressBarProps {
  /** 0–100. */
  percent: number
  palette: OverlayPalette
  /** Fill colour; the theme's accent unless the figures are a trial's. */
  color: string
  animation: string
  /** Accessible name, e.g. "Chosen Undead completion progress". */
  label: string
}

/** Bar height in CSS pixels — thick enough to read over a game capture. */
const BAR_HEIGHT = 8

/**
 * The overlay's one progress bar, shared by the scores/games rows and the
 * objectives view's header so both read the same. Drawn over a visible
 * track with a thin edge so it stands out on any scene behind the panel.
 */
export function ProgressBar({ percent, palette, color, animation, label }: ProgressBarProps) {
  return (
    <div
      role="progressbar"
      aria-label={label}
      aria-valuemin={0}
      aria-valuemax={100}
      aria-valuenow={percent}
      style={{
        height: BAR_HEIGHT,
        width: '100%',
        background: palette.progressTrack,
        border: `1px solid ${palette.panelBorder}`,
        borderRadius: 999,
        overflow: 'hidden',
        boxSizing: 'border-box',
      }}
    >
      <div
        style={{
          height: '100%',
          width: `${percent}%`,
          background: color,
          boxShadow: `0 0 6px ${color}`,
          borderRadius: 999,
          transition: animation,
        }}
      />
    </div>
  )
}
