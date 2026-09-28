import { act, renderHook } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import type { ReactNode } from 'react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { useLiveEvent } from './useLiveEvent'
import { eventsApi, EVENTS_QUERY_KEYS } from '../api/eventsApi'
import { SCOREBOARD_REFRESH_INTERVAL_MS } from '../scoreboard/scoreboardPolling'
import type { EventGame, EventResponse, ScoreboardResponse } from '../../../types'

vi.mock('../api/eventsApi', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../api/eventsApi')>()
  return { ...actual, eventsApi: { ...actual.eventsApi, get: vi.fn() } }
})

const EVENT_ID = 'event-1'

const game = (eventGameId: string, isEnabled: boolean): EventGame =>
  ({ eventGameId, gameName: eventGameId, isEnabled, objectives: [] }) as unknown as EventGame

const makeEvent = (overrides: Partial<EventResponse> = {}): EventResponse => ({
  id: EVENT_ID,
  name: 'Lordran Relay',
  urlAlias: null,
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
  games: [game('a', false), game('b', false)],
  ...overrides,
})

const setup = (initial: EventResponse) => {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  // A scoreboard already in the cache, as if the view had fetched it.
  const scoreboard: ScoreboardResponse = { entries: [], tieBreakMode: 'ByTime' }
  client.setQueryData(EVENTS_QUERY_KEYS.scoreboard(EVENT_ID), scoreboard)
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  )
  const hook = renderHook(() => useLiveEvent(initial), { wrapper })
  const scoreboardInvalidated = () =>
    client.getQueryState(EVENTS_QUERY_KEYS.scoreboard(EVENT_ID))?.isInvalidated ?? false
  return { ...hook, scoreboardInvalidated }
}

/** Advances fake time by exactly `ms`, running the timers (and fetches) due. */
const advance = async (ms: number) => {
  await act(async () => {
    await vi.advanceTimersByTimeAsync(ms)
  })
}

/**
 * Lets a fetch that just fired reach the hook. TanStack Query hands new data
 * to observers on a `setTimeout(0)`, and fake timers schedule a zero-delay
 * timeout created inside a tick 1 ms later, so the clock has to move a little.
 */
const SETTLE_MS = 5
const settle = () => advance(SETTLE_MS)

describe('useLiveEvent', () => {
  beforeEach(() => {
    vi.useFakeTimers()
    vi.mocked(eventsApi.get).mockReset()
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('returns the passed event before the first fetch lands', () => {
    vi.mocked(eventsApi.get).mockReturnValue(new Promise(() => {}))
    const initial = makeEvent({ name: 'Passed in' })

    const { result } = setup(initial)

    expect(result.current.name).toBe('Passed in')
  })

  it('polls an unstarted event at the scoreboard interval, not before', async () => {
    expect(SCOREBOARD_REFRESH_INTERVAL_MS).toBe(30_000)
    vi.mocked(eventsApi.get).mockResolvedValue(makeEvent())
    setup(makeEvent())
    await advance(0)
    const afterMount = vi.mocked(eventsApi.get).mock.calls.length

    await advance(SCOREBOARD_REFRESH_INTERVAL_MS - 1)
    expect(eventsApi.get).toHaveBeenCalledTimes(afterMount)

    await advance(1)
    expect(eventsApi.get).toHaveBeenCalledTimes(afterMount + 1)
    expect(eventsApi.get).toHaveBeenLastCalledWith(EVENT_ID)
  })

  it('reports the event starting on the next poll', async () => {
    vi.mocked(eventsApi.get).mockResolvedValue(makeEvent())
    const { result } = setup(makeEvent())
    await settle()
    expect(result.current.isStarted).toBe(false)

    vi.mocked(eventsApi.get).mockResolvedValue(makeEvent({ isStarted: true }))
    await advance(SCOREBOARD_REFRESH_INTERVAL_MS)
    await settle()

    expect(result.current.isStarted).toBe(true)
  })

  it('invalidates the scoreboard when the event starts', async () => {
    vi.mocked(eventsApi.get).mockResolvedValue(makeEvent())
    const { scoreboardInvalidated } = setup(makeEvent())
    await settle()
    expect(scoreboardInvalidated()).toBe(false)

    vi.mocked(eventsApi.get).mockResolvedValue(makeEvent({ isStarted: true }))
    await advance(SCOREBOARD_REFRESH_INTERVAL_MS)
    await settle()

    expect(scoreboardInvalidated()).toBe(true)
  })

  it('invalidates the scoreboard when the enabled game changes', async () => {
    const started = (enabled: string | null) =>
      makeEvent({
        isStarted: true,
        games: [game('a', enabled === 'a'), game('b', enabled === 'b')],
      })
    vi.mocked(eventsApi.get).mockResolvedValue(started('a'))
    const { scoreboardInvalidated } = setup(started('a'))
    await settle()
    expect(scoreboardInvalidated()).toBe(false)

    vi.mocked(eventsApi.get).mockResolvedValue(started('b'))
    await advance(SCOREBOARD_REFRESH_INTERVAL_MS)
    await settle()

    expect(scoreboardInvalidated()).toBe(true)
  })

  it('leaves the scoreboard alone when a poll changes nothing it depends on', async () => {
    vi.mocked(eventsApi.get).mockResolvedValue(makeEvent({ isStarted: true }))
    const { scoreboardInvalidated } = setup(makeEvent({ isStarted: true }))
    await settle()

    vi.mocked(eventsApi.get).mockResolvedValue(
      makeEvent({ isStarted: true, description: 'edited' }),
    )
    await advance(SCOREBOARD_REFRESH_INTERVAL_MS)
    await settle()

    expect(scoreboardInvalidated()).toBe(false)
  })
})
