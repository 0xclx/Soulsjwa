import { describe, expect, it } from 'vitest'
import {
  applyFilters,
  buildGamePages,
  buildObjectiveItems,
  filterEntry,
  getCompletedObjectiveNames,
  type ScopedEntry,
} from './overlayScope'
import { showsTrial } from '../scoreboard/scoreboardMetrics'
import { parseOverlayConfig } from './overlayConfig'
import type { GameBreakdown, ObjectiveDetail, ScoreboardEntry, TrialProgress } from '../../../types'

const objective = (overrides: Partial<ObjectiveDetail> = {}): ObjectiveDetail => ({
  objectiveId: 'o',
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
  isEnabled: false,
  isTrialActive: false,
  hasTrialRun: false,
  trial: null,
  ...overrides,
})

/**
 * A raw server entry, for the things that consume one (`filterEntry`,
 * `applyFilters`).
 */
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
 * An entry already narrowed to an overlay's scope, for the consumers that take
 * one. Defaults `scoringGameIds` to the enabled games — the same default scope
 * `filterEntry` applies — so a fixture cannot accidentally declare a trialed
 * game as scoring and hide the very mixing these tests check for.
 */
const scoped = (overrides: Partial<ScopedEntry> = {}): ScopedEntry => {
  const base = entry(overrides)
  const scoring = base.games.filter((g) => g.isEnabled)
  return {
    ...base,
    scoringGameIds: scoring.map((g) => g.eventGameId),
    scoringTotalObjectives: scoring.reduce((sum, g) => sum + g.totalObjectives, 0),
    scoringMaxScore: scoring.reduce(
      (sum, g) => sum + g.objectives.reduce((s, o) => s + o.score, 0),
      0,
    ),
    ...overrides,
  }
}

const defaultConfig = parseOverlayConfig('')

describe('filterEntry', () => {
  it('keeps a trialing game visible even though it is not the active game', () => {
    const result = filterEntry(
      entry({
        games: [
          game({ eventGameId: 'active', isEnabled: true }),
          game({ eventGameId: 'trialed', isEnabled: false, trial: trial({ score: 40 }) }),
          game({ eventGameId: 'past', isEnabled: false }),
        ],
      }),
      defaultConfig,
    )

    expect(result.games.map((g) => g.eventGameId)).toEqual(['active', 'trialed'])
  })

  it('excludes a trialing game from the official totals it is visible in', () => {
    const result = filterEntry(
      entry({
        // Server totals span every game; the overlay narrows them to the
        // games it scores.
        totalScore: 510,
        completedCount: 6,
        games: [
          game({ eventGameId: 'active', isEnabled: true, score: 10, completedCount: 1 }),
          game({
            eventGameId: 'trialed',
            isEnabled: false,
            score: 500,
            completedCount: 5,
            trial: trial({ score: 40 }),
          }),
        ],
      }),
      defaultConfig,
    )

    // Both games are visible, so a short-circuit on the visible set would
    // have left the server's 510 in place and moved this competitor's rank.
    expect(result.games).toHaveLength(2)
    expect(result.totalScore).toBe(10)
    expect(result.completedCount).toBe(1)
  })

  it('re-aggregates nothing when every game counts officially', () => {
    const unfiltered = entry({
      totalScore: 10,
      completedCount: 3,
      failedCount: 1,
      lastCompletedAt: '2026-01-02T00:00:00Z',
      games: [game({ eventGameId: 'active', isEnabled: true, score: 10, totalObjectives: 9 })],
    })

    const result = filterEntry(unfiltered, defaultConfig)

    // Every server figure is passed through rather than recomputed; only the
    // scope fields are added, so consumers never have to guess the official
    // denominator from `games`.
    expect(result).toMatchObject({
      totalScore: 10,
      completedCount: 3,
      failedCount: 1,
      lastCompletedAt: '2026-01-02T00:00:00Z',
      games: unfiltered.games,
      scoringGameIds: ['active'],
      scoringTotalObjectives: 9,
    })
  })
})

describe('applyFilters ranking', () => {
  it('keeps a trialing competitor behind, despite a huge unscored game', () => {
    // The trialing competitor's trial game also holds official score the
    // overlay is not scoring. If either the trial figures or that unscored
    // official score leaked into totalScore they would overtake `quiet`, so
    // the order below is genuinely derived rather than echoing the input.
    const quiet = entry({
      userId: 'quiet',
      displayName: 'Zzz',
      totalScore: 60,
      games: [game({ eventGameId: 'active', isEnabled: true, score: 60 })],
    })
    const trialing = entry({
      userId: 'trialing',
      displayName: 'Aaa',
      totalScore: 9999,
      games: [
        game({ eventGameId: 'active', isEnabled: true, score: 50 }),
        game({
          eventGameId: 'trialed',
          isEnabled: false,
          score: 9000,
          completedCount: 90,
          isTrialActive: true,
          trial: trial({ score: 9999, completedCount: 99 }),
        }),
      ],
    })

    const { entries } = applyFilters(
      { entries: [quiet, trialing], tieBreakMode: 'ByTime' },
      defaultConfig,
    )

    // 60 official beats 50 official; the 9000 unscored and 9999 trial are both
    // excluded, and the name order would have put 'Aaa' first on a tie.
    expect(entries.map((e) => e.userId)).toEqual(['quiet', 'trialing'])
    expect(entries.map((e) => e.totalScore)).toEqual([60, 50])
    expect(entries.map((e) => e.rank)).toEqual([1, 2])
  })
})

describe('getCompletedObjectiveNames', () => {
  it('reports the trial attempt for a trialing game', () => {
    const names = getCompletedObjectiveNames(
      scoped({
        games: [
          game({
            eventGameId: 'trialed',
            isTrialActive: true,
            trial: trial({ completedCount: 1 }),
            objectives: [
              // Officially complete, but not yet done in this trial.
              objective({ objectiveId: 'a', name: 'Official only', isCompleted: true }),
              objective({
                objectiveId: 'b',
                name: 'Done in trial',
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
        ],
      }),
    )

    expect(names).toEqual(['Done in trial'])
  })

  it('reports official completions for a game with no trial', () => {
    const names = getCompletedObjectiveNames(
      scoped({
        games: [game({ objectives: [objective({ name: 'Official only', isCompleted: true })] })],
      }),
    )

    expect(names).toEqual(['Official only'])
  })
})

describe('buildGamePages', () => {
  const pageFor = (games: GameBreakdown[], gameId: string) => {
    const pages = buildGamePages([
      scoped({
        // Event-wide server figures, spanning both games.
        totalScore: 60,
        completedCount: 5,
        failedCount: 4,
        lastCompletedAt: '2026-01-02T00:00:00Z',
        games,
      }),
    ])
    const index = games.findIndex((g) => g.eventGameId === gameId)
    return pages[index]![0]!
  }

  const first = game({
    eventGameId: 'first',
    score: 10,
    completedCount: 1,
    failedCount: 1,
    totalObjectives: 3,
    objectives: [objective({ isCompleted: true, completedAt: '2026-01-01T00:00:00Z' })],
  })
  const second = game({
    eventGameId: 'second',
    score: 50,
    completedCount: 4,
    failedCount: 3,
    // One objective still pending, so this game is deliberately unfinished.
    totalObjectives: 8,
    objectives: [objective({ isCompleted: true, completedAt: '2026-01-02T00:00:00Z' })],
  })

  it('narrows every figure the row reads to that game', () => {
    const page = pageFor([first, second], 'first')

    expect(page.totalScore).toBe(10)
    expect(page.completedCount).toBe(1)
    // Previously spread through from the entry, showing the event-wide 4.
    expect(page.failedCount).toBe(1)
  })

  it('narrows lastCompletedAt so a row only flashes for its own game', () => {
    const page = pageFor([first, second], 'first')

    // The entry's newest completion is in the other game; highlighting this
    // row for it would flash a competitor who did nothing here.
    expect(page.lastCompletedAt).toBe('2026-01-01T00:00:00Z')
  })

  it('derives finished state from that game alone', () => {
    const done = game({
      eventGameId: 'done',
      completedCount: 2,
      failedCount: 0,
      totalObjectives: 2,
    })
    const page = pageFor([done, second], 'done')

    expect(page.isFinished).toBe(true)
    expect(page.status).toBe('Completed')
    expect(pageFor([done, second], 'second').isFinished).toBe(false)
  })

  it("narrows a trialing game's page to that game alone", () => {
    const trialing = game({
      eventGameId: 'trialing',
      score: 5,
      completedCount: 1,
      failedCount: 0,
      totalObjectives: 4,
      isTrialActive: true,
      trial: trial({ score: 33, completedCount: 2 }),
    })
    const page = pageFor([first, trialing], 'trialing')

    // Derived, not echoed: the entry carries 60/5/4 across both games.
    expect(page.games.map((g) => g.eventGameId)).toEqual(['trialing'])
    expect(page.totalScore).toBe(5)
    expect(page.failedCount).toBe(0)
    expect(page.games[0]!.trial?.score).toBe(33)
  })
})

describe('paused trial does not override official state', () => {
  // Regression: substitution used to key on "has a trial", which includes a
  // paused run — so practising 3/10, stopping, then playing the real match
  // left the row pinned to the practice figures with no way back.
  const pausedTrialGame = game({
    eventGameId: 'g1',
    isEnabled: true,
    isTrialActive: false,
    trial: trial({ state: 'Paused', score: 30, completedCount: 3 }),
    objectives: [
      objective({
        objectiveId: 'o1',
        name: 'Officially done after the trial stopped',
        isCompleted: true,
        completedAt: '2026-01-02T00:00:00Z',
        trial: {
          isCompleted: false,
          completedAt: null,
          isFailed: false,
          failedAt: null,
          status: 'Pending',
        },
      }),
    ],
  })

  it('hands the objective marks back to the official record', () => {
    const items = buildObjectiveItems(scoped({ games: [pausedTrialGame] }))
    expect(items[0]!.isTrial).toBe(false)
  })

  it('lists the official completion, not the trial attempt', () => {
    const names = getCompletedObjectiveNames(scoped({ games: [pausedTrialGame] }))
    expect(names).toEqual(['Officially done after the trial stopped'])
  })

  it('still substitutes while the run is recording', () => {
    const recording = { ...pausedTrialGame, isTrialActive: true }
    expect(buildObjectiveItems(scoped({ games: [recording] }))[0]!.isTrial).toBe(true)
    expect(getCompletedObjectiveNames(scoped({ games: [recording] }))).toEqual([])
  })
})

describe('buildObjectiveItems', () => {
  it('flags rows belonging to a trialing game', () => {
    const items = buildObjectiveItems(
      scoped({
        games: [
          game({ eventGameId: 'plain', objectives: [objective({ objectiveId: 'a' })] }),
          game({
            eventGameId: 'trialed',
            isTrialActive: true,
            trial: trial(),
            objectives: [objective({ objectiveId: 'b' })],
          }),
        ],
      }),
    )

    expect(items.map((i) => i.isTrial)).toEqual([false, true])
  })
})

describe('showsTrial', () => {
  const scoring = game({
    eventGameId: 'active',
    isEnabled: true,
    totalObjectives: 10,
    trial: trial({ state: 'Paused' }),
  })
  const trialOnly = game({
    eventGameId: 'practice',
    isEnabled: false,
    totalObjectives: 20,
    trial: trial({ state: 'Paused' }),
  })
  const view = () => scoped({ games: [scoring, trialOnly] })

  it('hands a scored game back to the official record once its run pauses', () => {
    expect(showsTrial(scoring, view().scoringGameIds)).toBe(false)
    expect(showsTrial({ ...scoring, isTrialActive: true }, view().scoringGameIds)).toBe(true)
  })

  it('keeps showing the run on a game that is only in view because of it', () => {
    // Nothing official about this game is being reported — it is not scored —
    // so its official marks would be figures with no score beside them.
    expect(showsTrial(trialOnly, view().scoringGameIds)).toBe(true)
  })

  it('leaves a non-scored game with no trial on the official record', () => {
    const stale = game({ eventGameId: 'old', isEnabled: false })
    expect(showsTrial(stale, scoped({ games: [scoring, stale] }).scoringGameIds)).toBe(false)
  })
})

describe('filterEntry reports the scope its official figures were measured over', () => {
  it('excludes a trial-only game from the official denominator', () => {
    // Regression: the overlay row took its numerator from the scoring games
    // and its denominator from the visible ones, so starting a trial on an
    // unrelated game collapsed the official percentage (5/10 became 5/30)
    // with no official progress having changed.
    const result = filterEntry(
      entry({
        games: [
          game({
            eventGameId: 'active',
            isEnabled: true,
            completedCount: 5,
            score: 50,
            totalObjectives: 10,
          }),
          game({
            eventGameId: 'practice',
            totalObjectives: 20,
            trial: trial({ state: 'Paused', score: 99 }),
          }),
        ],
      }),
      defaultConfig,
    )

    expect(result.scoringGameIds).toEqual(['active'])
    expect(result.scoringTotalObjectives).toBe(10)
    expect(result.completedCount).toBe(5)
    // The trialed game is still on screen for its amber figures.
    expect(result.games.map((g) => g.eventGameId)).toEqual(['active', 'practice'])
  })

  it('reports the whole visible set as scoring when nothing was filtered out', () => {
    const result = filterEntry(
      entry({ games: [game({ eventGameId: 'only', isEnabled: true, totalObjectives: 7 })] }),
      defaultConfig,
    )

    expect(result.scoringGameIds).toEqual(['only'])
    expect(result.scoringTotalObjectives).toBe(7)
  })

  it('sums the points on offer over the scoring games only, whether or not anything was filtered', () => {
    const objectives = (...scores: number[]) =>
      scores.map((score, i) => ({
        objectiveId: `o${score}-${i}`,
        name: `o${i}`,
        score,
        category: null,
        isCompleted: false,
        completedAt: null,
        isFailed: false,
        failedAt: null,
        status: 'Pending' as const,
        trial: null,
      }))
    const active = game({ eventGameId: 'active', isEnabled: true, objectives: objectives(100, 50) })
    const past = game({ eventGameId: 'past', isEnabled: false, objectives: objectives(30) })

    // A past game in the entry but not in scope: its points are not on offer.
    expect(filterEntry(entry({ games: [past, active] }), defaultConfig).scoringMaxScore).toBe(150)
    // Nothing filtered: the same sum, through the pass-through branch.
    expect(filterEntry(entry({ games: [active] }), defaultConfig).scoringMaxScore).toBe(150)
    // Pinned to the past game, it is what is on offer.
    expect(
      filterEntry(entry({ games: [past, active] }), parseOverlayConfig('games=past'))
        .scoringMaxScore,
    ).toBe(30)
    // A game page narrows to that game's points.
    const pages = buildGamePages([scoped({ games: [past, active] })])
    expect(pages.map((page) => page[0]!.scoringMaxScore)).toEqual([0, 150])
  })

  it("names only completions whose points are in the row's score", () => {
    // A paused trial on a non-scored game used to contribute its *official*
    // completions to the ticker, naming objectives whose score was excluded.
    const names = getCompletedObjectiveNames(
      scoped({
        games: [
          game({
            eventGameId: 'active',
            isEnabled: true,
            objectives: [objective({ name: 'Real', isCompleted: true })],
          }),
          game({
            eventGameId: 'practice',
            trial: trial({ state: 'Paused' }),
            objectives: [objective({ objectiveId: 'x', name: 'Not scored', isCompleted: true })],
          }),
        ],
      }),
    )

    expect(names).toEqual(['Real'])
  })
})
