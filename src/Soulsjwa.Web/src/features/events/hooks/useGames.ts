import { useQuery } from '@tanstack/react-query'
import { eventsApi, EVENTS_QUERY_KEYS } from '../api/eventsApi'

export const useGames = () =>
  useQuery({
    queryKey: EVENTS_QUERY_KEYS.games,
    queryFn: eventsApi.listGames,
  })
