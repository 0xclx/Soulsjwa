import { useInfiniteQuery } from '@tanstack/react-query'
import { eventsApi, EVENTS_QUERY_KEYS } from '../api/eventsApi'
import type { EventAuditsParams } from '../api/eventsApi'

/** Rows fetched per page; not user-adjustable — cursor pages have no page-size UI. */
const PAGE_SIZE = 20

export type EventAuditsFilters = Omit<EventAuditsParams, 'page' | 'pageSize' | 'cursor'>

/**
 * Keyset/cursor pagination: each page carries the next page's cursor instead
 * of a page number, avoiding the OFFSET + COUNT this endpoint used to do on
 * every request. `fetchNextPage`/`hasNextPage` drive a "Load more"
 * affordance instead of numbered pages.
 */
export const useEventAudits = (eventId: string, filters: EventAuditsFilters, enabled = true) => {
  const stableTypes = [...(filters.types ?? [])].sort((a, b) => a.localeCompare(b))
  const stableFilters = { ...filters, types: stableTypes }

  return useInfiniteQuery({
    queryKey: EVENTS_QUERY_KEYS.audits(eventId, stableFilters),
    queryFn: ({ pageParam }: { pageParam: string | undefined }) =>
      eventsApi.listEventAudits(eventId, {
        ...stableFilters,
        pageSize: PAGE_SIZE,
        cursor: pageParam,
      }),
    initialPageParam: undefined as string | undefined,
    getNextPageParam: (lastPage) => lastPage.nextCursor ?? undefined,
    enabled: !!eventId && enabled,
  })
}
