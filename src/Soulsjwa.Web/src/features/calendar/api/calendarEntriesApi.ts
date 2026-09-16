import { apiClient } from '../../../lib/axios'
import type { CalendarEntry, UpsertCalendarEntryRequest } from '../../../types/calendar'

export const CALENDAR_ENTRIES_QUERY_KEYS = {
  list: (eventId: string) => ['events', eventId, 'calendar-entries'] as const,
}

export const calendarEntriesApi = {
  list: async (eventId: string): Promise<CalendarEntry[]> => {
    const { data } = await apiClient.get<CalendarEntry[]>(`/events/${eventId}/calendar-entries`)
    return data
  },

  create: async (eventId: string, request: UpsertCalendarEntryRequest): Promise<CalendarEntry> => {
    const { data } = await apiClient.post<CalendarEntry>(
      `/events/${eventId}/calendar-entries`,
      request,
    )
    return data
  },

  update: async (
    eventId: string,
    entryId: string,
    request: UpsertCalendarEntryRequest,
    version?: number,
  ): Promise<CalendarEntry> => {
    const { data } = await apiClient.put<CalendarEntry>(
      `/events/${eventId}/calendar-entries/${entryId}`,
      { ...request, version },
    )
    return data
  },

  remove: async (eventId: string, entryId: string): Promise<void> => {
    await apiClient.delete(`/events/${eventId}/calendar-entries/${entryId}`)
  },
}
