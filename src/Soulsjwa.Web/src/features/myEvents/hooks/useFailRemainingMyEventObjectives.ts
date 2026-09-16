import { useMutation, useQueryClient } from '@tanstack/react-query'
import { eventsApi } from '../../events/api/eventsApi'
import { invalidateEventScope } from '../../events/api/eventCache'
import { MY_EVENTS_QUERY_KEYS } from '../api/myEventsApi'

/**
 * Fails every still-pending objective of one game on the regular My Events
 * tabs, for the viewer or the delegated competitor. No optimistic update: the
 * server decides which objectives were still pending, so the list refetches.
 * Invalidates the same three scopes as
 * {@link import('./useToggleMyEventObjectiveFailure').useToggleMyEventObjectiveFailure}.
 */
export const useFailRemainingMyEventObjectives = (eventId: string, competitorId?: string) => {
  const queryClient = useQueryClient()
  const objectivesKey = MY_EVENTS_QUERY_KEYS.objectives(eventId, competitorId)

  return useMutation({
    mutationFn: ({ eventGameId }: { eventGameId: string }) =>
      eventsApi.failRemainingObjectives(eventId, eventGameId, competitorId),
    onSettled: () => {
      queryClient.invalidateQueries({ queryKey: objectivesKey })
      queryClient.invalidateQueries({ queryKey: MY_EVENTS_QUERY_KEYS.all })
      invalidateEventScope(queryClient, eventId, 'progress')
    },
  })
}
