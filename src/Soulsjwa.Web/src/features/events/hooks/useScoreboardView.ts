import { useCallback } from 'react'
import { useSearchParams } from 'react-router-dom'
import {
  DEFAULT_SCOREBOARD_VIEW,
  SCOREBOARD_VIEW_PARAM,
  parseScoreboardView,
  type ScoreboardView,
} from '../scoreboard/scoreboardView'

/**
 * The scoreboard view chosen in `?view=`, so a shared link opens the same
 * view. Switching replaces the history entry (the back button leaves the
 * page, not the toggle) and keeps every other param. The default view is
 * written as no param at all.
 */
export function useScoreboardView(): [ScoreboardView, (view: ScoreboardView) => void] {
  const [searchParams, setSearchParams] = useSearchParams()
  const view = parseScoreboardView(searchParams.get(SCOREBOARD_VIEW_PARAM))

  const setView = useCallback(
    (next: ScoreboardView) => {
      setSearchParams(
        (current) => {
          const params = new URLSearchParams(current)
          if (next === DEFAULT_SCOREBOARD_VIEW) params.delete(SCOREBOARD_VIEW_PARAM)
          else params.set(SCOREBOARD_VIEW_PARAM, next)
          return params
        },
        { replace: true },
      )
    },
    [setSearchParams],
  )

  return [view, setView]
}
