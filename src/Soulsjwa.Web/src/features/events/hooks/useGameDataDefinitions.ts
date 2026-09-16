import { useQuery } from '@tanstack/react-query'
import { eventsApi, EVENTS_QUERY_KEYS } from '../api/eventsApi'

export const useGameDataDefinitions = (gameId: number | undefined) =>
  useQuery({
    queryKey: EVENTS_QUERY_KEYS.gameDataDefinitions(gameId!),
    queryFn: () => eventsApi.getGameDataDefinitions(gameId!),
    enabled: !!gameId,
  })
