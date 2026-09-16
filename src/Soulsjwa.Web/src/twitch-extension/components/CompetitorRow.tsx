import type { BoardRow } from '../scoreboard/scope'

interface CompetitorRowProps {
  row: BoardRow
  flashing: boolean
  expanded: boolean
  onToggle: () => void
  children?: React.ReactNode
}

export const YOU_TAG = 'YOU'
export const FINISHED_TAG = 'FINISHED'
export const TRIAL_PREFIX = 'trial'

const initials = (name: string): string => name.slice(0, 2).toUpperCase()

/**
 * One competitor: rank, avatar with live dot, name, completion bar and
 * score. Tapping expands the per-game detail passed as children. The
 * broadcaster's own row is marked when they compete in the event.
 */
export function CompetitorRow({ row, flashing, expanded, onToggle, children }: CompetitorRowProps) {
  const percent =
    row.totalObjectives > 0 ? Math.round((row.completedCount / row.totalObjectives) * 100) : 0
  return (
    <li
      className="sx-row"
      data-you={row.isChannelCompetitor || undefined}
      data-flash={flashing || undefined}
      data-expanded={expanded || undefined}
    >
      <button
        type="button"
        className="sx-row__main"
        aria-expanded={expanded}
        aria-label={`${row.displayName}, rank ${row.rank}, ${row.score} points`}
        onClick={onToggle}
      >
        <span className={`sx-row__rank${row.rank === 1 ? ' sx-row__rank--top' : ''}`}>
          {row.rank}
        </span>
        <span
          className={`sx-row__avatar${row.isLive ? ' sx-row__avatar--live' : ''}`}
          aria-hidden="true"
        >
          {row.profileImageUrl ? (
            <img src={row.profileImageUrl} alt="" loading="lazy" />
          ) : (
            initials(row.displayName)
          )}
        </span>
        <span className="sx-row__who">
          <span className="sx-row__name">
            {row.displayName}
            {row.isChannelCompetitor && <span className="sx-row__you">{YOU_TAG}</span>}
          </span>
          <span className="sx-row__meta">
            <span className="sx-row__bar" aria-hidden="true">
              <i style={{ width: `${percent}%` }} />
            </span>
            <span>
              {row.completedCount}/{row.totalObjectives}
            </span>
            {row.failedCount > 0 && <span className="sx-row__failed">✕ {row.failedCount}</span>}
          </span>
        </span>
        <span className="sx-row__score">
          <b>{row.score}</b>
          {row.trialScore !== null ? (
            <span className="sx-row__trial">
              {TRIAL_PREFIX} +{row.trialScore}
            </span>
          ) : (
            row.isFinished && <span className="sx-row__finished">{FINISHED_TAG}</span>
          )}
        </span>
      </button>
      {expanded && <div className="sx-row__detail">{children}</div>}
    </li>
  )
}
