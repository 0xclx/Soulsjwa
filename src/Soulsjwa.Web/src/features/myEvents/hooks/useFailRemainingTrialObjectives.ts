import { useMutation, useQueryClient } from '@tanstack/react-query'
import type { MyTrialRun } from '../../../types'
import { eventsApi } from '../../events/api/eventsApi'
import { invalidateTrialProgress } from '../../events/api/trialRunCache'

/**
 * Fails every still-pending objective of a trial run's game on that run.
 * Asserts the run id like the single-objective trial hooks do, so a run that
 * stopped between a poll and the click is refused rather than written to the
 * official record.
 */
export const useFailRemainingTrialObjectives = (trialRun: MyTrialRun) => {
  const queryClient = useQueryClient()
  const onBehalfOf = trialRun.isOwnTrial ? undefined : trialRun.competitorId

  return useMutation({
    mutationFn: () =>
      eventsApi.failRemainingObjectives(
        trialRun.eventId,
        trialRun.eventGameId,
        onBehalfOf,
        trialRun.trialRunId,
      ),
    onSettled: () => invalidateTrialProgress(queryClient, trialRun.eventId),
  })
}
