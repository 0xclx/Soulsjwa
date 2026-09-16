import { useMutation, useQueryClient } from '@tanstack/react-query'
import type { MyEventObjectivesResponse } from '../../../types'
import { eventsApi } from '../../events/api/eventsApi'
import { invalidateEventScope } from '../../events/api/eventCache'
import { MY_EVENTS_QUERY_KEYS } from '../api/myEventsApi'

interface ToggleMyEventObjectiveFailureParams {
  eventGameId: string
  objectiveId: string
  failed: boolean
}

/** Mirrors {@link import('./useToggleMyEventObjective').useToggleMyEventObjective} for manual fail/reset. */
export const useToggleMyEventObjectiveFailure = (eventId: string, competitorId?: string) => {
  const queryClient = useQueryClient()
  const objectivesKey = MY_EVENTS_QUERY_KEYS.objectives(eventId, competitorId)

  return useMutation({
    mutationFn: ({ eventGameId, objectiveId, failed }: ToggleMyEventObjectiveFailureParams) =>
      failed
        ? eventsApi.resetFailedObjective(eventId, eventGameId, objectiveId, competitorId)
        : eventsApi.failObjective(eventId, eventGameId, objectiveId, competitorId),
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
    onSettled: () => {
      queryClient.invalidateQueries({ queryKey: objectivesKey })
      queryClient.invalidateQueries({ queryKey: MY_EVENTS_QUERY_KEYS.all })
      invalidateEventScope(queryClient, eventId, 'progress')
    },
  })
}
