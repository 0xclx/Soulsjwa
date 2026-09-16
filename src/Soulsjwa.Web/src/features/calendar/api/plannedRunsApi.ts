import { apiClient } from '../../../lib/axios'
import type {
  CreatePlannedRunRequest,
  PlannedRun,
  UpdatePlannedRunRequest,
} from '../../../types/calendar'

export const PLANNED_RUNS_QUERY_KEYS = {
  list: (eventId: string, userId: string) =>
    ['events', eventId, 'competitors', userId, 'planned-runs'] as const,
}

export const plannedRunsApi = {
  list: async (eventId: string, userId: string): Promise<PlannedRun[]> => {
    const { data } = await apiClient.get<PlannedRun[]>(
      `/events/${eventId}/competitors/${userId}/planned-runs`,
    )
    return data
  },

  create: async (
    eventId: string,
    userId: string,
    request: CreatePlannedRunRequest,
  ): Promise<PlannedRun> => {
    const { data } = await apiClient.post<PlannedRun>(
      `/events/${eventId}/competitors/${userId}/planned-runs`,
      request,
    )
    return data
  },

  update: async (
    eventId: string,
    userId: string,
    plannedRunId: string,
    request: UpdatePlannedRunRequest,
  ): Promise<PlannedRun> => {
    const { data } = await apiClient.put<PlannedRun>(
      `/events/${eventId}/competitors/${userId}/planned-runs/${plannedRunId}`,
      request,
    )
    return data
  },

  remove: async (eventId: string, userId: string, plannedRunId: string): Promise<void> => {
    await apiClient.delete(`/events/${eventId}/competitors/${userId}/planned-runs/${plannedRunId}`)
  },
}
