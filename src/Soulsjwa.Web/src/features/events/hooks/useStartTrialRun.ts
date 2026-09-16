import { useMutation, useQueryClient } from '@tanstack/react-query'
import { trialRunsApi, TRIAL_RUN_QUERY_KEYS } from '../api/trialRunsApi'
import { invalidateTrialState } from '../api/trialRunCache'

export const useStartTrialRun = (eventId: string, eventGameId: string, userId: string) => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => trialRunsApi.start(eventId, eventGameId, userId),
    onSuccess: (data) => {
      queryClient.setQueryData(TRIAL_RUN_QUERY_KEYS.detail(eventId, eventGameId, userId), data)
      invalidateTrialState(queryClient, eventId)
    },
  })
}
