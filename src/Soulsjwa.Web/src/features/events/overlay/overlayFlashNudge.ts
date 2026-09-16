import type {
  GameBreakdown,
  ObjectiveDetail,
  ScoreboardEntry,
  ScoreboardResponse,
} from '../../../types'

/**
 * A completion the designer pretends happened, so the overlay reacts on
 * demand. Resolved once, when it is pressed: the objective is fixed then and
 * never re-chosen, so the board later completing that objective for real
 * (or the sample data catching up) changes nothing on screen — a nudge that
 * re-resolved itself used to hop to the next open objective and flash it
 * out of nowhere.
 */
export interface FlashNudge {
  userId: string
  /** The objective stamped, or null for a competitor with no objectives (row-level timestamp only). */
  objectiveId: string | null
  /** ISO timestamp the completion is stamped with. */
  at: string
}

/**
 * Chooses what a press would complete for `entry`: the next open objective
 * of an enabled game first (so the objectives view animates a new tick and
 * the counts follow), else the newest completed one, re-stamped. Null when
 * the competitor has no objectives at all.
 */
export function pickNudgeTarget(entry: ScoreboardEntry): { objectiveId: string } | null {
  const candidates = [...entry.games].sort((a, b) => Number(b.isEnabled) - Number(a.isEnabled))
  for (const game of candidates) {
    const open = game.objectives.find((o) => !o.isCompleted && !o.isFailed)
    const newest = [...game.objectives]
      .filter((o) => o.isCompleted && !o.isFailed)
      .sort((a, b) => (b.completedAt ?? '').localeCompare(a.completedAt ?? ''))[0]
    const objective = open ?? newest
    if (objective) return { objectiveId: objective.objectiveId }
  }
  return null
}

const later = (a: string | null, b: string): string => (a !== null && a > b ? a : b)

/**
 * Applies pretend completions to a scoreboard so the overlay reacts through
 * the same path a real completion takes. Each nudge makes its objective
 * completed with a completion time no earlier than `at` (a later real
 * completion keeps its own time, so it still flashes), with counts and
 * score following when the objective was open; a nudge with no objective
 * only moves the row-level timestamp, which is what the overlay reads when
 * no game narrows it.
 */
export function applyFlashNudges(
  scoreboard: ScoreboardResponse,
  nudges: readonly FlashNudge[],
): ScoreboardResponse {
  if (nudges.length === 0) return scoreboard
  return {
    ...scoreboard,
    entries: scoreboard.entries.map((entry) => {
      const mine = nudges.filter((n) => n.userId === entry.userId)
      if (mine.length === 0) return entry
      let next = entry
      for (const nudge of mine) next = applyNudge(next, nudge)
      return next
    }),
  }
}

function applyNudge(entry: ScoreboardEntry, nudge: FlashNudge): ScoreboardEntry {
  const lastCompletedAt = later(entry.lastCompletedAt, nudge.at)
  if (nudge.objectiveId === null) return { ...entry, lastCompletedAt }

  let added: ObjectiveDetail | null = null
  const games: GameBreakdown[] = entry.games.map((game) => {
    const objective = game.objectives.find((o) => o.objectiveId === nudge.objectiveId)
    if (!objective) return game
    const wasCompleted = objective.isCompleted
    const stamped: ObjectiveDetail = {
      ...objective,
      isCompleted: true,
      completedAt: later(objective.completedAt, nudge.at),
      status: 'Completed',
    }
    if (!wasCompleted) added = objective
    return {
      ...game,
      objectives: game.objectives.map((o) => (o === objective ? stamped : o)),
      completedCount: game.completedCount + (wasCompleted ? 0 : 1),
      score: game.score + (wasCompleted ? 0 : objective.score),
    }
  })
  const addedScore = (added as ObjectiveDetail | null)?.score ?? 0
  return {
    ...entry,
    games,
    lastCompletedAt,
    completedCount: entry.completedCount + (added ? 1 : 0),
    totalScore: entry.totalScore + addedScore,
  }
}
