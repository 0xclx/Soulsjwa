import { act, renderHook } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { useChangeHighlights } from './useChangeHighlights'
import type { GameBreakdown, ScoreboardEntry, TrialProgress } from '../../../types'

const trial = (overrides: Partial<TrialProgress> = {}): TrialProgress => ({
  trialRunId: 't',
  state: 'Running',
  score: 0,
  completedCount: 0,
  failedCount: 0,
  lastCompletedAt: null,
  ...overrides,
})

const game = (overrides: Partial<GameBreakdown> = {}): GameBreakdown => ({
  eventGameId: 'g',
  gameName: 'Game',
  score: 0,
  completedCount: 0,
  totalObjectives: 0,
  objectives: [],
  infos: [],
  hasDeathClip: false,
  failedCount: 0,
  isEnabled: true,
  isTrialActive: false,
  hasTrialRun: false,
  trial: null,
  ...overrides,
})

const entry = (overrides: Partial<ScoreboardEntry> = {}): ScoreboardEntry => ({
  userId: 'u',
  displayName: 'Player',
  twitchLogin: 'player',
  isLive: false,
  totalScore: 0,
  completedCount: 0,
  isFinished: false,
  lastCompletedAt: null,
  totalInGameTimeMs: null,
  rank: 1,
  games: [],
  failedCount: 0,
  status: 'Pending',
  ...overrides,
})

/**
 * Renders the hook, settles the deferred first pass (which only records the
 * baseline), then re-renders with `next` and settles again.
 */
const flashesOn = (first: ScoreboardEntry, next: ScoreboardEntry): boolean => {
  const { result, rerender } = renderHook(({ entries }) => useChangeHighlights(entries, true, 5), {
    initialProps: { entries: [first] },
  })
  act(() => void vi.advanceTimersByTime(1))
  rerender({ entries: [next] })
  act(() => void vi.advanceTimersByTime(1))
  return result.current.has(next.userId)
}

describe('useChangeHighlights', () => {
  beforeEach(() => vi.useFakeTimers())
  afterEach(() => vi.useRealTimers())

  it('flashes an official completion', () => {
    expect(
      flashesOn(
        entry({ lastCompletedAt: null }),
        entry({ lastCompletedAt: '2026-01-01T10:00:00Z' }),
      ),
    ).toBe(true)
  })

  it('flashes a trial completion', () => {
    expect(
      flashesOn(
        entry({ games: [game({ isTrialActive: true, trial: trial() })] }),
        entry({
          games: [
            game({
              isTrialActive: true,
              trial: trial({ lastCompletedAt: '2026-01-01T12:00:00Z' }),
            }),
          ],
        }),
      ),
    ).toBe(true)
  })

  it('does not flash when a run is merely paused', () => {
    // Regression: the tracked value used to switch between the trial and
    // official sources on `isTrialActive`, so pausing swapped a 12:00 trial
    // timestamp for a 10:00 official one and the row flashed as though the
    // competitor had just scored. Every Stop produced a phantom completion.
    const recording = entry({
      lastCompletedAt: '2026-01-01T10:00:00Z',
      games: [
        game({ isTrialActive: true, trial: trial({ lastCompletedAt: '2026-01-01T12:00:00Z' }) }),
      ],
    })
    const paused = entry({
      lastCompletedAt: '2026-01-01T10:00:00Z',
      games: [
        game({
          isTrialActive: false,
          trial: trial({ state: 'Paused', lastCompletedAt: '2026-01-01T12:00:00Z' }),
        }),
      ],
    })

    expect(flashesOn(recording, paused)).toBe(false)
    expect(flashesOn(paused, recording)).toBe(false)
  })

  it('flashes an official completion made while a trial is recording', () => {
    // The score column is official and does move, so the flash that exists to
    // make a change readable has to fire — tracking only the trial's
    // timestamp meant it never did.
    const before = entry({
      lastCompletedAt: '2026-01-01T10:00:00Z',
      games: [
        game({ isTrialActive: true, trial: trial({ lastCompletedAt: '2026-01-01T12:00:00Z' }) }),
      ],
    })
    const after = entry({
      lastCompletedAt: '2026-01-01T13:00:00Z',
      games: [
        game({ isTrialActive: true, trial: trial({ lastCompletedAt: '2026-01-01T12:00:00Z' }) }),
      ],
    })

    expect(flashesOn(before, after)).toBe(true)
  })

  it('does not flash when a trial is disabled and its timestamp disappears', () => {
    const withTrial = entry({
      games: [
        game({ isTrialActive: true, trial: trial({ lastCompletedAt: '2026-01-01T12:00:00Z' }) }),
      ],
    })
    const withoutTrial = entry({ games: [game()] })

    expect(flashesOn(withTrial, withoutTrial)).toBe(false)
  })

  it('records a baseline on the first pass without flashing', () => {
    const { result } = renderHook(() =>
      useChangeHighlights([entry({ lastCompletedAt: '2026-01-01T10:00:00Z' })], true, 5),
    )
    act(() => void vi.advanceTimersByTime(1))

    expect(result.current.size).toBe(0)
  })

  it('clears the highlight once its duration elapses', () => {
    const { result, rerender } = renderHook(
      ({ entries }) => useChangeHighlights(entries, true, 5),
      { initialProps: { entries: [entry()] } },
    )
    act(() => void vi.advanceTimersByTime(1))
    rerender({ entries: [entry({ lastCompletedAt: '2026-01-01T10:00:00Z' })] })
    act(() => void vi.advanceTimersByTime(1))
    expect(result.current.size).toBe(1)

    act(() => void vi.advanceTimersByTime(5000))
    expect(result.current.size).toBe(0)
  })
})

describe('useChangeHighlights row keys', () => {
  it('keys rows however the caller says, so one competitor can flash on one page and not another', () => {
    vi.useFakeTimers()
    const rows = (whenA: string | null, whenB: string | null) => [
      entry({ userId: 'u', lastCompletedAt: whenA, games: [game({ eventGameId: 'a' })] }),
      entry({ userId: 'u', lastCompletedAt: whenB, games: [game({ eventGameId: 'b' })] }),
    ]
    const keyOf = (e: ScoreboardEntry) => `${e.userId}:${e.games[0]!.eventGameId}`
    const { result, rerender } = renderHook(
      ({ entries }) => useChangeHighlights(entries, true, 5, keyOf),
      { initialProps: { entries: rows(null, null) } },
    )

    rerender({ entries: rows(null, '2026-01-01T00:00:00.000Z') })
    act(() => {
      vi.advanceTimersByTime(0)
    })

    expect([...result.current]).toEqual(['u:b'])
    vi.useRealTimers()
  })
})
