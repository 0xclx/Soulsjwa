import { useMutation, useQueryClient } from '@tanstack/react-query'
import { eventsApi, EVENTS_QUERY_KEYS } from '../api/eventsApi'
import { invalidateEventScope } from '../api/eventCache'
import type { CompetitorInfoType } from '../../../types'

export const useUpdateCompetitorInfo = (eventId: string, eventGameId: string, userId: string) => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({
      infoId,
      ...payload
    }: {
      infoId: string
      type?: CompetitorInfoType
      url?: string | null
      text?: string | null
    }) => eventsApi.updateCompetitorInfo(eventId, eventGameId, userId, infoId, payload),
    onSuccess: () => {
      queryClient.invalidateQueries({
        queryKey: EVENTS_QUERY_KEYS.competitorInfos(eventId, eventGameId, userId),
      })
      invalidateEventScope(queryClient, eventId, 'structure')
    },
  })
}
