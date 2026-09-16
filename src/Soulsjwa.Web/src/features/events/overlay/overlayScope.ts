import type {
  GameBreakdown,
  ObjectiveOutcome,
  ScoreboardEntry,
  ScoreboardResponse,
  ObjectiveDetail,
} from '../../../types'
import { OBJECTIVE_OUTCOMES } from '../../../types'
import {
  gameLastCompletedAt,
  objectiveState,
  showsTrial,
  trialTotals,
} from '../scoreboard/scoreboardMetrics'
import type { OverlayConfig } from './overlayConfig'

const [PENDING_OUTCOME, COMPLETED_OUTCOME, FAILED_OUTCOME] = OBJECTIVE_OUTCOMES

const COMPLETED_OBJECTIVES_LABEL = 'Done:'
const COMPLETED_OBJECTIVE_MORE_SUFFIX = 'more'
const COMPLETED_OBJECTIVE_PREVIEW_LIMIT = 3

/** Fallback group label for objectives that have no category set. */
const UNCATEGORIZED_LABEL = 'Other'

/**
 * Completion outcome for a narrowed set of objectives. Shared by every place
 * that re-derives an entry over a subset of its games, so a per-game page and
 * a filtered scope always agree on what "finished" means.
 */
function outcomeFor(
  completedCount: number,
  failedCount: number,
  totalObjectives: number,
): { isFinished: boolean; status: ObjectiveOutcome } {
  const isFinished = totalObjectives > 0 && completedCount + failedCount === totalObjectives
  if (!isFinished) return { isFinished, status: PENDING_OUTCOME }
  return { isFinished, status: failedCount > 0 ? FAILED_OUTCOME : COMPLETED_OUTCOME }
}

/**
 * A scoreboard entry narrowed to one overlay's scope. `games` is deliberately
 * wider than the set the official figures come from — a trialed game stays in
 * view for its amber figures while contributing nothing to
 * `totalScore`/`completedCount`/`failedCount` — so consumers must not derive
 * an official denominator from it. The two fields below say what the official
 * figures were actually measured over.
 */
export interface ScopedEntry extends ScoreboardEntry {
  /** Ids of the games the official figures above were summed from. */
  scoringGameIds: readonly string[]
  /** Objective count across those games — the denominator for `completedCount`. */
  scoringTotalObjectives: number
  /** Points on offer across those games — the denominator for `totalScore`. */
  scoringMaxScore: number
}

const maxScoreOf = (games: readonly GameBreakdown[]): number =>
  games.reduce((sum, game) => sum + game.objectives.reduce((s, o) => s + o.score, 0), 0)

/**
 * One objective flattened together with its game and category. The
 * `objectives` overlay view paginates these, repeating the game/category
 * headers whenever they change (page boundaries included) so viewers always
 * know which group a row belongs to.
 */
export interface ObjectiveItem {
  gameId: string
  gameName: string
  category: string
  objective: ObjectiveDetail
  /** The game is reporting a trial run, so this row draws that attempt. */
  isTrial: boolean
}

/**
 * Flattens a competitor's games into an ordered list of objectives grouped by
 * game and then by category (Game → Category → Objective), preserving the
 * first-seen order of both games and categories so the layout is stable across
 * refreshes.
 */
export function buildObjectiveItems(entry: ScopedEntry): ObjectiveItem[] {
  const items: ObjectiveItem[] = []
  for (const game of entry.games) {
    const byCategory = new Map<string, ObjectiveDetail[]>()
    for (const objective of game.objectives) {
      const category = objective.category?.trim() || UNCATEGORIZED_LABEL
      const bucket = byCategory.get(category)
      if (bucket) bucket.push(objective)
      else byCategory.set(category, [objective])
    }
    for (const [category, objectives] of byCategory) {
      for (const objective of objectives) {
        items.push({
          gameId: game.eventGameId,
          gameName: game.gameName,
          category,
          objective,
          isTrial: showsTrial(game, entry.scoringGameIds),
        })
      }
    }
  }
  return items
}

export function chunkObjectiveItems(items: ObjectiveItem[], pageSize: number): ObjectiveItem[][] {
  if (items.length === 0) return [[]]
  const pages: ObjectiveItem[][] = []
  for (let i = 0; i < items.length; i += pageSize) {
    pages.push(items.slice(i, i + pageSize))
  }
  return pages
}

/**
 * Apply the URL-driven filters (games / players) to a single scoreboard
 * entry. The original `ScoreboardResponse` is computed server-side against
 * the entire event, so we re-aggregate per visible scope here.
 */
export function filterEntry(entry: ScoreboardEntry, config: OverlayConfig): ScopedEntry {
  // With no explicit gameIds pin, default to the event's single active game
  // rather than every game ever played — the same rule the in-app scoreboard
  // follows, so the two never disagree about what "the" game is. An explicit
  // URL pin still gets exactly what it asked for.
  const gameIds = config.gameIds ? new Set(config.gameIds) : null
  const scoringGames: GameBreakdown[] = gameIds
    ? entry.games.filter((g) => gameIds.has(g.eventGameId))
    : entry.games.filter((g) => g.isEnabled)

  // Visibility and scoring are deliberately separate sets. A trial is not
  // restricted to the event's active game, so a trialing game has to stay
  // visible for its trial figures to render at all — but it must never join
  // the official totals below, which feed this overlay's own re-ranking. A
  // game that was enabled earlier can still hold official completions, so
  // folding one in would move ranks.
  const scoringIds = new Set(scoringGames.map((g) => g.eventGameId))
  const visibleGames = entry.games.filter((g) => scoringIds.has(g.eventGameId) || g.trial !== null)

  const scoringGameIds = scoringGames.map((g) => g.eventGameId)
  const scoringTotalObjectives = scoringGames.reduce((sum, game) => sum + game.totalObjectives, 0)
  const scoringMaxScore = maxScoreOf(scoringGames)

  // Keyed on the scoring set, not the visible one: when a trialing game is
  // forced into view the two differ, and short-circuiting here would leave
  // the server's all-games totals in place — counting official score from a
  // game this overlay is not scoring.
  if (scoringGames.length === entry.games.length)
    return { ...entry, scoringGameIds, scoringTotalObjectives, scoringMaxScore }

  let totalScore = 0
  let completedCount = 0
  let failedCount = 0
  let lastCompletedAt: string | null = null
  for (const g of scoringGames) {
    totalScore += g.score
    completedCount += g.completedCount
    failedCount += g.failedCount
    for (const o of g.objectives) {
      if (o.completedAt && (!lastCompletedAt || o.completedAt > lastCompletedAt)) {
        lastCompletedAt = o.completedAt
      }
    }
  }
  const { isFinished, status } = outcomeFor(completedCount, failedCount, scoringTotalObjectives)

  // We intentionally do not recompute `totalInGameTimeMs` from a filtered
  // subset of games — the server tracks it at event scope only.
  return {
    ...entry,
    games: visibleGames,
    scoringGameIds,
    scoringTotalObjectives,
    scoringMaxScore,
    totalScore,
    completedCount,
    failedCount,
    isFinished,
    status,
    lastCompletedAt,
  }
}

export interface VisibleScope {
  entries: ScopedEntry[]
}

/** The completion figures a row (or the objectives view's header) draws for an entry. */
export interface EntryProgress {
  completedCount: number
  failedCount: number
  totalObjectives: number
  /** 0–100, rounded. */
  percent: number
  /** True when the figures are a trial's rather than the official record's. */
  isTrial: boolean
}

/**
 * Numerator and denominator have to come from the same scope. Games whose
 * marks the row draws as the trial's (see `showsTrial`) supply the trial's
 * figures over those games' objectives; otherwise the official figures are
 * measured over `scoringTotalObjectives` — never a sum over `entry.games`,
 * which includes trialed games that contribute nothing to `completedCount`
 * and collapsed the percentage whenever a trial was forced into view.
 */
export function entryProgress(entry: ScopedEntry): EntryProgress {
  const substituted = entry.games.filter((game) => showsTrial(game, entry.scoringGameIds))
  const substitutedTotals = trialTotals(substituted)
  const totalObjectives = substitutedTotals
    ? substituted.reduce((s, g) => s + g.totalObjectives, 0)
    : entry.scoringTotalObjectives
  const completedCount = substitutedTotals ? substitutedTotals.completedCount : entry.completedCount
  const failedCount = substitutedTotals ? substitutedTotals.failedCount : entry.failedCount
  return {
    completedCount,
    failedCount,
    totalObjectives,
    percent: totalObjectives > 0 ? Math.round((completedCount / totalObjectives) * 100) : 0,
    isTrial: substitutedTotals !== null,
  }
}

export function applyFilters(data: ScoreboardResponse, config: OverlayConfig): VisibleScope {
  const playerIds = config.playerIds ? new Set(config.playerIds) : null

  // Filter by player first so we don't run `filterEntry` over rows we'd just
  // throw away — `filterEntry` allocates per entry and most overlays will
  // either include everyone or pin to a small handful of players.
  const sourceEntries = playerIds
    ? data.entries.filter((e) => playerIds.has(e.userId))
    : data.entries
  let entries = sourceEntries.map((e) => filterEntry(e, config))

  // Re-rank within the visible scope so "1/2/3" make sense to viewers.
  entries.sort((a, b) => b.totalScore - a.totalScore || a.displayName.localeCompare(b.displayName))
  entries = entries.map((e, i) => ({ ...e, rank: i + 1 }))

  return { entries }
}

/** One page per visible game, each page containing every competitor's row for that game. */
export function buildGamePages(entries: ScopedEntry[]): ScopedEntry[][] {
  const gameIds = new Set<string>()
  const orderedGameIds: string[] = []
  for (const e of entries) {
    for (const g of e.games) {
      if (!gameIds.has(g.eventGameId)) {
        gameIds.add(g.eventGameId)
        orderedGameIds.push(g.eventGameId)
      }
    }
  }
  return orderedGameIds.map((gid) =>
    entries
      .map((e) => {
        const game = e.games.find((g) => g.eventGameId === gid)
        if (!game) return null
        // Every figure the row reads has to be narrowed to this game, not
        // just the score: a spread-through failedCount showed the event-wide
        // total, and a spread-through lastCompletedAt flashed the row here
        // when the competitor completed something in a different game.
        return {
          ...e,
          games: [game],
          totalScore: game.score,
          completedCount: game.completedCount,
          failedCount: game.failedCount,
          lastCompletedAt: gameLastCompletedAt(game),
          // This page reports one game, so that game alone is the scope its
          // official figures were measured over.
          scoringGameIds: e.scoringGameIds.includes(gid) ? [gid] : [],
          scoringTotalObjectives: e.scoringGameIds.includes(gid) ? game.totalObjectives : 0,
          scoringMaxScore: e.scoringGameIds.includes(gid) ? maxScoreOf([game]) : 0,
          ...outcomeFor(game.completedCount, game.failedCount, game.totalObjectives),
        } as ScopedEntry
      })
      .filter((x): x is ScopedEntry => x != null),
  )
}

/** Chunk entries into pages of at most `pageSize` rows, for the `scores` view. */
export function chunkEntries(entries: ScopedEntry[], pageSize: number): ScopedEntry[][] {
  const out: ScopedEntry[][] = []
  for (let i = 0; i < entries.length; i += pageSize) {
    out.push(entries.slice(i, i + pageSize))
  }
  return out.length === 0 ? [[]] : out
}

export function getCompletedObjectiveNames(entry: ScopedEntry): string[] {
  return entry.games.flatMap((game) =>
    game.objectives
      // Matches the marks the objectives view draws for this game, so the
      // ticker and the list never name a different set of completions — and
      // in particular never names an official completion from a game whose
      // points are not in the score beside it.
      .filter(
        (objective) =>
          objectiveState(objective, showsTrial(game, entry.scoringGameIds)).isCompleted,
      )
      .map((objective) => objective.name),
  )
}

export function formatCompletedObjectives(names: string[]): string | null {
  if (names.length === 0) return null

  const visibleNames = names.slice(0, COMPLETED_OBJECTIVE_PREVIEW_LIMIT)
  const hiddenCount = names.length - visibleNames.length
  const suffix = hiddenCount > 0 ? ` (+${hiddenCount} ${COMPLETED_OBJECTIVE_MORE_SUFFIX})` : ''
  return `${COMPLETED_OBJECTIVES_LABEL} ${visibleNames.join(' • ')}${suffix}`
}
