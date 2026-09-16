import { apiClient } from '../../../lib/axios'
import type { GlobalCalendarResponse } from '../../../types/calendar'

export const CALENDAR_QUERY_KEYS = {
  /** Prefix shared by every from/to variant — invalidate with this to refresh all of them. */
  globalPrefix: ['calendar', 'global'] as const,
  global: (from?: string, to?: string) => ['calendar', 'global', from ?? null, to ?? null] as const,
}

export const calendarApi = {
  /** `from`/`to` are ISO-8601 strings; omit either to use the server's default window. */
  getGlobal: async (from?: string, to?: string): Promise<GlobalCalendarResponse> => {
    const { data } = await apiClient.get<GlobalCalendarResponse>('/calendar', {
      params: { from, to },
    })
    return data
  },
}
