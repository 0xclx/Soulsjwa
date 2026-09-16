import { describe, expect, it } from 'vitest'
import {
  activeGame,
  entryView,
  gameLastCompletedAt,
  objectiveState,
  recordingGames,
  showsTrial,
  trialGames,
  trialTotals,
} from './scoreboardMetrics'
import type {
  GameBreakdown,
  ObjectiveDetail,
  TrialObjectiveState,
  TrialProgress,
} from '../../../types'

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
  isEnabled: false,
  isTrialActive: false,
  hasTrialRun: false,
  trial: null,
  ...overrides,
})

const trial = (overrides: Partial<TrialProgress> = {}): TrialProgress => ({
  trialRunId: 't',
  state: 'Running',
  score: 0,
  completedCount: 0,
  failedCount: 0,
  lastCompletedAt: null,
  ...overrides,
})

describe('gameLastCompletedAt', () => {
  it('returns null when no objective is completed', () => {
    expect(
      gameLastCompletedAt(
        game({
          objectives: [
            {
              objectiveId: 'o',
              name: 'O',
              score: 1,
              category: null,
              isCompleted: false,
              completedAt: null,
              isFailed: false,
              failedAt: null,
              status: 'Pending',
              trial: null,
            },
          ],
        }),
      ),
    ).toBeNull()
  })

  it('returns the latest completion timestamp among objectives', () => {
    expect(
      gameLastCompletedAt(
        game({
          objectives: [
            {
              objectiveId: 'a',
              name: 'A',
              score: 1,
              category: null,
              isCompleted: true,
              completedAt: '2026-01-01T10:00:00Z',
              isFailed: false,
              failedAt: null,
              status: 'Completed',
              trial: null,
            },
            {
              objectiveId: 'b',
              name: 'B',
              score: 1,
              category: null,
              isCompleted: true,
              completedAt: '2026-01-01T12:00:00Z',
              isFailed: false,
              failedAt: null,
              status: 'Completed',
              trial: null,
            },
          ],
        }),
      ),
    ).toBe('2026-01-01T12:00:00Z')
  })
})

describe('activeGame', () => {
  it('returns the one enabled game among several', () => {
    const enabled = game({ eventGameId: 'active', isEnabled: true })
    const games = [game({ eventGameId: 'past-1' }), enabled, game({ eventGameId: 'past-2' })]

    expect(activeGame(games)).toBe(enabled)
  })

  it('returns undefined when no game is enabled', () => {
    expect(activeGame([game(), game()])).toBeUndefined()
  })

  it('returns undefined for an empty list', () => {
    expect(activeGame([])).toBeUndefined()
  })
})

describe('trialGames', () => {
  it('includes a trial on a game that is not the active one', () => {
    const trialing = game({ eventGameId: 'disabled', isEnabled: false, trial: trial() })
    const games = [game({ eventGameId: 'active', isEnabled: true }), trialing]

    expect(trialGames(games)).toEqual([trialing])
  })

  it('excludes games without a started trial', () => {
    expect(trialGames([game(), game({ isEnabled: true })])).toEqual([])
  })
})

describe('trialTotals', () => {
  it('returns null when none of the given games has a trial', () => {
    expect(trialTotals([game(), game({ isEnabled: true })])).toBeNull()
  })

  it('sums only the games it is given', () => {
    const shown = game({ eventGameId: 'a', trial: trial({ score: 40, completedCount: 2 }) })
    const hidden = game({ eventGameId: 'b', trial: trial({ score: 500, completedCount: 9 }) })

    expect(trialTotals([shown])).toMatchObject({ score: 40, completedCount: 2 })
    expect(trialTotals([shown, hidden])).toMatchObject({ score: 540, completedCount: 11 })
  })

  it('reports the latest completion across contributing games', () => {
    const earlier = game({ trial: trial({ lastCompletedAt: '2026-01-01T10:00:00Z' }) })
    const later = game({ trial: trial({ lastCompletedAt: '2026-01-01T12:00:00Z' }) })

    expect(trialTotals([earlier, later])?.lastCompletedAt).toBe('2026-01-01T12:00:00Z')
  })

  it('still reports what a paused run earned', () => {
    // Display keeps a stopped run's score on screen; only substitution is
    // gated on it still recording — see recordingGames.
    const paused = game({ isTrialActive: false, trial: trial({ state: 'Paused', score: 7 }) })
    expect(trialTotals([paused])).toMatchObject({ score: 7 })
  })
})

describe('recordingGames', () => {
  it('includes only runs that are still recording', () => {
    const running = game({ eventGameId: 'running', isTrialActive: true, trial: trial() })
    const paused = game({
      eventGameId: 'paused',
      isTrialActive: false,
      trial: trial({ state: 'Paused' }),
    })

    // Both are "trial games" for display; only one may replace official state.
    expect(trialGames([running, paused])).toHaveLength(2)
    expect(recordingGames([running, paused])).toEqual([running])
  })

  it('is empty when every run is paused', () => {
    const paused = game({ isTrialActive: false, trial: trial({ state: 'Paused' }) })
    expect(recordingGames([paused])).toEqual([])
  })
})

const objectiveTrial = (overrides: Partial<TrialObjectiveState> = {}): TrialObjectiveState => ({
  isCompleted: false,
  completedAt: null,
  isFailed: false,
  failedAt: null,
  status: 'Pending',
  ...overrides,
})

const objective = (overrides: Partial<ObjectiveDetail> = {}): ObjectiveDetail => ({
  objectiveId: 'o',
  name: 'O',
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

describe('objectiveState', () => {
  it('returns the official record when no trial is being shown', () => {
    const official = objective({ isCompleted: true, completedAt: '2026-01-01T10:00:00Z' })
    expect(objectiveState(official, false)).toBe(official)
  })

  it('returns the official record even when a trial row exists but is not shown', () => {
    // A paused run: its score stays on screen, but the marks are the official
    // ones again, because that is where completions are landing.
    const official = objective({ isCompleted: true, trial: objectiveTrial({ isCompleted: false }) })
    expect(objectiveState(official, false).isCompleted).toBe(true)
  })

  it('returns the trial attempt while the trial is shown', () => {
    const attempt = objectiveTrial({ isCompleted: true, completedAt: '2026-01-01T12:00:00Z' })
    expect(objectiveState(objective({ trial: attempt }), true)).toBe(attempt)
  })

  it('reports nothing done rather than the official mark when a trial row is missing', () => {
    // The server sends trial state for every objective of a trialed game, so
    // this is the guard, not a real case — but falling back to official here
    // would tick an objective the competitor has not done in *this* run.
    const state = objectiveState(objective({ isCompleted: true, isFailed: false }), true)
    expect(state.isCompleted).toBe(false)
    expect(state.status).toBe('Pending')
  })
})

describe('entryView', () => {
  it('shows the active game and appends a trial on a different game', () => {
    const active = game({ eventGameId: 'active', isEnabled: true })
    const trialed = game({ eventGameId: 'trialed', trial: trial({ score: 12 }) })

    const view = entryView([active, game({ eventGameId: 'past' }), trialed])

    expect(view.activeGame).toBe(active)
    expect(view.breakdownGames).toEqual([active, trialed])
    expect(view.trials).toMatchObject({ score: 12 })
  })

  it('lists the active game once when the trial is on that same game', () => {
    const active = game({ eventGameId: 'active', isEnabled: true, trial: trial({ score: 5 }) })

    expect(entryView([active]).breakdownGames).toEqual([active])
  })

  it('shows a trialed game on its own when no game is enabled yet', () => {
    const trialed = game({ eventGameId: 'trialed', trial: trial() })

    const view = entryView([game({ eventGameId: 'past' }), trialed])

    expect(view.activeGame).toBeUndefined()
    expect(view.breakdownGames).toEqual([trialed])
  })

  it('reports no trial and no breakdown for an entry with neither', () => {
    const view = entryView([game({ eventGameId: 'past' })])

    expect(view.trials).toBeNull()
    expect(view.breakdownGames).toEqual([])
  })
})

describe('showsTrial', () => {
  const scored = game({
    eventGameId: 'active',
    isEnabled: true,
    trial: trial({ state: 'Paused' }),
  })
  const trialOnly = game({ eventGameId: 'practice', trial: trial({ state: 'Paused' }) })
  const scope = ['active']

  it('hands a scored game back to the official record once its run pauses', () => {
    expect(showsTrial(scored, scope)).toBe(false)
  })

  it('substitutes on a scored game while its run records', () => {
    expect(showsTrial({ ...scored, isTrialActive: true }, scope)).toBe(true)
  })

  it('keeps showing the run on a game that is in view only because of it', () => {
    // Nothing official about this game is reported — it is not in the score —
    // so its official marks would be ticks with no points behind them.
    expect(showsTrial(trialOnly, scope)).toBe(true)
  })

  it('leaves a non-scored game with no trial on the official record', () => {
    expect(showsTrial(game({ eventGameId: 'old' }), scope)).toBe(false)
  })

  it('treats an empty scope as scoring nothing', () => {
    // No active game yet: a trialed game is then the only thing on screen.
    expect(showsTrial(trialOnly, [])).toBe(true)
    expect(showsTrial(game({ eventGameId: 'x' }), [])).toBe(false)
  })
})

describe('entryView scope', () => {
  it('scores the active game alone', () => {
    const view = entryView([
      game({ eventGameId: 'active', isEnabled: true }),
      game({ eventGameId: 'practice', trial: trial() }),
      game({ eventGameId: 'past' }),
    ])

    expect(view.scoringGameIds).toEqual(['active'])
  })

  it('scores nothing when no game is enabled', () => {
    expect(entryView([game({ eventGameId: 'practice', trial: trial() })]).scoringGameIds).toEqual(
      [],
    )
  })
})
