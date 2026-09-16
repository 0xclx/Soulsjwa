import { useMutation, useQueryClient } from '@tanstack/react-query'
import { eventsApi } from '../api/eventsApi'
import { invalidateEventScope } from '../api/eventCache'

export const useCreateObjective = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (params: {
      eventId: string
      eventGameId: string
      payload: { name: string; score: number; category?: string; rule?: string; failRule?: string }
    }) => eventsApi.createObjective(params.eventId, params.eventGameId, params.payload),
    onSuccess: (_data, { eventId }) => invalidateEventScope(queryClient, eventId, 'listing'),
  })
}
