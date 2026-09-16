import { useMutation, useQueryClient } from '@tanstack/react-query'
import { eventsApi } from '../api/eventsApi'
import { invalidateEventScope } from '../api/eventCache'

export const useUnarchiveEvent = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: eventsApi.unarchive,
    onSuccess: (_data, id) => invalidateEventScope(queryClient, id, 'listing'),
  })
}
