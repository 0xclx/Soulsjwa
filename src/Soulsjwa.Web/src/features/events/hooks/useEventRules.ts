import { useQuery } from '@tanstack/react-query'
import { eventRulesApi, EVENT_RULES_QUERY_KEYS } from '../api/eventRulesApi'

export const useEventRules = (eventId: string) =>
  useQuery({
    queryKey: EVENT_RULES_QUERY_KEYS.detail(eventId),
    queryFn: () => eventRulesApi.get(eventId),
    enabled: !!eventId,
  })
