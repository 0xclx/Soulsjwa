import { describe, expect, it, vi, beforeEach, afterEach } from 'vitest'
import { act, renderHook, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import type { ReactNode } from 'react'
import { useEvent } from './useEvent'
import { eventsApi, EVENTS_QUERY_KEYS } from '../api/eventsApi'
import { SCOREBOARD_REFRESH_INTERVAL_MS } from '../scoreboard/scoreboardPolling'
import type { EventResponse } from '../../../types'

vi.mock('../api/eventsApi', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../api/eventsApi')>()
  return { ...actual, eventsApi: { ...actual.eventsApi, get: vi.fn() } }
})

const EVENT_ID = '0d9e8f4a-1b2c-43d4-95e6-7f8091a2b3c4'
const ALIAS = 'lordran-relay'

const makeEvent = (overrides: Partial<EventResponse> = {}): EventResponse => ({
  id: EVENT_ID,
  name: 'Lordran Relay',
  urlAlias: ALIAS,
  description: '',
  createdById: 'user-1',
  isArchived: false,
  isStarted: false,
  isFeatured: false,
  allowTrialRuns: false,
  tieBreakMode: 'ByTime',
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
  competitors: [],
  games: [],
  ...overrides,
})

const wrapper = ({ children }: { children: ReactNode }) => {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return <QueryClientProvider client={client}>{children}</QueryClientProvider>
}

describe('useEvent', () => {
  beforeEach(() => {
    vi.mocked(eventsApi.get).mockReset()
  })

  it('issues exactly one request for an alias identifier', async () => {
    vi.mocked(eventsApi.get).mockResolvedValue(makeEvent())

    const { result } = renderHook(() => useEvent(ALIAS), { wrapper })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))

    expect(eventsApi.get).toHaveBeenCalledTimes(1)
    expect(eventsApi.get).toHaveBeenCalledWith(ALIAS)
  })

  it('issues exactly one request for a guid identifier', async () => {
    vi.mocked(eventsApi.get).mockResolvedValue(makeEvent())

    const { result } = renderHook(() => useEvent(EVENT_ID), { wrapper })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))

    expect(eventsApi.get).toHaveBeenCalledTimes(1)
    expect(eventsApi.get).toHaveBeenCalledWith(EVENT_ID.toLowerCase())
  })

  it('never transitions isLoading back to true for the same identifier', async () => {
    vi.mocked(eventsApi.get).mockResolvedValue(makeEvent())
    const loadingStates: boolean[] = []

    const { result, rerender } = renderHook(() => useEvent(ALIAS), { wrapper })
    loadingStates.push(result.current.isLoading)

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    loadingStates.push(result.current.isLoading)

    rerender()
    loadingStates.push(result.current.isLoading)

    expect(loadingStates).toEqual([true, false, false])
  })

  it('seeds the canonical id cache entry so navigating from alias to id is an instant hit', async () => {
    vi.mocked(eventsApi.get).mockResolvedValue(makeEvent())
    // Mirror the app's real QueryClient defaults (lib/react-query/queryClient.ts):
    // a 5-minute staleTime is what makes the seeded entry an instant, no-refetch
    // hit — a bare `new QueryClient()` defaults to staleTime 0, which would
    // background-refetch on mount regardless of the seed.
    const client = new QueryClient({
      defaultOptions: { queries: { retry: false, staleTime: 1000 * 60 * 5 } },
    })
    const wrapWithClient = ({ children }: { children: ReactNode }) => (
      <QueryClientProvider client={client}>{children}</QueryClientProvider>
    )

    const { result: byAlias } = renderHook(() => useEvent(ALIAS), { wrapper: wrapWithClient })
    await waitFor(() => expect(byAlias.current.isSuccess).toBe(true))

    expect(client.getQueryData(EVENTS_QUERY_KEYS.detail(EVENT_ID))).toEqual(makeEvent())

    vi.mocked(eventsApi.get).mockClear()
    const { result: byId } = renderHook(() => useEvent(EVENT_ID), { wrapper: wrapWithClient })

    // Resolves from cache — no network call needed.
    expect(byId.current.data).toEqual(makeEvent())
    expect(eventsApi.get).not.toHaveBeenCalled()
  })

  describe('polling', () => {
    beforeEach(() => {
      vi.useFakeTimers()
    })

    afterEach(() => {
      vi.useRealTimers()
    })

    const advance = async (ms: number) => {
      await act(async () => {
        await vi.advanceTimersByTimeAsync(ms)
      })
    }

    it('polls a started event at the scoreboard interval, not before', async () => {
      vi.mocked(eventsApi.get).mockResolvedValue(makeEvent({ isStarted: true }))
      renderHook(() => useEvent(EVENT_ID), { wrapper })
      await advance(0)
      expect(eventsApi.get).toHaveBeenCalledTimes(1)

      await advance(SCOREBOARD_REFRESH_INTERVAL_MS - 1)
      expect(eventsApi.get).toHaveBeenCalledTimes(1)

      await advance(1)
      expect(eventsApi.get).toHaveBeenCalledTimes(2)
    })

    it('does not poll an unstarted event', async () => {
      vi.mocked(eventsApi.get).mockResolvedValue(makeEvent({ isStarted: false }))
      renderHook(() => useEvent(EVENT_ID), { wrapper })
      await advance(SCOREBOARD_REFRESH_INTERVAL_MS * 2)

      expect(eventsApi.get).toHaveBeenCalledTimes(1)
    })
  })
})
