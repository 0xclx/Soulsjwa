import { useInfiniteQuery } from '@tanstack/react-query'
import { adminApi, ADMIN_QUERY_KEYS } from '../api/adminApi'
import type { AdminAuditsParams } from '../api/adminApi'

/** Rows fetched per page; not user-adjustable — cursor pages have no page-size UI. */
const PAGE_SIZE = 20

export type AdminAuditsFilters = Omit<AdminAuditsParams, 'page' | 'pageSize' | 'cursor'>

/**
 * Keyset/cursor pagination: each page carries the next page's cursor instead
 * of a page number, avoiding the OFFSET + COUNT this endpoint used to do on
 * every request. `fetchNextPage`/`hasNextPage` drive a "Load more"
 * affordance instead of numbered pages.
 */
export const useAdminAudits = (filters: AdminAuditsFilters, enabled = true) => {
  const stableTypes = [...(filters.types ?? [])].sort((a, b) => a.localeCompare(b))
  const stableFilters = { ...filters, types: stableTypes }

  return useInfiniteQuery({
    queryKey: ADMIN_QUERY_KEYS.audits(stableFilters),
    queryFn: ({ pageParam }: { pageParam: string | undefined }) =>
      adminApi.listAdminAudits({ ...stableFilters, pageSize: PAGE_SIZE, cursor: pageParam }),
    initialPageParam: undefined as string | undefined,
    getNextPageParam: (lastPage) => lastPage.nextCursor ?? undefined,
    enabled,
  })
}
