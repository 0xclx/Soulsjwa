import { useEffect, useRef, useState } from 'react'
import { objectiveState } from '../../features/events/scoreboard/scoreboardMetrics'
import { formatIngameTime } from '../../features/events/scoreboard/formatIngameTime'
import type { ObjectiveDetail } from '../../types'
import type {
  TwitchExtensionCompetitorDetail,
  TwitchExtensionEntry,
  TwitchExtensionGame,
} from '../../types/twitchExtension'
import { twitchExtensionClient } from '../api/twitchExtensionClient'

interface CompetitorDetailProps {
  token: string
  entry: TwitchExtensionEntry
  games: TwitchExtensionGame[]
  activeEventGameId: string | null
  showTrial: boolean
}

export const SHOW_OBJECTIVES_LABEL = 'Show objectives'
export const HIDE_OBJECTIVES_LABEL = 'Hide objectives'
export const NOW_TAG = 'now'
export const IGT_LABEL = 'IGT'
const UNCATEGORIZED_LABEL = 'Other'
const LOADING_LABEL = 'Loading objectives…'
const FAILED_LABEL = 'Could not load objectives.'

type DetailState =
  | { status: 'idle' }
  | { status: 'loading' }
  | { status: 'ready'; detail: TwitchExtensionCompetitorDetail }
  | { status: 'failed' }

/**
 * The per-game breakdown under an expanded row, with the objective list a
 * further tap away: the polled payload carries no objectives, so this is the
 * only place they are fetched, and only for the competitor a viewer asked about.
 */
export function CompetitorDetail({
  token,
  entry,
  games,
  activeEventGameId,
  showTrial,
}: CompetitorDetailProps) {
  const [wantObjectives, setWantObjectives] = useState(false)
  const [state, setState] = useState<DetailState>({ status: 'idle' })
  const mounted = useRef(true)

  useEffect(() => {
    mounted.current = true
    return () => {
      mounted.current = false
    }
  }, [])

  // Fetched once, on the first tap; a later hide/show just re-renders what was loaded.
  const toggleObjectives = () => {
    setWantObjectives((v) => !v)
    if (state.status !== 'idle') return
    setState({ status: 'loading' })
    twitchExtensionClient
      .competitorDetail(token, entry.userId)
      .then((result) => {
        if (!mounted.current) return
        setState(
          result.status === 'ok' ? { status: 'ready', detail: result.data } : { status: 'failed' },
        )
      })
      .catch(() => {
        if (mounted.current) setState({ status: 'failed' })
      })
  }

  return (
    <div className="sx-detail">
      <ul className="sx-detail__games">
        {games.map((game) => {
          const figures = entry.games.find((g) => g.eventGameId === game.eventGameId)
          const trial = showTrial ? figures?.trial : null
          return (
            <li key={game.eventGameId} className="sx-detail__game">
              <span className="sx-detail__game-name">
                {game.name}
                {game.eventGameId === activeEventGameId && (
                  <em className="sx-detail__now">{NOW_TAG}</em>
                )}
              </span>
              <span className="sx-detail__figures">
                {figures?.score ?? 0} pts · {figures?.completedCount ?? 0}/{game.totalObjectives}
                {figures && figures.failedCount > 0 && ` · ✕${figures.failedCount}`}
                {trial && <span className="sx-detail__trial"> · trial {trial.score}</span>}
              </span>
            </li>
          )
        })}
      </ul>
      {entry.totalInGameTimeMs !== null && (
        <div className="sx-detail__igt">
          {IGT_LABEL} {formatIngameTime(entry.totalInGameTimeMs)}
        </div>
      )}
      <button
        type="button"
        className="sx-detail__toggle"
        aria-expanded={wantObjectives}
        onClick={toggleObjectives}
      >
        {wantObjectives ? HIDE_OBJECTIVES_LABEL : SHOW_OBJECTIVES_LABEL} ›
      </button>
      {wantObjectives && state.status === 'loading' && (
        <p className="sx-detail__note">{LOADING_LABEL}</p>
      )}
      {wantObjectives && state.status === 'failed' && (
        <p className="sx-detail__note">{FAILED_LABEL}</p>
      )}
      {wantObjectives && state.status === 'ready' && (
        <ObjectiveList detail={state.detail} showTrial={showTrial} />
      )}
    </div>
  )
}

function groupByCategory(objectives: ObjectiveDetail[]): Array<[string, ObjectiveDetail[]]> {
  const groups = new Map<string, ObjectiveDetail[]>()
  for (const objective of objectives) {
    const category = objective.category?.trim() || UNCATEGORIZED_LABEL
    const bucket = groups.get(category)
    if (bucket) bucket.push(objective)
    else groups.set(category, [objective])
  }
  return [...groups]
}

function ObjectiveList({
  detail,
  showTrial,
}: {
  detail: TwitchExtensionCompetitorDetail
  showTrial: boolean
}) {
  return (
    <div className="sx-objectives">
      {detail.games.map((game) => (
        <section key={game.eventGameId} className="sx-objectives__game">
          <h4>{game.gameName}</h4>
          {groupByCategory(game.objectives).map(([category, objectives]) => (
            <div key={category}>
              <h5>{category}</h5>
              <ul>
                {objectives.map((objective) => {
                  // A recording trial's marks stand in for the official ones,
                  // the same substitution the app and the overlay make.
                  const mark = objectiveState(objective, showTrial && game.isTrialActive)
                  return (
                    <li
                      key={objective.objectiveId}
                      data-done={mark.isCompleted || undefined}
                      data-failed={mark.isFailed || undefined}
                    >
                      <span className="sx-objectives__mark" aria-hidden="true">
                        {mark.isCompleted ? '✓' : mark.isFailed ? '✕' : '·'}
                      </span>
                      <span className="sx-objectives__name">{objective.name}</span>
                      <span className="sx-objectives__score">{objective.score}</span>
                    </li>
                  )
                })}
              </ul>
            </div>
          ))}
        </section>
      ))}
    </div>
  )
}
