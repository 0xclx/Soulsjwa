import type { GameBreakdown, ObjectiveDetail, ScoreboardResponse } from '../../../types'

/**
 * A game as the sample builder needs it: the event's real games, with their
 * real objectives when the event has any, so the pins the form offers and the
 * names on screen match what the live overlay will show.
 */
export interface SampleGame {
  eventGameId: string
  gameName: string
  isEnabled: boolean
  objectives: SampleObjective[]
}

export interface SampleObjective {
  id: string
  name: string
  score: number
  category: string | null
}

/**
 * Placeholder objectives for a game that has none defined yet, so an overlay
 * can be designed before the event's objectives are written.
 */
export const SAMPLE_OBJECTIVES: readonly SampleObjective[] = [
  { id: 'sample-objective-1', name: 'Reach the first bonfire', score: 5, category: 'Opening' },
  { id: 'sample-objective-2', name: 'Defeat the gatekeeper', score: 15, category: 'Opening' },
  { id: 'sample-objective-3', name: 'Find the hidden path', score: 10, category: 'Midgame' },
  { id: 'sample-objective-4', name: 'Ring the first bell', score: 25, category: 'Midgame' },
  { id: 'sample-objective-5', name: 'Ring the second bell', score: 25, category: 'Midgame' },
  { id: 'sample-objective-6', name: 'Slay the final boss', score: 50, category: 'Endgame' },
]

/** Fictional competitors — named after player titles, not people, so they read as placeholders. */
export const SAMPLE_COMPETITORS: readonly {
  userId: string
  displayName: string
  isLive: boolean
}[] = [
  { userId: 'sample-competitor-1', displayName: 'Chosen Undead', isLive: true },
  { userId: 'sample-competitor-2', displayName: 'Bearer of the Curse', isLive: true },
  { userId: 'sample-competitor-3', displayName: 'Ashen One', isLive: false },
  { userId: 'sample-competitor-4', displayName: 'Tarnished', isLive: true },
]

/** How far along each sample competitor starts, as a share of the objectives, before `tick` advances them. */
const STARTING_PROGRESS: readonly number[] = [0.7, 0.55, 0.4, 0.2]

/** Minutes between two consecutive sample completions, counting back from `base`. */
const MINUTES_BETWEEN_COMPLETIONS = 7

/**
 * How many objectives competitor `index` has completed beyond its start at
 * `tick`: ticks are dealt round-robin, one competitor at a time, so each tick
 * moves exactly one row.
 */
export function sampleAdvances(index: number, tick: number): number {
  const count = SAMPLE_COMPETITORS.length
  return Math.floor(tick / count) + (tick % count > index ? 1 : 0)
}

/**
 * A scoreboard of fictional competitors over the event's real games, for
 * designing an overlay before the event has anything to show. Deterministic
 * for a given `tick` and `base`: each tick completes one more objective for
 * one competitor in turn (wrapping around once a run is complete), so a
 * preview that advances the tick shows one progress bar move and one row
 * flash at a time, the way a live event does. Timestamps are fixed offsets
 * from `base` per objective, so nothing else on the board changes between
 * ticks and a row flashes only when its own newest completion did. Games
 * with no objectives get `SAMPLE_OBJECTIVES`.
 */
export function buildSampleScoreboard(
  games: readonly SampleGame[],
  tick: number,
  base: Date,
): ScoreboardResponse {
  const entries = SAMPLE_COMPETITORS.map((competitor, index) => {
    const breakdown = games.map((game) => sampleGameBreakdown(game, index, tick, base))
    const scoring = breakdown.filter((g) => g.isEnabled)
    const counted = scoring.length > 0 ? scoring : breakdown
    const completedAts = counted
      .flatMap((g) => g.objectives)
      .map((o) => o.completedAt)
      .filter((at): at is string => at !== null)
      .sort()
    return {
      userId: competitor.userId,
      displayName: competitor.displayName,
      twitchLogin: competitor.userId,
      isLive: competitor.isLive,
      totalScore: counted.reduce((sum, g) => sum + g.score, 0),
      completedCount: counted.reduce((sum, g) => sum + g.completedCount, 0),
      failedCount: 0,
      isFinished: false,
      lastCompletedAt: completedAts.at(-1) ?? null,
      totalInGameTimeMs: null,
      rank: 0,
      status: 'Pending' as const,
      games: breakdown,
    }
  })

  entries.sort((a, b) => b.totalScore - a.totalScore || a.displayName.localeCompare(b.displayName))
  return {
    tieBreakMode: 'ByTime',
    entries: entries.map((entry, i) => ({ ...entry, rank: i + 1 })),
  }
}

function sampleGameBreakdown(
  game: SampleGame,
  competitorIndex: number,
  tick: number,
  base: Date,
): GameBreakdown {
  const source = game.objectives.length > 0 ? game.objectives : SAMPLE_OBJECTIVES
  const start = Math.round(source.length * (STARTING_PROGRESS[competitorIndex] ?? 0))
  // Wraps to "nothing done yet" one advance after everything is done, so a
  // long-running preview keeps moving instead of parking at 100%.
  const completed = (start + sampleAdvances(competitorIndex, tick)) % (source.length + 1)

  const objectives: ObjectiveDetail[] = source.map((objective, i) => {
    const isCompleted = i < completed
    // Later objectives carry later timestamps, so completing the next one
    // always advances the row's newest completion.
    const completedAt = isCompleted
      ? new Date(
          base.getTime() - (source.length - i) * MINUTES_BETWEEN_COMPLETIONS * 60_000,
        ).toISOString()
      : null
    return {
      objectiveId: objective.id,
      name: objective.name,
      score: objective.score,
      category: objective.category,
      isCompleted,
      completedAt,
      isFailed: false,
      failedAt: null,
      status: isCompleted ? 'Completed' : 'Pending',
      trial: null,
    }
  })

  return {
    eventGameId: game.eventGameId,
    gameName: game.gameName,
    score: objectives.filter((o) => o.isCompleted).reduce((sum, o) => sum + o.score, 0),
    completedCount: completed,
    totalObjectives: source.length,
    objectives,
    infos: [],
    hasDeathClip: false,
    failedCount: 0,
    isEnabled: game.isEnabled,
    isTrialActive: false,
    hasTrialRun: false,
    trial: null,
  }
}
