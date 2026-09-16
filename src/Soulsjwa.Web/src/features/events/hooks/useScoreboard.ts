import { useQuery } from '@tanstack/react-query'
import { eventsApi, EVENTS_QUERY_KEYS } from '../api/eventsApi'

export const useScoreboard = (eventId: string, live = false) =>
  useQuery({
    queryKey: EVENTS_QUERY_KEYS.scoreboard(eventId),
    queryFn: () => eventsApi.getScoreboard(eventId),
    enabled: !!eventId,
    refetchInterval: live ? 5000 : false,
  })
