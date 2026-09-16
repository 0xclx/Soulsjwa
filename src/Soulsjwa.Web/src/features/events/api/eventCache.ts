import type { QueryClient } from '@tanstack/react-query'
import { EVENTS_QUERY_KEYS } from './eventsApi'

/**
 * Named, intentional invalidation scopes for an event-scoped mutation.
 * Picking the narrowest scope that actually changed keeps a single
 * checkbox toggle from refetching the audit log, overlay-token list, or
 * event list — sibling sub-resources that used to be invalidated as a
 * side effect of `EVENTS_QUERY_KEYS.detail(id)` prefix-matching them.
 *
 * - `'progress'` — objective completion/failure changed: scoreboard + scores.
 * - `'structure'` — games/objectives/competitors changed: detail + scoreboard + scores.
 * - `'listing'` — event created/archived/featured/renamed/started/stopped:
 *   detail + the event list + the featured event.
 */
export type EventCacheScope = 'progress' | 'structure' | 'listing'

/**
 * Invalidates exactly the event-scoped queries a given kind of change can
 * affect, by explicit key — never by relying on TanStack Query's
 * prefix-matching invalidation as a side effect.
 */
export const invalidateEventScope = (
  queryClient: QueryClient,
  eventId: string,
  scope: EventCacheScope,
): void => {
  if (scope === 'progress' || scope === 'structure') {
    queryClient.invalidateQueries({ queryKey: EVENTS_QUERY_KEYS.scoreboard(eventId) })
    queryClient.invalidateQueries({ queryKey: EVENTS_QUERY_KEYS.scores(eventId) })
  }
  if (scope === 'structure' || scope === 'listing') {
    queryClient.invalidateQueries({ queryKey: EVENTS_QUERY_KEYS.detail(eventId) })
  }
  if (scope === 'listing') {
    queryClient.invalidateQueries({ queryKey: EVENTS_QUERY_KEYS.listRoot })
    queryClient.invalidateQueries({ queryKey: EVENTS_QUERY_KEYS.featured })
  }
}
