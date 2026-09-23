import { useQuery } from '@tanstack/react-query'
import { eventsApi, EVENTS_QUERY_KEYS } from '../api/eventsApi'
import { SCOREBOARD_REFRESH_INTERVAL_MS } from '../scoreboard/scoreboardPolling'

interface UseScoreboardOptions {
  /** Poll at the shared scoreboard interval. */
  live?: boolean
  /**
   * False to skip the request entirely — the scoreboard surfaces pass
   * `event.isStarted`, since an unstarted event has no scores to show.
   */
  enabled?: boolean
}

export const useScoreboard = (
  eventId: string,
  { live = false, enabled = true }: UseScoreboardOptions = {},
) =>
  useQuery({
    queryKey: EVENTS_QUERY_KEYS.scoreboard(eventId),
    queryFn: () => eventsApi.getScoreboard(eventId),
    enabled: !!eventId && enabled,
    refetchInterval: live ? SCOREBOARD_REFRESH_INTERVAL_MS : false,
  })
