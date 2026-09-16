import { useQuery, useQueryClient } from '@tanstack/react-query'
import { eventsApi, EVENTS_QUERY_KEYS } from '../api/eventsApi'

const EVENT_ID_PATTERN =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i

/**
 * Loads an event by id or URL alias with a single request — `GET
 * /events/{identifier}` already accepts either form and returns the
 * complete `EventResponse`, so there is no separate "resolve" round-trip.
 */
export const useEvent = (identifier: string) => {
  const queryClient = useQueryClient()
  // Two casings of the same GUID must share one cache entry; an alias is
  // case-sensitive and left as-is.
  const normalizedIdentifier = EVENT_ID_PATTERN.test(identifier)
    ? identifier.toLowerCase()
    : identifier

  return useQuery({
    queryKey: EVENTS_QUERY_KEYS.detail(normalizedIdentifier),
    queryFn: async () => {
      const data = await eventsApi.get(normalizedIdentifier)
      // Seed the canonical-id entry too, so navigating from the alias to the
      // id (or vice versa) is an instant cache hit instead of a second
      // round-trip for data we already have.
      if (normalizedIdentifier !== data.id) {
        queryClient.setQueryData(EVENTS_QUERY_KEYS.detail(data.id), data)
      }
      return data
    },
    enabled: !!normalizedIdentifier,
    refetchInterval: (query) => (query.state.data?.isStarted ? 5000 : false),
  })
}
