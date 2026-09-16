import { QueryClient } from '@tanstack/react-query'

/**
 * App-wide TanStack Query defaults; per-query overrides are fine at the call
 * site.
 *
 * - `refetchOnWindowFocus: false` — focus refetch causes a thundering herd
 *   when an admin alt-tabs between dashboards; we invalidate explicitly
 *   after mutations instead.
 * - `retry` — 4xx (401/403/404) is deterministic and never retried.
 * - mutations are not retried because most of them are not idempotent.
 */
export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 1000 * 60 * 5, // 5 minutes
      gcTime: 1000 * 60 * 15, // 15 minutes
      refetchOnWindowFocus: false,
      refetchOnReconnect: true,
      retry: (failureCount, error: unknown) => {
        const status = (error as { response?: { status?: number } })?.response?.status
        if (status === 401 || status === 403 || status === 404) return false
        return failureCount < 2
      },
    },
    mutations: {
      retry: false,
    },
  },
})
