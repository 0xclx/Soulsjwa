import { useMutation, useQueryClient } from '@tanstack/react-query'
import { eventsApi, EVENTS_QUERY_KEYS } from '../api/eventsApi'
import { invalidateEventScope } from '../api/eventCache'

export const useRemoveCompetitorInfo = (eventId: string, eventGameId: string, userId: string) => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (infoId: string) =>
      eventsApi.removeCompetitorInfo(eventId, eventGameId, userId, infoId),
    onSuccess: () => {
      queryClient.invalidateQueries({
        queryKey: EVENTS_QUERY_KEYS.competitorInfos(eventId, eventGameId, userId),
      })
      invalidateEventScope(queryClient, eventId, 'structure')
    },
  })
}
