import { useQuery } from '@tanstack/react-query'
import { eventsApi, EVENTS_QUERY_KEYS } from '../api/eventsApi'

export const useEventScores = (eventId: string, live = false) =>
  useQuery({
    queryKey: EVENTS_QUERY_KEYS.scores(eventId),
    queryFn: () => eventsApi.getScores(eventId),
    enabled: !!eventId,
    refetchInterval: live ? 5000 : false,
  })
