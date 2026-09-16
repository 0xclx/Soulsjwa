import { useMutation, useQueryClient } from '@tanstack/react-query'
import { eventsApi } from '../api/eventsApi'
import { invalidateEventScope } from '../api/eventCache'

export const useCreateEvent = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: eventsApi.create,
    onSuccess: (data) => invalidateEventScope(queryClient, data.id, 'listing'),
  })
}
