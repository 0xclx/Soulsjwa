import { useQuery, keepPreviousData } from '@tanstack/react-query'
import { eventsApi, EVENTS_QUERY_KEYS, type EventStatusFilter } from '../api/eventsApi'

export const useEvents = (
  page = 1,
  includeArchived = false,
  search = '',
  status: EventStatusFilter = 'all',
) =>
  useQuery({
    queryKey: EVENTS_QUERY_KEYS.list({ page, includeArchived, search, status }),
    queryFn: () => eventsApi.list({ page, pageSize: 20, includeArchived, search, status }),
    placeholderData: keepPreviousData,
  })
