import { act, renderHook } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import type { ReactNode } from 'react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { useScoreboard } from './useScoreboard'
import { eventsApi } from '../api/eventsApi'
import { SCOREBOARD_REFRESH_INTERVAL_MS } from '../scoreboard/scoreboardPolling'

vi.mock('../api/eventsApi', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../api/eventsApi')>()
  return { ...actual, eventsApi: { ...actual.eventsApi, getScoreboard: vi.fn() } }
})

const wrapper = ({ children }: { children: ReactNode }) => {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return <QueryClientProvider client={client}>{children}</QueryClientProvider>
}

const advance = async (ms: number) => {
  await act(async () => {
    await vi.advanceTimersByTimeAsync(ms)
  })
}

describe('useScoreboard', () => {
  beforeEach(() => {
    vi.useFakeTimers()
    vi.mocked(eventsApi.getScoreboard).mockReset()
    vi.mocked(eventsApi.getScoreboard).mockResolvedValue({ entries: [], tieBreakMode: 'ByTime' })
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('makes no request while disabled', async () => {
    renderHook(() => useScoreboard('event-1', { live: true, enabled: false }), { wrapper })
    await advance(SCOREBOARD_REFRESH_INTERVAL_MS * 2)

    expect(eventsApi.getScoreboard).not.toHaveBeenCalled()
  })

  it('polls a live scoreboard at the shared interval, not before', async () => {
    renderHook(() => useScoreboard('event-1', { live: true }), { wrapper })
    await advance(0)
    expect(eventsApi.getScoreboard).toHaveBeenCalledTimes(1)

    await advance(SCOREBOARD_REFRESH_INTERVAL_MS - 1)
    expect(eventsApi.getScoreboard).toHaveBeenCalledTimes(1)

    await advance(1)
    expect(eventsApi.getScoreboard).toHaveBeenCalledTimes(2)
  })

  it('does not poll when not live', async () => {
    renderHook(() => useScoreboard('event-1'), { wrapper })
    await advance(SCOREBOARD_REFRESH_INTERVAL_MS * 2)

    expect(eventsApi.getScoreboard).toHaveBeenCalledTimes(1)
  })
})
