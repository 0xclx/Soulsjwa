import { useMutation, useQueryClient } from '@tanstack/react-query'
import { eventRulesApi, EVENT_RULES_QUERY_KEYS } from '../api/eventRulesApi'

export const useUpdateEventRules = (eventId: string) => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (content: string | null) => eventRulesApi.update(eventId, content),
    onSuccess: (data) => {
      queryClient.setQueryData(EVENT_RULES_QUERY_KEYS.detail(eventId), data)
    },
  })
}
