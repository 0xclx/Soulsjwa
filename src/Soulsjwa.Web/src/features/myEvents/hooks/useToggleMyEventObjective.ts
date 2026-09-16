import { useMutation, useQueryClient } from '@tanstack/react-query'
import type { MyEventObjectivesResponse } from '../../../types'
import { eventsApi } from '../../events/api/eventsApi'
import { invalidateEventScope } from '../../events/api/eventCache'
import { MY_EVENTS_QUERY_KEYS } from '../api/myEventsApi'

interface ToggleMyEventObjectiveParams {
  eventGameId: string
  objectiveId: string
  completed: boolean
}

export const useToggleMyEventObjective = (eventId: string, competitorId?: string) => {
  const queryClient = useQueryClient()
  const objectivesKey = MY_EVENTS_QUERY_KEYS.objectives(eventId, competitorId)

  return useMutation({
    mutationFn: ({ eventGameId, objectiveId, completed }: ToggleMyEventObjectiveParams) =>
      completed
        ? eventsApi.uncompleteObjective(eventId, eventGameId, objectiveId, competitorId)
        : eventsApi.completeObjective(eventId, eventGameId, objectiveId, competitorId),
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
    onSettled: () => {
      queryClient.invalidateQueries({ queryKey: objectivesKey })
      queryClient.invalidateQueries({ queryKey: MY_EVENTS_QUERY_KEYS.all })
      invalidateEventScope(queryClient, eventId, 'progress')
    },
  })
}
