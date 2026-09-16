import { apiClient } from '../../../lib/axios'
import type { MyEventObjectivesResponse, MyEventsResponse } from '../../../types'

export const MY_EVENTS_QUERY_KEYS = {
  all: ['my-events'] as const,
  objectives: (eventId: string, competitorId?: string) =>
    ['my-events', eventId, 'objectives', competitorId ?? 'self'] as const,
}

export const myEventsApi = {
  list: async (): Promise<MyEventsResponse> => {
    const { data } = await apiClient.get<MyEventsResponse>('/me/events')
    return data
  },

  getObjectives: async (
    eventId: string,
    competitorId?: string,
  ): Promise<MyEventObjectivesResponse> => {
    const { data } = await apiClient.get<MyEventObjectivesResponse>(
      `/me/events/${eventId}/objectives`,
      { params: competitorId ? { competitorId } : undefined },
    )
    return data
  },
}
