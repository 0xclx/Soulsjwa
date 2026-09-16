import { useEffect, useMemo, useState } from 'react'
import type { TwitchExtensionScoreboard } from '../../types/twitchExtension'
import { scoreboardPageUrl } from '../api/twitchExtensionClient'
import { buildBoardView, initialScope, scopedGame, type BoardScope } from '../scoreboard/scope'
import { useChangeFlash } from '../scoreboard/useChangeFlash'
import { CompetitorDetail } from './CompetitorDetail'
import { CompetitorRow } from './CompetitorRow'
import { ScopeSwitcher } from './ScopeSwitcher'

interface BoardProps {
  board: TwitchExtensionScoreboard
  token: string
  lastUpdatedAt: number | null
  error: string | null
  /** The video component gets a shorter list with the broadcaster kept in view. */
  compact?: boolean
}

export const LIVE_TAG = 'Live'
export const UPCOMING_TAG = 'Upcoming'
export const FINISHED_EVENT_TAG = 'Finished'
export const FEATURED_NOTE = 'featured event'
export const FULL_SCOREBOARD_LABEL = 'Full scoreboard ↗'
export const TAP_HINT = 'Tap a player for detail'
export const NO_ROWS_LABEL = 'No competitors yet.'
export const NO_GAME_LABEL = 'No game to show for this scope.'
const PLAYERS_SUFFIX = 'players'
const UPDATED_PREFIX = 'updated'
/** Rows the compact board shows before it stops, the broadcaster's own row always included. */
const COMPACT_ROW_LIMIT = 5

const eventStatus = (board: TwitchExtensionScoreboard, rows: { isFinished: boolean }[]): string => {
  const ev = board.event
  if (!ev) return ''
  if (ev.isStarted) return LIVE_TAG
  return rows.length > 0 && rows.every((r) => r.isFinished) ? FINISHED_EVENT_TAG : UPCOMING_TAG
}

const ago = (at: number | null, now: number): string => {
  if (at === null) return ''
  const seconds = Math.max(0, Math.round((now - at) / 1000))
  return `${UPDATED_PREFIX} ${seconds < 60 ? `${seconds} s` : `${Math.round(seconds / 60)} min`} ago`
}

/** The panel proper: header, scope switcher, ranked rows and footer. */
export function Board({ board, token, lastUpdatedAt, error, compact = false }: BoardProps) {
  const [scope, setScope] = useState<BoardScope>(() => initialScope(board.settings, board.games))
  const [expanded, setExpanded] = useState<string | null>(null)
  const [now, setNow] = useState(() => Date.now())

  // A broadcaster changing the default while viewers watch re-seeds the scope.
  const settingsKey = `${board.settings.defaultScope}:${board.settings.pinnedEventGameId ?? ''}:${board.event?.id ?? ''}`
  const [seededFrom, setSeededFrom] = useState(settingsKey)
  if (seededFrom !== settingsKey) {
    setSeededFrom(settingsKey)
    setScope(initialScope(board.settings, board.games))
  }

  useEffect(() => {
    const handle = window.setInterval(() => setNow(Date.now()), 1000)
    return () => window.clearInterval(handle)
  }, [])

  const view = useMemo(() => buildBoardView(board, scope), [board, scope])
  const flashing = useChangeFlash(view.rows)
  const activeGame = scopedGame({ kind: 'active' }, board)
  const status = eventStatus(board, view.rows)

  const rows = useMemo(() => {
    if (!compact) return view.rows
    const top = view.rows.slice(0, COMPACT_ROW_LIMIT)
    const you = view.rows.find((r) => r.isChannelCompetitor)
    return you && !top.includes(you) ? [...top, you] : top
  }, [compact, view.rows])

  const link = board.event ? scoreboardPageUrl(board.event) : null

  return (
    <div className={`sx-board${compact ? ' sx-board--compact' : ''}`}>
      <header className="sx-board__head">
        <div className="sx-board__title">
          <strong title={board.event?.name}>{board.event?.name}</strong>
          {status && <span className={`sx-pill sx-pill--${status.toLowerCase()}`}>{status}</span>}
        </div>
        <div className="sx-board__sub">
          <span>
            {view.game ? view.game.name : `${board.entries.length} ${PLAYERS_SUFFIX}`}
            {board.event?.source === 'Featured' && !compact && ` · ${FEATURED_NOTE}`}
          </span>
          <span className={error ? 'sx-board__stale' : undefined} title={error ?? undefined}>
            {error ? error : ago(lastUpdatedAt, now)}
          </span>
        </div>
        {board.policy.allowViewerScopeSwitch && (
          <ScopeSwitcher
            scope={scope}
            games={board.games}
            activeGame={activeGame}
            onChange={setScope}
            compact={compact}
          />
        )}
      </header>
      <ol className="sx-board__rows" aria-label="Scoreboard">
        {rows.map((row) => (
          <CompetitorRow
            key={row.userId}
            row={row}
            flashing={flashing.has(row.userId)}
            expanded={expanded === row.userId}
            onToggle={() => setExpanded((current) => (current === row.userId ? null : row.userId))}
          >
            {(() => {
              const entry = board.entries.find((e) => e.userId === row.userId)
              return entry ? (
                <CompetitorDetail
                  token={token}
                  entry={entry}
                  games={board.games}
                  activeEventGameId={board.event?.activeEventGameId ?? null}
                  showTrial={board.settings.showTrialProgress}
                />
              ) : null
            })()}
          </CompetitorRow>
        ))}
        {rows.length === 0 && (
          <li className="sx-board__empty">
            {view.game || scope.kind === 'all' ? NO_ROWS_LABEL : NO_GAME_LABEL}
          </li>
        )}
      </ol>
      {!compact && (
        <footer className="sx-board__foot">
          <span>{TAP_HINT}</span>
          {link && (
            <a href={link} target="_blank" rel="noopener noreferrer">
              {FULL_SCOREBOARD_LABEL}
            </a>
          )}
        </footer>
      )}
    </div>
  )
}
