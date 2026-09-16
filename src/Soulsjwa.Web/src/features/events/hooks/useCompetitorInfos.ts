import { useQuery } from '@tanstack/react-query'
import { eventsApi, EVENTS_QUERY_KEYS } from '../api/eventsApi'

/**
 * Lists the additional metadata entries (death clips, links, plain text notes)
 * attached to a specific (event-game, competitor) pair.
 */
export const useCompetitorInfos = (
  eventId: string,
  eventGameId: string,
  userId: string,
  enabled = true,
) =>
  useQuery({
    queryKey: EVENTS_QUERY_KEYS.competitorInfos(eventId, eventGameId, userId),
    queryFn: () => eventsApi.listCompetitorInfos(eventId, eventGameId, userId),
    enabled: enabled && !!eventId && !!eventGameId && !!userId,
  })
