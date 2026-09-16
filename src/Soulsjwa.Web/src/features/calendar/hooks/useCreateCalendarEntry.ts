import { useMutation, useQueryClient } from '@tanstack/react-query'
import { calendarEntriesApi, CALENDAR_ENTRIES_QUERY_KEYS } from '../api/calendarEntriesApi'
import type { UpsertCalendarEntryRequest } from '../../../types/calendar'
import { CALENDAR_QUERY_KEYS } from '../api/calendarApi'

export const useCreateCalendarEntry = (eventId: string) => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (request: UpsertCalendarEntryRequest) =>
      calendarEntriesApi.create(eventId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: CALENDAR_ENTRIES_QUERY_KEYS.list(eventId) })
      queryClient.invalidateQueries({ queryKey: CALENDAR_QUERY_KEYS.globalPrefix })
    },
  })
}
