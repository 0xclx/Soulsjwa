import { describe, it, expect, beforeEach, vi } from 'vitest'
import { renderHook, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import type { ReactNode } from 'react'
import { useEventAudits } from './useEventAudits'
import { eventsApi } from '../api/eventsApi'

vi.mock('../api/eventsApi', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../api/eventsApi')>()
  return { ...actual, eventsApi: { ...actual.eventsApi, listEventAudits: vi.fn() } }
})

const EVENT_ID = 'event-1'

const wrapper = ({ children }: { children: ReactNode }) => {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return <QueryClientProvider client={client}>{children}</QueryClientProvider>
}

describe('useEventAudits', () => {
  beforeEach(() => {
    vi.mocked(eventsApi.listEventAudits).mockReset().mockResolvedValue({
      items: [],
      page: 1,
      pageSize: 20,
      totalCount: 0,
      nextCursor: null,
      hasNextPage: false,
      hasPreviousPage: false,
    })
  })

  it('is order-independent: types in a different order hit the same cache entry, one request', async () => {
    const client = new QueryClient({
      defaultOptions: { queries: { retry: false, staleTime: 60_000 } },
    })
    const wrapWithClient = ({ children }: { children: ReactNode }) => (
      <QueryClientProvider client={client}>{children}</QueryClientProvider>
    )

    const { result: first } = renderHook(() => useEventAudits(EVENT_ID, { types: ['b', 'a'] }), {
      wrapper: wrapWithClient,
    })
    await waitFor(() => expect(first.current.isSuccess).toBe(true))

    vi.mocked(eventsApi.listEventAudits).mockClear()
    const { result: second } = renderHook(() => useEventAudits(EVENT_ID, { types: ['a', 'b'] }), {
      wrapper: wrapWithClient,
    })

    // Same cache entry — resolves instantly, no new request.
    expect(second.current.data).toBeDefined()
    expect(eventsApi.listEventAudits).not.toHaveBeenCalled()
  })

  it('renders without useMemo (no stale sort across renders)', async () => {
    const { result, rerender } = renderHook(({ types }) => useEventAudits(EVENT_ID, { types }), {
      wrapper,
      initialProps: { types: ['b', 'a'] },
    })
    await waitFor(() => expect(result.current.isSuccess).toBe(true))

    rerender({ types: ['c', 'a'] })
    await waitFor(() =>
      expect(vi.mocked(eventsApi.listEventAudits)).toHaveBeenLastCalledWith(EVENT_ID, {
        types: ['a', 'c'],
        pageSize: 20,
        cursor: undefined,
      }),
    )
  })
})
