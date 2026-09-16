import { useMutation, useQueryClient } from '@tanstack/react-query'
import { calendarEntriesApi, CALENDAR_ENTRIES_QUERY_KEYS } from '../api/calendarEntriesApi'
import { CALENDAR_QUERY_KEYS } from '../api/calendarApi'

export const useDeleteCalendarEntry = (eventId: string) => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (entryId: string) => calendarEntriesApi.remove(eventId, entryId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: CALENDAR_ENTRIES_QUERY_KEYS.list(eventId) })
      queryClient.invalidateQueries({ queryKey: CALENDAR_QUERY_KEYS.globalPrefix })
    },
  })
}
