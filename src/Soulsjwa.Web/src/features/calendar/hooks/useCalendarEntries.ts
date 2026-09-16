import { useQuery } from '@tanstack/react-query'
import { calendarEntriesApi, CALENDAR_ENTRIES_QUERY_KEYS } from '../api/calendarEntriesApi'

export const useCalendarEntries = (eventId: string) =>
  useQuery({
    queryKey: CALENDAR_ENTRIES_QUERY_KEYS.list(eventId),
    queryFn: () => calendarEntriesApi.list(eventId),
    enabled: !!eventId,
  })
