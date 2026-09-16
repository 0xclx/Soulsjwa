import { describe, expect, it, vi } from 'vitest'
import { renderHook } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import type { ReactNode } from 'react'
import { useObjectiveCompletion } from './useObjectiveCompletion'
import type {
  EventResponse,
  GameBreakdown,
  ObjectiveDetail,
  ScoreboardResponse,
  User,
} from '../../../types'

vi.mock('./useCompleteObjective', () => ({ useCompleteObjective: () => ({ isPending: false }) }))
vi.mock('./useUncompleteObjective', () => ({
  useUncompleteObjective: () => ({ isPending: false }),
}))
vi.mock('./useFailObjective', () => ({ useFailObjective: () => ({ isPending: false }) }))
vi.mock('./useResetFailedObjective', () => ({
  useResetFailedObjective: () => ({ isPending: false }),
}))
vi.mock('./useFailRemainingObjectives', () => ({
  useFailRemainingObjectives: () => ({ isPending: false }),
}))

const USER_ID = '11111111-1111-1111-1111-111111111111'

const objective = (overrides: Partial<ObjectiveDetail> = {}): ObjectiveDetail => ({
  objectiveId: 'o1',
  name: 'Objective',
  score: 10,
  category: null,
  isCompleted: false,
  completedAt: null,
  isFailed: false,
  failedAt: null,
  status: 'Pending',
  trial: null,
  ...overrides,
})

const game = (overrides: Partial<GameBreakdown> = {}): GameBreakdown => ({
  eventGameId: 'g1',
  gameName: 'Game',
  score: 0,
  completedCount: 0,
  totalObjectives: 1,
  objectives: [objective()],
  infos: [],
  hasDeathClip: false,
  failedCount: 0,
  isEnabled: true,
  isTrialActive: false,
  hasTrialRun: false,
  trial: null,
  ...overrides,
})

const scoreboard = (games: GameBreakdown[]): ScoreboardResponse => ({
  tieBreakMode: 'SharedPlace',
  entries: [
    {
      userId: USER_ID,
      displayName: 'Player',
      twitchLogin: 'player',
      isLive: false,
      totalScore: 0,
      completedCount: 0,
      isFinished: false,
      lastCompletedAt: null,
      totalInGameTimeMs: null,
      rank: 1,
      games,
      failedCount: 0,
      status: 'Pending',
    },
  ],
})

const event = {
  id: 'event-1',
  isStarted: true,
  createdById: USER_ID,
  competitors: [{ userId: USER_ID, displayName: 'Player' }],
} as unknown as EventResponse

const currentUser = { id: USER_ID } as User

const wrapper = ({ children }: { children: ReactNode }) => (
  <QueryClientProvider client={new QueryClient()}>{children}</QueryClientProvider>
)

const render = (games: GameBreakdown[]) =>
  renderHook(() => useObjectiveCompletion('event-1', event, currentUser, scoreboard(games)), {
    wrapper,
  })

describe('useObjectiveCompletion trial gating', () => {
  it('gates a game whose trial is recording, and says the tick would go to the run', () => {
    const { result } = render([
      game({ eventGameId: 'plain' }),
      game({ eventGameId: 'trialing', hasTrialRun: true, isTrialActive: true }),
    ])

    expect([...result.current.trialBlockReasons.keys()]).toEqual(['trialing'])
    expect(result.current.trialBlockReasons.get('trialing')).toMatch(/recording for this game/)
  })

  it('gates a game whose trial is enabled but dormant, and says nothing is recorded', () => {
    // The whole point of the rule: a dormant slot must not leave an official
    // checkbox live, because the server refuses that write outright.
    const { result } = render([game({ eventGameId: 'dormant', hasTrialRun: true })])

    expect([...result.current.trialBlockReasons.keys()]).toEqual(['dormant'])
    expect(result.current.trialBlockReasons.get('dormant')).toMatch(/not recording/)
  })

  it('is empty when trial mode is off everywhere', () => {
    const { result } = render([game()])
    expect(result.current.trialBlockReasons.size).toBe(0)
  })

  it('gates only the trialed game, leaving its siblings editable', () => {
    // The composed value is what EventGamesSection passes to each GameCard,
    // so assert the gate itself rather than the fixture's own flags.
    const { result } = render([
      game({ eventGameId: 'plain' }),
      game({ eventGameId: 'trialing', hasTrialRun: true, isTrialActive: true }),
    ])

    expect(result.current.canToggleCompletion).toBe(true)
    expect(
      result.current.canToggleCompletion && !result.current.trialBlockReasons.has('plain'),
    ).toBe(true)
    expect(
      result.current.canToggleCompletion && !result.current.trialBlockReasons.has('trialing'),
    ).toBe(false)
  })

  it('reports an official completion but not a trial-only one', () => {
    // The games tab shows official state; a trial completion must not tick
    // its checkbox, which is exactly why that game is gated.
    const { result } = render([
      game({
        eventGameId: 'trialing',
        hasTrialRun: true,
        isTrialActive: true,
        objectives: [
          objective({
            objectiveId: 'official',
            isCompleted: true,
            completedAt: '2026-01-01T00:00:00Z',
          }),
          objective({
            objectiveId: 'trialOnly',
            isCompleted: false,
            trial: {
              isCompleted: true,
              completedAt: '2026-01-01T00:00:00Z',
              isFailed: false,
              failedAt: null,
              status: 'Completed',
            },
          }),
        ],
      }),
    ])

    expect([...result.current.completedObjectiveIds]).toEqual(['official'])
  })
})
