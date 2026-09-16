import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import { render, screen, act, fireEvent } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { useMyEventObjectives } from './useMyEventObjectives'
import { useToggleMyEventObjective } from './useToggleMyEventObjective'
import { myEventsApi } from '../api/myEventsApi'
import { eventsApi } from '../../events/api/eventsApi'
import type { MyEventObjectivesResponse } from '../../../types'

vi.mock('../api/myEventsApi', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../api/myEventsApi')>()
  return { ...actual, myEventsApi: { ...actual.myEventsApi, getObjectives: vi.fn() } }
})

vi.mock('../../events/api/eventsApi', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../events/api/eventsApi')>()
  return { ...actual, eventsApi: { ...actual.eventsApi, completeObjective: vi.fn() } }
})

const EVENT_ID = 'event-1'
const GAME_ID = 'game-1'
const OBJECTIVE_ID = 'objective-1'

const makeResponse = (completed: boolean): MyEventObjectivesResponse => ({
  competitorId: 'competitor-1',
  competitorName: 'Chosen Undead',
  games: [
    {
      gameId: GAME_ID,
      gameName: 'Dark Souls',
      isTrialActive: false,
      hasTrialRun: false,
      objectives: [
        {
          objectiveId: OBJECTIVE_ID,
          name: 'Ring both Bells of Awakening',
          completed,
          completedAt: completed ? '2026-01-01T00:00:00Z' : null,
          score: 100,
          failed: false,
          failedAt: null,
        },
      ],
    },
  ],
})

const INCOMPLETE_RESPONSE = makeResponse(false)
const COMPLETED_RESPONSE = makeResponse(true)

/** Combines the two real hooks the way MyEventsPanel wires them: the toggle
 * mutation's pending state gates the objectives poll. */
function Harness() {
  const toggle = useToggleMyEventObjective(EVENT_ID, undefined)
  const objectives = useMyEventObjectives(EVENT_ID, undefined, true, toggle.isPending)
  const objective = objectives.data?.games[0]?.objectives[0]

  return (
    <div>
      <span data-testid="completed">{String(objective?.completed ?? false)}</span>
      <button
        onClick={() =>
          objective &&
          toggle.mutate({
            eventGameId: GAME_ID,
            objectiveId: OBJECTIVE_ID,
            completed: objective.completed,
          })
        }
      >
        toggle
      </button>
    </div>
  )
}

const renderHarness = () => {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <Harness />
    </QueryClientProvider>,
  )
}

describe('optimistic toggle vs. poll race', () => {
  beforeEach(() => {
    vi.mocked(myEventsApi.getObjectives).mockReset()
    vi.mocked(eventsApi.completeObjective).mockReset()
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('does not let a poll tick overwrite the optimistic value while the toggle is still pending', async () => {
    vi.useFakeTimers()
    vi.mocked(myEventsApi.getObjectives).mockResolvedValue(INCOMPLETE_RESPONSE)
    let resolveComplete!: () => void
    vi.mocked(eventsApi.completeObjective).mockImplementation(
      () =>
        new Promise<void>((resolve) => {
          resolveComplete = resolve
        }),
    )

    renderHarness()
    await act(async () => {
      await vi.advanceTimersByTimeAsync(0)
    })
    expect(screen.getByTestId('completed')).toHaveTextContent('false')
    expect(myEventsApi.getObjectives).toHaveBeenCalledTimes(1)

    await act(async () => {
      fireEvent.click(screen.getByText('toggle'))
      await vi.advanceTimersByTimeAsync(0)
    })
    // The optimistic update lands immediately.
    expect(screen.getByTestId('completed')).toHaveTextContent('true')

    // Advance past a full poll interval (5s) while the mutation is still
    // in flight. Without gating the poll on the pending toggle, this would
    // fire a refetch that overwrites the optimistic value with the
    // pre-mutation (still-incomplete) server response.
    await act(async () => {
      await vi.advanceTimersByTimeAsync(5000)
    })
    expect(myEventsApi.getObjectives).toHaveBeenCalledTimes(1)
    expect(screen.getByTestId('completed')).toHaveTextContent('true')

    // Once the mutation actually settles, onSettled's invalidation refetches
    // the authoritative value — polling is no longer suppressed. The server
    // has by now processed the completion, so the refetch confirms it.
    vi.mocked(myEventsApi.getObjectives).mockResolvedValue(COMPLETED_RESPONSE)
    await act(async () => {
      resolveComplete()
      await vi.advanceTimersByTimeAsync(0)
    })
    expect(vi.mocked(myEventsApi.getObjectives).mock.calls.length).toBeGreaterThanOrEqual(2)
    expect(screen.getByTestId('completed')).toHaveTextContent('true')
  })
})
