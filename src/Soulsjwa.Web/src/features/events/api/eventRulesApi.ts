import { apiClient } from '../../../lib/axios'
import type { EventRules } from '../../../types'

export const EVENT_RULES_QUERY_KEYS = {
  detail: (eventId: string) => ['events', eventId, 'rules'] as const,
}

export const eventRulesApi = {
  get: async (eventId: string): Promise<EventRules> => {
    const { data } = await apiClient.get<EventRules>(`/events/${eventId}/rules`)
    return data
  },

  update: async (eventId: string, content: string | null): Promise<EventRules> => {
    const { data } = await apiClient.put<EventRules>(`/events/${eventId}/rules`, { content })
    return data
  },
}
