import { describe, expect, it } from 'vitest'
import {
  SAMPLE_COMPETITORS,
  SAMPLE_OBJECTIVES,
  buildSampleScoreboard,
  sampleAdvances,
  type SampleGame,
} from './overlaySampleData'

const NOW = new Date('2026-09-15T12:00:00.000Z')

const bareGame: SampleGame = {
  eventGameId: 'game-1',
  gameName: 'Dark Souls',
  isEnabled: true,
  objectives: [],
}

const definedGame: SampleGame = {
  eventGameId: 'game-2',
  gameName: 'Elden Ring',
  isEnabled: true,
  objectives: [
    { id: 'o1', name: 'Defeat Margit', score: 10, category: 'Limgrave' },
    { id: 'o2', name: 'Defeat Godrick', score: 20, category: 'Limgrave' },
  ],
}

describe('buildSampleScoreboard', () => {
  it('uses the placeholder objectives for a game that has none, and the real ones otherwise', () => {
    const board = buildSampleScoreboard([bareGame, definedGame], 0, NOW)
    const [first] = board.entries

    const [bare, defined] = first!.games
    expect(bare!.objectives.map((o) => o.name)).toEqual(SAMPLE_OBJECTIVES.map((o) => o.name))
    expect(defined!.objectives.map((o) => o.name)).toEqual(['Defeat Margit', 'Defeat Godrick'])
    expect(defined!.totalObjectives).toBe(2)
  })

  it('names every sample competitor once, ranked by score with consistent totals', () => {
    const board = buildSampleScoreboard([bareGame], 0, NOW)

    expect(board.entries.map((e) => e.displayName).sort()).toEqual(
      SAMPLE_COMPETITORS.map((c) => c.displayName).sort(),
    )
    expect(board.entries.map((e) => e.rank)).toEqual([1, 2, 3, 4])
    for (let i = 1; i < board.entries.length; i++) {
      expect(board.entries[i - 1]!.totalScore).toBeGreaterThanOrEqual(board.entries[i]!.totalScore)
    }
    for (const entry of board.entries) {
      const game = entry.games[0]!
      const completed = game.objectives.filter((o) => o.isCompleted)
      expect(entry.completedCount).toBe(completed.length)
      expect(entry.totalScore).toBe(completed.reduce((sum, o) => sum + o.score, 0))
      // The overlay flashes a row off this timestamp, so it has to track the objectives.
      expect(entry.lastCompletedAt).toBe(
        completed.length > 0
          ? completed
              .map((o) => o.completedAt)
              .sort()
              .at(-1)
          : null,
      )
    }
  })

  it('is deterministic for a tick, and each tick moves exactly one competitor in turn', () => {
    const again = buildSampleScoreboard([bareGame], 3, NOW)
    expect(buildSampleScoreboard([bareGame], 3, NOW)).toEqual(again)

    const countFor = (tick: number, userId: string) =>
      buildSampleScoreboard([bareGame], tick, NOW).entries.find((e) => e.userId === userId)!
        .completedCount
    for (let tick = 0; tick < SAMPLE_COMPETITORS.length * 2; tick++) {
      const moved = SAMPLE_COMPETITORS.filter(
        (c) => countFor(tick + 1, c.userId) !== countFor(tick, c.userId),
      )
      expect(moved).toHaveLength(1)
      expect(moved[0]).toBe(SAMPLE_COMPETITORS[tick % SAMPLE_COMPETITORS.length])
      const was = countFor(tick, moved[0]!.userId)
      const now = countFor(tick + 1, moved[0]!.userId)
      // One more, or wrapped back to zero once the run was complete.
      expect(now === was + 1 || (was === SAMPLE_OBJECTIVES.length && now === 0)).toBe(true)
    }
  })

  it('deals advances round-robin', () => {
    const n = SAMPLE_COMPETITORS.length
    expect(SAMPLE_COMPETITORS.map((_, i) => sampleAdvances(i, 0))).toEqual(Array(n).fill(0))
    expect(SAMPLE_COMPETITORS.map((_, i) => sampleAdvances(i, 1))).toEqual([
      1,
      ...Array(n - 1).fill(0),
    ])
    expect(SAMPLE_COMPETITORS.map((_, i) => sampleAdvances(i, n))).toEqual(Array(n).fill(1))
  })

  it('moves only the advancing row’s newest completion, so only that row flashes', () => {
    const before = buildSampleScoreboard([bareGame], 0, NOW)
    const after = buildSampleScoreboard([bareGame], 1, NOW)
    const lastFor = (board: typeof before, userId: string) =>
      board.entries.find((e) => e.userId === userId)!.lastCompletedAt
    const [mover, ...others] = SAMPLE_COMPETITORS
    expect(lastFor(after, mover!.userId)).not.toBe(lastFor(before, mover!.userId))
    expect(new Date(lastFor(after, mover!.userId)!).getTime()).toBeGreaterThan(
      new Date(lastFor(before, mover!.userId)!).getTime(),
    )
    for (const other of others) {
      expect(lastFor(after, other.userId)).toBe(lastFor(before, other.userId))
    }
  })

  it('only counts enabled games toward the totals, as the live scoreboard does', () => {
    const past: SampleGame = { ...definedGame, eventGameId: 'past', isEnabled: false }
    const board = buildSampleScoreboard([past, bareGame], 0, NOW)

    for (const entry of board.entries) {
      const active = entry.games.find((g) => g.eventGameId === bareGame.eventGameId)!
      expect(entry.totalScore).toBe(active.score)
      expect(entry.completedCount).toBe(active.completedCount)
    }
  })
})
