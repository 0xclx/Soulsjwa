import { apiClient } from '../../../lib/axios'
import type { TrialRun } from '../../../types'

export const TRIAL_RUN_QUERY_KEYS = {
  detail: (eventId: string, eventGameId: string, userId: string) =>
    ['trialRuns', eventId, eventGameId, userId] as const,
}

const basePath = (eventId: string, eventGameId: string, userId: string) =>
  `/events/${eventId}/games/${eventGameId}/trial-runs/${userId}`

export const trialRunsApi = {
  get: async (eventId: string, eventGameId: string, userId: string): Promise<TrialRun | null> => {
    try {
      const { data } = await apiClient.get<TrialRun>(basePath(eventId, eventGameId, userId))
      return data
    } catch (err) {
      const status = (err as { response?: { status?: number } })?.response?.status
      if (status === 404) return null
      throw err
    }
  },

  enable: async (eventId: string, eventGameId: string, userId: string): Promise<TrialRun> => {
    const { data } = await apiClient.post<TrialRun>(basePath(eventId, eventGameId, userId))
    return data
  },

  disable: async (eventId: string, eventGameId: string, userId: string): Promise<void> => {
    await apiClient.delete(basePath(eventId, eventGameId, userId))
  },

  start: async (eventId: string, eventGameId: string, userId: string): Promise<TrialRun> => {
    const { data } = await apiClient.post<TrialRun>(
      `${basePath(eventId, eventGameId, userId)}/start`,
    )
    return data
  },

  stop: async (eventId: string, eventGameId: string, userId: string): Promise<TrialRun> => {
    const { data } = await apiClient.post<TrialRun>(
      `${basePath(eventId, eventGameId, userId)}/stop`,
    )
    return data
  },

  reset: async (eventId: string, eventGameId: string, userId: string): Promise<TrialRun> => {
    const { data } = await apiClient.post<TrialRun>(
      `${basePath(eventId, eventGameId, userId)}/reset`,
    )
    return data
  },
}
