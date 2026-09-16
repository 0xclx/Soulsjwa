import {
  entryProgress,
  getCompletedObjectiveNames,
  formatCompletedObjectives,
  type ScopedEntry,
} from '../overlayScope'
import { trialGames, trialTotals } from '../../scoreboard/scoreboardMetrics'
import { ProgressBar } from './ProgressBar'
import {
  TRIAL_ACCENT_HEX,
  TRIAL_BADGE_TEXT_OVERLAY,
  TRIAL_TOOLTIP,
} from '../../scoreboard/trialPresentation'
import type { OverlayPalette } from '../overlayPalette'

interface ScoreRowProps {
  entry: ScopedEntry
  /** Row index within the current page, for the alternating row background. */
  index: number
  palette: OverlayPalette
  animation: string
  showProgress: boolean
  isFlashing: boolean
}

/** Renders one competitor's row in the `scores`/`games` overlay views. */
export function ScoreRow({
  entry,
  index,
  palette,
  animation,
  showProgress,
  isFlashing,
}: ScoreRowProps) {
  const trialing = trialGames(entry.games)
  const trials = trialTotals(trialing)
  const progress = entryProgress(entry)
  const substitutedTotals = progress.isTrial
  const { completedCount, failedCount, totalObjectives, percent: progressPct } = progress
  // The substituted figures are the trial's, so they must not be styled as
  // the official ones — the badge beside the name marks the competitor, not
  // this block, and is also present for a run whose marks are official.
  const figureColor = substitutedTotals ? TRIAL_ACCENT_HEX : palette.subText
  const barColor = substitutedTotals ? TRIAL_ACCENT_HEX : palette.accent
  const completedObjectives = formatCompletedObjectives(getCompletedObjectiveNames(entry))
  // trialTotals is non-null only when trialing is non-empty, so the game
  // names are always available to name what the amber figures belong to.
  const trialTitle = trials
    ? `${TRIAL_TOOLTIP} (${trialing.map((g) => g.gameName).join(', ')})`
    : undefined

  return (
    <div
      data-highlight={isFlashing ? 'true' : undefined}
      style={{
        display: 'grid',
        gridTemplateColumns: showProgress
          ? 'auto minmax(0, 1.2fr) minmax(120px, 0.8fr) auto'
          : 'auto 1fr auto auto',
        alignItems: 'center',
        gap: 12,
        padding: '8px 12px',
        background: isFlashing
          ? palette.highlight
          : index % 2 === 0
            ? palette.rowBg
            : palette.rowAltBg,
        borderRadius: 8,
        transition: animation,
        fontSize: 'clamp(12px, 1.8vw, 16px)',
      }}
    >
      <div
        style={{
          width: 28,
          textAlign: 'center',
          fontWeight: 700,
          color: entry.rank <= 3 ? palette.accent : palette.text,
          fontVariantNumeric: 'tabular-nums',
        }}
      >
        {entry.rank}
      </div>
      <div
        style={{
          display: 'grid',
          gap: 2,
          minWidth: 0,
        }}
      >
        <div
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: 8,
            minWidth: 0,
          }}
        >
          {entry.profileImageUrl && (
            <img
              src={entry.profileImageUrl}
              alt=""
              width={24}
              height={24}
              style={{ borderRadius: '50%' }}
            />
          )}
          <span
            style={{
              overflow: 'hidden',
              textOverflow: 'ellipsis',
              whiteSpace: 'nowrap',
              fontWeight: 600,
            }}
          >
            {entry.displayName}
          </span>
          <span
            title={entry.isLive ? 'Live' : 'Offline'}
            style={{
              width: 8,
              height: 8,
              borderRadius: '50%',
              background: entry.isLive ? '#22c55e' : '#ef4444',
              flexShrink: 0,
            }}
          />
          {trials && (
            <span
              title={trialTitle}
              style={{
                fontSize: '0.65em',
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
        {completedObjectives && (
          <div
            title={completedObjectives}
            style={{
              color: palette.subText,
              fontSize: '0.78em',
              overflow: 'hidden',
              textOverflow: 'ellipsis',
              whiteSpace: 'nowrap',
            }}
          >
            {completedObjectives}
          </div>
        )}
      </div>
      {showProgress ? (
        <div>
          <div
            style={{
              display: 'flex',
              justifyContent: 'space-between',
              gap: 8,
              color: figureColor,
              fontVariantNumeric: 'tabular-nums',
              fontSize: '0.85em',
              marginBottom: 4,
            }}
          >
            <span>
              {substitutedTotals && (
                <span style={{ fontWeight: 700, letterSpacing: '0.05em' }}>
                  {TRIAL_BADGE_TEXT_OVERLAY}{' '}
                </span>
              )}
              {completedCount}
              {totalObjectives > 0 && (
                <>
                  {' / '}
                  {totalObjectives}
                </>
              )}
              {failedCount > 0 && <span style={{ color: '#ef4444' }}> ({failedCount} failed)</span>}
            </span>
            <span>{progressPct}%</span>
          </div>
          <ProgressBar
            percent={progressPct}
            palette={palette}
            color={barColor}
            animation={animation}
            label={`${entry.displayName} completion progress`}
          />
        </div>
      ) : (
        <div
          style={{
            color: figureColor,
            fontVariantNumeric: 'tabular-nums',
            fontSize: '0.85em',
          }}
          title={substitutedTotals ? TRIAL_TOOLTIP : undefined}
        >
          {substitutedTotals && (
            <span style={{ fontWeight: 700, letterSpacing: '0.05em' }}>
              {TRIAL_BADGE_TEXT_OVERLAY}{' '}
            </span>
          )}
          {completedCount}
          {totalObjectives > 0 && (
            <>
              {' / '}
              {totalObjectives}
            </>
          )}
          {failedCount > 0 && <span style={{ color: '#ef4444' }}> ({failedCount} failed)</span>}
        </div>
      )}
      <div
        style={{
          fontWeight: 700,
          fontVariantNumeric: 'tabular-nums',
          color: palette.accent,
          minWidth: 48,
          textAlign: 'right',
        }}
      >
        {entry.totalScore}
        {entry.scoringMaxScore > 0 && (
          <span
            aria-label="of"
            style={{ color: palette.subText, fontWeight: 500, fontSize: '0.75em' }}
          >
            {' / '}
            {entry.scoringMaxScore}
          </span>
        )}
        {trials && (
          <div
            title={trialTitle}
            style={{ color: TRIAL_ACCENT_HEX, fontSize: '0.7em', letterSpacing: '0.05em' }}
          >
            {TRIAL_BADGE_TEXT_OVERLAY} {trials.score}
          </div>
        )}
      </div>
    </div>
  )
}
