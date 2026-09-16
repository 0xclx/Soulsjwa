import { describe, expect, it } from 'vitest'
import { applyFlashNudges, pickNudgeTarget } from './overlayFlashNudge'
import { buildSampleScoreboard, SAMPLE_COMPETITORS } from './overlaySampleData'
import type { ScoreboardResponse } from '../../../types'

const NOW = new Date('2026-09-15T12:00:00.000Z')
const AT = '2026-09-15T12:30:00.000Z'
const board = () =>
  buildSampleScoreboard(
    [{ eventGameId: 'g1', gameName: 'Dark Souls', isEnabled: true, objectives: [] }],
    0,
    NOW,
  )

describe('applyFlashNudges', () => {
  it('returns the same board when there is nothing to nudge', () => {
    const b = board()
    expect(applyFlashNudges(b, [])).toBe(b)
  })

  it('picks the next open objective, or the newest completed one once all are done', () => {
    const b = board()
    const partway = b.entries.find((e) => e.completedCount > 0 && e.completedCount < 6)!
    expect(pickNudgeTarget(partway)!.objectiveId).toBe(
      partway.games[0]!.objectives.find((o) => !o.isCompleted)!.objectiveId,
    )
    const bare = { ...partway, games: [] }
    expect(pickNudgeTarget(bare)).toBeNull()
  })

  it('completes the nudged competitor’s chosen objective, counts and score included', () => {
    const b = board()
    const target = b.entries.find((e) => e.completedCount > 0 && e.completedCount < 6)!
    const nextOpen = target.games[0]!.objectives.find((o) => !o.isCompleted)!

    const nudged = applyFlashNudges(b, [
      { userId: target.userId, objectiveId: nextOpen.objectiveId, at: AT },
    ])

    const after = nudged.entries.find((e) => e.userId === target.userId)!
    const stamped = after.games[0]!.objectives.find((o) => o.objectiveId === nextOpen.objectiveId)!
    expect(stamped.isCompleted).toBe(true)
    expect(stamped.completedAt).toBe(AT)
    expect(after.lastCompletedAt).toBe(AT)
    expect(after.completedCount).toBe(target.completedCount + 1)
    expect(after.totalScore).toBe(target.totalScore + nextOpen.score)
    // Everyone else is untouched, by reference.
    for (const entry of nudged.entries) {
      if (entry.userId !== target.userId)
        expect(entry).toBe(b.entries.find((e) => e.userId === entry.userId))
    }
  })

  it('re-stamps the newest completion once every objective is done', () => {
    const b = board()
    const source = b.entries[0]!
    const allDone: ScoreboardResponse = {
      ...b,
      entries: b.entries.map((e) =>
        e.userId === source.userId
          ? {
              ...e,
              games: e.games.map((g) => ({
                ...g,
                completedCount: g.objectives.length,
                objectives: g.objectives.map((o, i) => ({
                  ...o,
                  isCompleted: true,
                  status: 'Completed' as const,
                  completedAt: `2026-09-15T0${i}:00:00.000Z`,
                })),
              })),
            }
          : e,
      ),
    }

    const done = allDone.entries.find((e) => e.userId === source.userId)!
    const objectiveId = pickNudgeTarget(done)!.objectiveId
    expect(objectiveId).toBe(done.games[0]!.objectives.at(-1)!.objectiveId)

    const after = applyFlashNudges(allDone, [
      { userId: source.userId, objectiveId, at: AT },
    ]).entries.find((e) => e.userId === source.userId)!

    const objectives = after.games[0]!.objectives
    expect(objectives.at(-1)!.completedAt).toBe(AT)
    expect(objectives.filter((o) => o.completedAt === AT)).toHaveLength(1)
    expect(after.completedCount).toBe(done.completedCount)
  })

  it('stays on its objective when the board later completes it for real, and never hides a newer completion', () => {
    const b = board()
    const target = b.entries.find((e) => e.completedCount > 0 && e.completedCount < 6)!
    const chosen = target.games[0]!.objectives.find((o) => !o.isCompleted)!
    const nudge = { userId: target.userId, objectiveId: chosen.objectiveId, at: AT }
    const nudgedBefore = applyFlashNudges(b, [nudge]).entries.find(
      (e) => e.userId === target.userId,
    )!

    // The board catches up: the same objective is now really completed, at a later time.
    const LATER = '2026-09-15T13:00:00.000Z'
    const caughtUp: ScoreboardResponse = {
      ...b,
      entries: b.entries.map((e) =>
        e.userId === target.userId
          ? {
              ...e,
              completedCount: e.completedCount + 1,
              totalScore: e.totalScore + chosen.score,
              lastCompletedAt: LATER,
              games: e.games.map((g) => ({
                ...g,
                completedCount: g.completedCount + 1,
                score: g.score + chosen.score,
                objectives: g.objectives.map((o) =>
                  o.objectiveId === chosen.objectiveId
                    ? { ...o, isCompleted: true, completedAt: LATER, status: 'Completed' as const }
                    : o,
                ),
              })),
            }
          : e,
      ),
    }
    const nudgedAfter = applyFlashNudges(caughtUp, [nudge]).entries.find(
      (e) => e.userId === target.userId,
    )!

    // Nothing else got completed by the nudge moving on…
    expect(nudgedAfter.completedCount).toBe(nudgedBefore.completedCount)
    expect(
      nudgedAfter.games[0]!.objectives.filter((o) => o.isCompleted).map((o) => o.objectiveId),
    ).toEqual(
      nudgedBefore.games[0]!.objectives.filter((o) => o.isCompleted).map((o) => o.objectiveId),
    )
    // …and the real, later time wins over the pretend one.
    expect(
      nudgedAfter.games[0]!.objectives.find((o) => o.objectiveId === chosen.objectiveId)!
        .completedAt,
    ).toBe(LATER)
    expect(nudgedAfter.lastCompletedAt).toBe(LATER)
  })

  it('completes the first objective, counts included, for a competitor with none done', () => {
    const b = board()
    const target = b.entries.find((e) => e.completedCount === 0)
    // The last sample competitor starts furthest back; make sure one has nothing done.
    const fresh: ScoreboardResponse = target
      ? b
      : {
          ...b,
          entries: b.entries.map((e, i) =>
            i === 0
              ? {
                  ...e,
                  completedCount: 0,
                  totalScore: 0,
                  lastCompletedAt: null,
                  games: e.games.map((g) => ({
                    ...g,
                    completedCount: 0,
                    score: 0,
                    objectives: g.objectives.map((o) => ({
                      ...o,
                      isCompleted: false,
                      completedAt: null,
                      status: 'Pending' as const,
                    })),
                  })),
                }
              : e,
          ),
        }
    const userId = (target ?? fresh.entries[0]!).userId

    const objectiveId = pickNudgeTarget(
      fresh.entries.find((e) => e.userId === userId)!,
    )!.objectiveId
    const after = applyFlashNudges(fresh, [{ userId, objectiveId, at: AT }]).entries.find(
      (e) => e.userId === userId,
    )!

    const first = after.games[0]!.objectives[0]!
    expect(first.isCompleted).toBe(true)
    expect(first.completedAt).toBe(AT)
    expect(after.completedCount).toBe(1)
    expect(after.games[0]!.completedCount).toBe(1)
    expect(after.totalScore).toBe(first.score)
    expect(after.lastCompletedAt).toBe(AT)
  })

  it('falls back to the row-level timestamp for a competitor with no objectives', () => {
    const b = board()
    const bare: ScoreboardResponse = {
      ...b,
      entries: b.entries.map((e) => ({ ...e, games: [] })),
    }
    const userId = SAMPLE_COMPETITORS[0]!.userId

    const after = applyFlashNudges(bare, [{ userId, objectiveId: null, at: AT }]).entries.find(
      (e) => e.userId === userId,
    )!

    expect(after.lastCompletedAt).toBe(AT)
    expect(after.games).toEqual([])
  })
})
