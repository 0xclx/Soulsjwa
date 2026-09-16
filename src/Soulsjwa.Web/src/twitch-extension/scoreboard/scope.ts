import type {
  TwitchExtensionEntry,
  TwitchExtensionEntryGame,
  TwitchExtensionGame,
  TwitchExtensionScoreboard,
  TwitchExtensionSettings,
} from '../../types/twitchExtension'

/**
 * What the board is narrowed to. `all` is the event's official ranking as the
 * server computed it; the other two re-derive every figure from one game and
 * re-rank within it, the same rule the OBS overlay's games view follows.
 */
export type BoardScope =
  { kind: 'all' } | { kind: 'active' } | { kind: 'game'; eventGameId: string }

export const ALL_SCOPE: BoardScope = { kind: 'all' }
export const ACTIVE_SCOPE: BoardScope = { kind: 'active' }

/** One rendered row: every figure already narrowed to the scope. */
export interface BoardRow {
  userId: string
  displayName: string
  twitchLogin: string
  profileImageUrl: string | null
  isLive: boolean
  rank: number
  score: number
  completedCount: number
  failedCount: number
  totalObjectives: number
  isFinished: boolean
  /** Newest completion within the scope — what the change flash watches. */
  lastCompletedAt: string | null
  /** Trial figures within the scope, or null when no trial is on screen. */
  trialScore: number | null
  trialLastCompletedAt: string | null
  isChannelCompetitor: boolean
}

export interface BoardView {
  scope: BoardScope
  /** The game the rows are narrowed to, or null for `all`. */
  game: TwitchExtensionGame | null
  rows: BoardRow[]
}

/** The scope a channel's saved settings open on, given the games the event actually has. */
export function initialScope(
  settings: TwitchExtensionSettings,
  games: TwitchExtensionGame[],
): BoardScope {
  switch (settings.defaultScope) {
    case 'ActiveGame':
      return ACTIVE_SCOPE
    case 'PinnedGame':
      return settings.pinnedEventGameId &&
        games.some((g) => g.eventGameId === settings.pinnedEventGameId)
        ? { kind: 'game', eventGameId: settings.pinnedEventGameId }
        : ACTIVE_SCOPE
    default:
      return ALL_SCOPE
  }
}

export const sameScope = (a: BoardScope, b: BoardScope): boolean =>
  a.kind === b.kind && (a.kind !== 'game' || b.kind !== 'game' || a.eventGameId === b.eventGameId)

/** Resolves `active` to the event's enabled game; null when the scope names no single game. */
export function scopedGame(
  scope: BoardScope,
  board: TwitchExtensionScoreboard,
): TwitchExtensionGame | null {
  if (scope.kind === 'all') return null
  const id = scope.kind === 'active' ? board.event?.activeEventGameId : scope.eventGameId
  return board.games.find((g) => g.eventGameId === id) ?? null
}

const trialTotal = (games: readonly TwitchExtensionEntryGame[]): number | null => {
  const withTrial = games.filter((g) => g.trial !== null)
  if (withTrial.length === 0) return null
  return withTrial.reduce((sum, g) => sum + (g.trial?.score ?? 0), 0)
}

const latest = (timestamps: ReadonlyArray<string | null | undefined>): string | null => {
  let result: string | null = null
  for (const at of timestamps) if (at && (result === null || at > result)) result = at
  return result
}

const allGamesRow = (
  entry: TwitchExtensionEntry,
  totalObjectives: number,
  channelCompetitorUserId: string | null,
  showTrial: boolean,
): BoardRow => ({
  userId: entry.userId,
  displayName: entry.displayName,
  twitchLogin: entry.twitchLogin,
  profileImageUrl: entry.profileImageUrl,
  isLive: entry.isLive,
  rank: entry.rank,
  score: entry.totalScore,
  completedCount: entry.completedCount,
  failedCount: entry.failedCount,
  totalObjectives,
  isFinished: entry.isFinished,
  lastCompletedAt: entry.lastCompletedAt,
  trialScore: showTrial ? trialTotal(entry.games) : null,
  trialLastCompletedAt: showTrial ? latest(entry.games.map((g) => g.trial?.lastCompletedAt)) : null,
  isChannelCompetitor: entry.userId === channelCompetitorUserId,
})

const gameRow = (
  entry: TwitchExtensionEntry,
  game: TwitchExtensionGame,
  channelCompetitorUserId: string | null,
  showTrial: boolean,
): BoardRow => {
  const figures = entry.games.find((g) => g.eventGameId === game.eventGameId)
  const completed = figures?.completedCount ?? 0
  const failed = figures?.failedCount ?? 0
  return {
    userId: entry.userId,
    displayName: entry.displayName,
    twitchLogin: entry.twitchLogin,
    profileImageUrl: entry.profileImageUrl,
    isLive: entry.isLive,
    rank: 0,
    score: figures?.score ?? 0,
    completedCount: completed,
    failedCount: failed,
    totalObjectives: game.totalObjectives,
    isFinished: game.totalObjectives > 0 && completed + failed === game.totalObjectives,
    lastCompletedAt: figures?.lastCompletedAt ?? null,
    trialScore: showTrial && figures?.trial ? figures.trial.score : null,
    trialLastCompletedAt: showTrial && figures?.trial ? figures.trial.lastCompletedAt : null,
    isChannelCompetitor: entry.userId === channelCompetitorUserId,
  }
}

/**
 * Ranks rows narrowed to one game: score first, then the earlier newest
 * completion (first to reach the score), then name for stability. Equal
 * scores share a place under `SharedPlace`, the event's default, and are
 * split by time under `ByTime`, so the per-game ranking follows the same
 * mode the event's official ranking does.
 */
function rankRows(rows: BoardRow[], byTime: boolean): BoardRow[] {
  const sorted = [...rows].sort(
    (a, b) =>
      b.score - a.score ||
      (a.lastCompletedAt ?? '￿').localeCompare(b.lastCompletedAt ?? '￿') ||
      a.displayName.localeCompare(b.displayName),
  )
  let place = 0
  let previousScore: number | null = null
  return sorted.map((row, index) => {
    if (byTime || row.score !== previousScore) place = index + 1
    previousScore = row.score
    return { ...row, rank: place }
  })
}

/** Narrows the server's board to a scope. Pure, so the tests cover every rule here. */
export function buildBoardView(board: TwitchExtensionScoreboard, scope: BoardScope): BoardView {
  const showTrial = board.settings.showTrialProgress
  const highlightId = board.settings.highlightChannelCompetitor
    ? board.channelCompetitorUserId
    : null

  if (scope.kind === 'all') {
    const totalObjectives = board.games.reduce((sum, g) => sum + g.totalObjectives, 0)
    return {
      scope,
      game: null,
      rows: board.entries.map((e) => allGamesRow(e, totalObjectives, highlightId, showTrial)),
    }
  }

  const game = scopedGame(scope, board)
  if (!game) return { scope, game: null, rows: [] }

  const byTime = board.event?.tieBreakMode === 'ByTime'
  return {
    scope,
    game,
    rows: rankRows(
      board.entries.map((e) => gameRow(e, game, highlightId, showTrial)),
      byTime,
    ),
  }
}
