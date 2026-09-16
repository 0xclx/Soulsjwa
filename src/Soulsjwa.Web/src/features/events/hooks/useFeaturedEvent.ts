import { useQuery } from '@tanstack/react-query'
import { eventsApi, EVENTS_QUERY_KEYS } from '../api/eventsApi'

export const useFeaturedEvent = () =>
  useQuery({
    queryKey: EVENTS_QUERY_KEYS.featured,
    queryFn: () => eventsApi.getFeatured(),
  })
