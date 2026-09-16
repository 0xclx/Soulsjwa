import { useMutation, useQueryClient } from '@tanstack/react-query'
import { eventsApi, EVENTS_QUERY_KEYS } from '../api/eventsApi'
import { invalidateEventScope } from '../api/eventCache'

export const useEditCompletionTime = (eventId: string) => {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (params: {
      eventGameId: string
      objectiveId: string
      userId: string
      body: { completedAt: string; reason: string }
    }) =>
      eventsApi.editCompletionTime(
        eventId,
        params.eventGameId,
        params.objectiveId,
        params.userId,
        params.body,
      ),
    onSuccess: () => {
      invalidateEventScope(queryClient, eventId, 'progress')
      queryClient.invalidateQueries({ queryKey: EVENTS_QUERY_KEYS.auditsRoot(eventId) })
    },
  })
}
