import { useMutation, useQueryClient } from '@tanstack/react-query'
import { eventsApi } from '../api/eventsApi'
import { invalidateEventScope } from '../api/eventCache'

export const useImportPredefinedObjectives = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (params: { eventId: string; eventGameId: string; objectiveIds?: string[] }) =>
      eventsApi.importPredefinedObjectives(params.eventId, params.eventGameId, params.objectiveIds),
    onSuccess: (_data, { eventId }) => invalidateEventScope(queryClient, eventId, 'listing'),
  })
}
