import { useMutation, useQueryClient } from '@tanstack/react-query'
import { eventsApi } from '../api/eventsApi'
import { invalidateEventScope } from '../api/eventCache'

export const useDuplicateEvent = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: eventsApi.duplicate,
    onSuccess: (data) => invalidateEventScope(queryClient, data.id, 'listing'),
  })
}
