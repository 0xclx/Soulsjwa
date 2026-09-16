import { useQuery } from '@tanstack/react-query'
import { eventsApi, EVENTS_QUERY_KEYS } from '../api/eventsApi'

export const usePredefinedObjectives = (gameId: number, enabled = true) =>
  useQuery({
    queryKey: EVENTS_QUERY_KEYS.predefinedObjectives(gameId),
    queryFn: () => eventsApi.listPredefinedObjectives(gameId),
    enabled,
  })
