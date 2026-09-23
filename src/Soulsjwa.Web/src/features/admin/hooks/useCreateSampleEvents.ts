import { useMutation, useQueryClient } from '@tanstack/react-query'
import { adminApi } from '../api/adminApi'
import { EVENTS_QUERY_KEYS } from '../../events/api/eventsApi'

export const useCreateSampleEvents = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => adminApi.createSampleEvents(),
    // The new events belong in every events list page.
    onSuccess: () => queryClient.invalidateQueries({ queryKey: EVENTS_QUERY_KEYS.listRoot }),
  })
}
