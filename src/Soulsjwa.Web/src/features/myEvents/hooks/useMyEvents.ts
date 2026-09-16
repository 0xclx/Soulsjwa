import { useQuery } from '@tanstack/react-query'
import { myEventsApi, MY_EVENTS_QUERY_KEYS } from '../api/myEventsApi'

const MY_EVENTS_REFRESH_INTERVAL_MS = 5000

export const useMyEvents = () =>
  useQuery({
    queryKey: MY_EVENTS_QUERY_KEYS.all,
    queryFn: myEventsApi.list,
    staleTime: MY_EVENTS_REFRESH_INTERVAL_MS,
    refetchInterval: MY_EVENTS_REFRESH_INTERVAL_MS,
    refetchIntervalInBackground: true,
  })
