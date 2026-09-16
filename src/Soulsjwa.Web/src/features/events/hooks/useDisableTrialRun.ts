import { useMutation, useQueryClient } from '@tanstack/react-query'
import { trialRunsApi, TRIAL_RUN_QUERY_KEYS } from '../api/trialRunsApi'
import { invalidateTrialState } from '../api/trialRunCache'

export const useDisableTrialRun = (eventId: string, eventGameId: string, userId: string) => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => trialRunsApi.disable(eventId, eventGameId, userId),
    onSuccess: () => {
      queryClient.setQueryData(TRIAL_RUN_QUERY_KEYS.detail(eventId, eventGameId, userId), null)
      // The run and every completion under it are gone, so the trial list,
      // its objective ticks, and the scoreboard/overlay figures must all be
      // refetched rather than left showing a run that no longer exists.
      invalidateTrialState(queryClient, eventId)
    },
  })
}
