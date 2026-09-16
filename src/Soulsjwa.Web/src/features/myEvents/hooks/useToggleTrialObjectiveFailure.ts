import { useMutation, useQueryClient } from '@tanstack/react-query'
import type { MyEventObjectivesResponse, MyTrialRun } from '../../../types'
import { eventsApi } from '../../events/api/eventsApi'
import { invalidateTrialProgress } from '../../events/api/trialRunCache'
import { MY_TRIAL_RUNS_QUERY_KEYS } from '../api/myTrialRunsApi'

interface ToggleTrialObjectiveFailureParams {
  objectiveId: string
  failed: boolean
}

/** Mirrors {@link import('./useToggleTrialObjective').useToggleTrialObjective} for fail/reset. */
export const useToggleTrialObjectiveFailure = (trialRun: MyTrialRun) => {
  const queryClient = useQueryClient()
  const objectivesKey = MY_TRIAL_RUNS_QUERY_KEYS.objectives(trialRun.trialRunId)
  const onBehalfOf = trialRun.isOwnTrial ? undefined : trialRun.competitorId

  return useMutation({
    mutationFn: ({ objectiveId, failed }: ToggleTrialObjectiveFailureParams) =>
      failed
        ? eventsApi.resetFailedObjective(
            trialRun.eventId,
            trialRun.eventGameId,
            objectiveId,
            onBehalfOf,
            trialRun.trialRunId,
          )
        : eventsApi.failObjective(
            trialRun.eventId,
            trialRun.eventGameId,
            objectiveId,
            onBehalfOf,
            trialRun.trialRunId,
          ),
    onMutate: async ({ objectiveId, failed }) => {
      await queryClient.cancelQueries({ queryKey: objectivesKey })
      const previous = queryClient.getQueryData<MyEventObjectivesResponse>(objectivesKey)
      queryClient.setQueryData<MyEventObjectivesResponse>(objectivesKey, (current) =>
        current
          ? {
              ...current,
              games: current.games.map((game) => ({
                ...game,
                objectives: game.objectives.map((objective) =>
                  objective.objectiveId === objectiveId
                    ? {
                        ...objective,
                        failed: !failed,
                        failedAt: failed ? null : new Date().toISOString(),
                      }
                    : objective,
                ),
              })),
            }
          : current,
      )
      return { previous }
    },
    onError: (_error, _variables, context) => {
      if (context?.previous) queryClient.setQueryData(objectivesKey, context.previous)
    },
    onSettled: () => invalidateTrialProgress(queryClient, trialRun.eventId),
  })
}
