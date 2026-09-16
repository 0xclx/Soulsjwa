import { useMutation, useQueryClient } from '@tanstack/react-query'
import { eventsApi, EVENTS_QUERY_KEYS } from '../api/eventsApi'
import { invalidateEventScope } from '../api/eventCache'
import type { CompetitorInfoType } from '../../../types'

export const useAddCompetitorInfo = (eventId: string, eventGameId: string, userId: string) => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: { type: CompetitorInfoType; url?: string; text?: string }) =>
      eventsApi.addCompetitorInfo(eventId, eventGameId, userId, payload),
    onSuccess: () => {
      queryClient.invalidateQueries({
        queryKey: EVENTS_QUERY_KEYS.competitorInfos(eventId, eventGameId, userId),
      })
      invalidateEventScope(queryClient, eventId, 'structure')
    },
  })
}
