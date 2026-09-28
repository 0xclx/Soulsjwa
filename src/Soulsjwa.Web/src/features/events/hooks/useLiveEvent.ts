import { useEffect, useRef } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { eventsApi, EVENTS_QUERY_KEYS } from '../api/eventsApi'
import { SCOREBOARD_REFRESH_INTERVAL_MS } from '../scoreboard/scoreboardPolling'
import type { EventResponse } from '../../../types'

/**
 * The event as the scoreboard surfaces should see it: re-read at the
 * scoreboard interval whether or not it has started, so starting it or
 * switching the enabled game shows without a reload. `event` (from whatever
 * query the surface already has, e.g. the featured event) is shown until the
 * first read lands.
 *
 * When `isStarted` or the enabled game changes, the scoreboard is invalidated
 * so the two polls resync at once instead of up to one interval apart.
 */
export const useLiveEvent = (event: EventResponse): EventResponse => {
  const queryClient = useQueryClient()
  const { data = event } = useQuery({
    queryKey: EVENTS_QUERY_KEYS.detail(event.id),
    queryFn: () => eventsApi.get(event.id),
    placeholderData: event,
    refetchInterval: SCOREBOARD_REFRESH_INTERVAL_MS,
  })

  const isStarted = data.isStarted
  const enabledGameId = data.games.find((game) => game.isEnabled)?.eventGameId ?? null
  const seen = useRef({ isStarted, enabledGameId })

  useEffect(() => {
    if (seen.current.isStarted === isStarted && seen.current.enabledGameId === enabledGameId) {
      return
    }
    seen.current = { isStarted, enabledGameId }
    void queryClient.invalidateQueries({ queryKey: EVENTS_QUERY_KEYS.scoreboard(data.id) })
  }, [isStarted, enabledGameId, data.id, queryClient])

  return data
}
