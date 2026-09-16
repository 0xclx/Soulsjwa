import { apiClient } from '../../../lib/axios'
import type { MyEventObjectivesResponse, MyTrialRun } from '../../../types'

export const MY_TRIAL_RUNS_QUERY_KEYS = {
  all: ['my-trial-runs'] as const,
  objectives: (trialRunId: string) => ['my-trial-runs', trialRunId, 'objectives'] as const,
}

export const myTrialRunsApi = {
  list: async (): Promise<MyTrialRun[]> => {
    const { data } = await apiClient.get<MyTrialRun[]>('/me/trial-runs')
    return data
  },

  getObjectives: async (trialRunId: string): Promise<MyEventObjectivesResponse> => {
    const { data } = await apiClient.get<MyEventObjectivesResponse>(
      `/me/trial-runs/${trialRunId}/objectives`,
    )
    return data
  },
}
