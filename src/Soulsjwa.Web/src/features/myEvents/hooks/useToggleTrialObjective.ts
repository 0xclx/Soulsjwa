import { useMutation, useQueryClient } from '@tanstack/react-query'
import type { MyEventObjectivesResponse, MyTrialRun } from '../../../types'
import { eventsApi } from '../../events/api/eventsApi'
import { invalidateTrialProgress } from '../../events/api/trialRunCache'
import { MY_TRIAL_RUNS_QUERY_KEYS } from '../api/myTrialRunsApi'

interface ToggleTrialObjectiveParams {
  objectiveId: string
  completed: boolean
}

/**
 * Ticks an objective inside a trial run. There is no trial-specific write
 * endpoint: the ordinary completion endpoint attributes the row to whichever
 * run is active for that competitor and game, which is why this is only safe
 * to call while the run is actually recording.
 */
export const useToggleTrialObjective = (trialRun: MyTrialRun) => {
  const queryClient = useQueryClient()
  const objectivesKey = MY_TRIAL_RUNS_QUERY_KEYS.objectives(trialRun.trialRunId)
  const onBehalfOf = trialRun.isOwnTrial ? undefined : trialRun.competitorId

  return useMutation({
    mutationFn: ({ objectiveId, completed }: ToggleTrialObjectiveParams) =>
      completed
        ? eventsApi.uncompleteObjective(
            trialRun.eventId,
            trialRun.eventGameId,
            objectiveId,
            onBehalfOf,
            trialRun.trialRunId,
          )
        : eventsApi.completeObjective(
            trialRun.eventId,
            trialRun.eventGameId,
            objectiveId,
            onBehalfOf,
            trialRun.trialRunId,
          ),
    onMutate: async ({ objectiveId, completed }) => {
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
                        completed: !completed,
                        completedAt: completed ? null : new Date().toISOString(),
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
