import type { ObjectiveItem } from '../overlayScope'
import { objectiveState } from '../../scoreboard/scoreboardMetrics'
import {
  TRIAL_ACCENT_HEX,
  TRIAL_BADGE_TEXT_OVERLAY,
  TRIAL_TOOLTIP,
} from '../../scoreboard/trialPresentation'
import { COMPLETED_STATUS_MARK, FAILED_STATUS_MARK, type OverlayPalette } from '../overlayPalette'

interface ObjectivesViewProps {
  items: ObjectiveItem[]
  palette: OverlayPalette
  /** Objective ids whose completion just landed; these animate in rather than appearing done. */
  fresh?: ReadonlySet<string>
  /** How long the strikethrough takes to sweep across a fresh completion. */
  strikeSeconds?: number
}

const TICK_POP_MS = 450
const ROW_FADE_MS = 700

/**
 * Renders one page of the `objectives` view: a flat list of objectives with
 * repeated Game and Category headers whenever either changes. Completed
 * objectives are struck through and their score is shown so viewers can read
 * the run as a live checklist; failed objectives are struck through in red.
 *
 * A completion that just landed (`fresh`) is drawn in rather than switched
 * on, so it catches the eye: the row's background fades in, the tick pops,
 * and the strikethrough sweeps across the name over `strikeSeconds` before
 * settling into the ordinary struck-through row.
 */
export function ObjectivesView({
  items,
  palette,
  fresh,
  strikeSeconds = 3.5,
}: ObjectivesViewProps) {
  if (items.length === 0) {
    return (
      <div
        style={{
          padding: 24,
          textAlign: 'center',
          color: palette.subText,
          fontSize: 'clamp(12px, 1.6vw, 14px)',
        }}
      >
        No objectives yet.
      </div>
    )
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
      <style>{`
        @keyframes overlayObjectiveRowFade { from { background-color: transparent } }
        @keyframes overlayObjectiveTickPop {
          0% { transform: scale(0); opacity: 0 }
          60% { transform: scale(1.35); opacity: 1 }
          100% { transform: scale(1); opacity: 1 }
        }
        @keyframes overlayObjectiveStrike { from { transform: scaleX(0) } to { transform: scaleX(1) } }
      `}</style>
      {items.map((item, idx) => {
        const prev = idx > 0 ? items[idx - 1] : undefined
        const showGameHeader = !prev || prev.gameId !== item.gameId
        const showCategoryHeader = showGameHeader || !prev || prev.category !== item.category
        const { objective } = item
        const state = objectiveState(objective, item.isTrial)
        const done = state.isCompleted
        const failed = state.isFailed
        const isFresh = done && !failed && (fresh?.has(objective.objectiveId) ?? false)
        return (
          <div key={objective.objectiveId}>
            {showGameHeader && (
              <div
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  gap: 6,
                  fontWeight: 700,
                  fontSize: 'clamp(13px, 1.9vw, 17px)',
                  color: palette.text,
                  marginTop: idx === 0 ? 0 : 8,
                  paddingTop: 4,
                }}
              >
                <span>{item.gameName}</span>
                {/* This view draws the trial's marks, so it has to carry its
                    own badge — the scores view's one is not on screen here,
                    and the title row above shows the official score. */}
                {item.isTrial && (
                  <span
                    title={TRIAL_TOOLTIP}
                    style={{
                      fontSize: '0.6em',
                      fontWeight: 700,
                      letterSpacing: '0.05em',
                      color: TRIAL_ACCENT_HEX,
                      border: `1px solid ${TRIAL_ACCENT_HEX}`,
                      borderRadius: 4,
                      padding: '1px 4px',
                      flexShrink: 0,
                    }}
                  >
                    {TRIAL_BADGE_TEXT_OVERLAY}
                  </span>
                )}
              </div>
            )}
            {showCategoryHeader && (
              <div
                style={{
                  fontWeight: 600,
                  fontSize: 'clamp(10px, 1.4vw, 13px)',
                  color: palette.accent,
                  textTransform: 'uppercase',
                  letterSpacing: '0.04em',
                  marginTop: 4,
                  marginBottom: 2,
                }}
              >
                {item.category}
              </div>
            )}
            <div
              data-completed={done ? 'true' : undefined}
              data-failed={failed ? 'true' : undefined}
              data-fresh={isFresh ? 'true' : undefined}
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 8,
                padding: '3px 10px',
                borderRadius: 6,
                background: done ? palette.rowBg : 'transparent',
                fontSize: 'clamp(12px, 1.7vw, 15px)',
                animation: isFresh
                  ? `overlayObjectiveRowFade ${ROW_FADE_MS}ms ease-out`
                  : undefined,
              }}
            >
              <span
                aria-hidden
                style={{
                  width: 16,
                  textAlign: 'center',
                  color: failed ? '#ef4444' : done ? '#22c55e' : palette.subText,
                  flexShrink: 0,
                  display: 'inline-block',
                  animation: isFresh
                    ? `overlayObjectiveTickPop ${TICK_POP_MS}ms ease-out`
                    : undefined,
                }}
              >
                {failed ? FAILED_STATUS_MARK : done ? COMPLETED_STATUS_MARK : '○'}
              </span>
              <span style={{ flex: 1, minWidth: 0 }}>
                {/* The strike is always our own line, sized to the text, so a
                    fresh completion's sweep ends exactly where the settled row's
                    line is — handing over to text-decoration changed the line's
                    length and position at the very moment the hold ended. */}
                <span
                  style={{
                    display: 'inline-block',
                    maxWidth: '100%',
                    overflow: 'hidden',
                    textOverflow: 'ellipsis',
                    whiteSpace: 'nowrap',
                    verticalAlign: 'bottom',
                    position: 'relative',
                    color: failed ? '#ef4444' : done ? palette.subText : palette.text,
                  }}
                >
                  {objective.name}
                  {(done || failed) && (
                    <span
                      aria-hidden
                      data-testid="objective-strike"
                      data-sweeping={isFresh ? 'true' : undefined}
                      style={{
                        position: 'absolute',
                        left: 0,
                        right: 0,
                        top: '50%',
                        height: '0.08em',
                        minHeight: 1,
                        background: 'currentColor',
                        transformOrigin: 'left center',
                        animation: isFresh
                          ? `overlayObjectiveStrike ${strikeSeconds}s ease-in-out forwards`
                          : undefined,
                      }}
                    />
                  )}
                </span>
              </span>
              <span
                style={{
                  fontWeight: 700,
                  fontVariantNumeric: 'tabular-nums',
                  color: done ? palette.accent : palette.subText,
                  flexShrink: 0,
                }}
              >
                {objective.score}
              </span>
            </div>
          </div>
        )
      })}
    </div>
  )
}
