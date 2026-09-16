import { useQuery } from '@tanstack/react-query'
import { trialRunsApi, TRIAL_RUN_QUERY_KEYS } from '../api/trialRunsApi'

export const useTrialRun = (eventId: string, eventGameId: string, userId: string) =>
  useQuery({
    queryKey: TRIAL_RUN_QUERY_KEYS.detail(eventId, eventGameId, userId),
    queryFn: () => trialRunsApi.get(eventId, eventGameId, userId),
  })
