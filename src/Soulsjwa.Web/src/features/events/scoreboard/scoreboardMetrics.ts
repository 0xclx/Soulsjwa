import type {
  GameBreakdown,
  ObjectiveDetail,
  ScoreboardEntry,
  TrialObjectiveState,
} from '../../../types'

/**
 * The event's single active game, or undefined before an owner has enabled
 * one. At most one game is ever enabled per event (DB-enforced).
 */
export const activeGame = (games: readonly GameBreakdown[]): GameBreakdown | undefined =>
  games.find((game) => game.isEnabled)

/**
 * Two questions that must not be conflated. `trialGames` — a run was started,
 * paused included — decides whether to DISPLAY the amber figures; a stopped
 * run keeps what it earned. `recordingGames` — still recording — is the only
 * thing that decides whether the trial's marks SUBSTITUTE for the official
 * ones, since a paused run hands the competitor back to the official record.
 */
export const trialGames = (games: readonly GameBreakdown[]): GameBreakdown[] =>
  games.filter((game) => game.trial !== null)

export const recordingGames = (games: readonly GameBreakdown[]): GameBreakdown[] =>
  games.filter((game) => game.isTrialActive)

/**
 * What a recording trial reports for an objective it carries no row for. A
 * guard rather than a real case (the server sends trial state for every
 * objective of a trialed game), but it must never fall back to the official
 * mark — that would tick something not done in *this* run.
 */
const TRIAL_UNTOUCHED: TrialObjectiveState = {
  isCompleted: false,
  completedAt: null,
  isFailed: false,
  failedAt: null,
  status: 'Pending',
}

/**
 * The outcome an objective row should draw: the recording trial's attempt, or
 * the official record. `showTrial` must come from the game's `isTrialActive`,
 * never from `trial !== null` — a paused run hands the marks back to the
 * official record. See `showsTrial`.
 */
export const objectiveState = (
  objective: ObjectiveDetail,
  showTrial: boolean,
): TrialObjectiveState => (showTrial ? (objective.trial ?? TRIAL_UNTOUCHED) : objective)

/**
 * The single rule behind every set of marks, counts and timestamps on both
 * the in-app scoreboard and the OBS overlay. Two cases, and they differ:
 *
 * - A **scored** game substitutes only while its run records; once paused the
 *   figures beside it are official again, so pinning it to stale practice
 *   marks would hide real progress.
 * - A game in view **only because it carries a trial** always shows that run,
 *   paused or not — nothing official about it is being reported, so its
 *   official marks would be ticks with no points behind them.
 *
 * `scoringGameIds` is therefore the caller's scope, not the competitor's
 * whole game list: the overlay's `games=` pin, or the one active game in-app.
 */
export const showsTrial = (game: GameBreakdown, scoringGameIds: readonly string[]): boolean =>
  scoringGameIds.includes(game.eventGameId) ? game.isTrialActive : game.trial !== null

export interface TrialTotals {
  score: number
  completedCount: number
  failedCount: number
  lastCompletedAt: string | null
}

/**
 * Trial figures summed across the games passed in — the ones actually being
 * displayed, not the competitor's full set, so the total always matches what
 * the viewer can see. (This is why the server reports trial progress per game
 * and never per competitor.) Null when none of them has a trial.
 */
export const trialTotals = (games: readonly GameBreakdown[]): TrialTotals | null => {
  const withTrial = trialGames(games)
  if (withTrial.length === 0) return null

  let lastCompletedAt: string | null = null
  for (const game of withTrial) {
    const at = game.trial?.lastCompletedAt
    if (at && (lastCompletedAt === null || at > lastCompletedAt)) lastCompletedAt = at
  }

  return {
    score: withTrial.reduce((total, game) => total + (game.trial?.score ?? 0), 0),
    completedCount: withTrial.reduce((total, game) => total + (game.trial?.completedCount ?? 0), 0),
    failedCount: withTrial.reduce((total, game) => total + (game.trial?.failedCount ?? 0), 0),
    lastCompletedAt,
  }
}

export interface ScoreboardEntryView {
  /** The event's active game among this competitor's games, if any. */
  activeGame: GameBreakdown | undefined
  /**
   * Where this view's official figures come from — the active game alone.
   * Pass to `showsTrial`; a trialed game outside this set is in view for its
   * trial only.
   */
  scoringGameIds: readonly string[]
  /** Games with a started run, paused included — what the badge reports on. */
  trialing: GameBreakdown[]
  /** Trial figures summed over `trialing`, or null when there is no trial. */
  trials: TrialTotals | null
  /**
   * The games whose breakdown the entry renders. A trial is not restricted to
   * the active game, so a trialed game has to be appended — otherwise an amber
   * score shows on the row with no way to expand the objectives behind it.
   */
  breakdownGames: GameBreakdown[]
}

/**
 * Shared by the desktop row and the mobile card so the two can't disagree
 * about which games they show or which figures they badge.
 */
export const entryView = (games: readonly GameBreakdown[]): ScoreboardEntryView => {
  const active = activeGame(games)
  const trialing = trialGames(games)
  return {
    activeGame: active,
    scoringGameIds: active ? [active.eventGameId] : [],
    trialing,
    trials: trialTotals(trialing),
    breakdownGames: [
      ...(active ? [active] : []),
      ...trialing.filter((game) => game.eventGameId !== active?.eventGameId),
    ],
  }
}

/**
 * Most recent completion within one game, derived from its objectives — the
 * "last completed" column in the expanded breakdown. Null when nothing is done.
 */
export const gameLastCompletedAt = (game: Pick<GameBreakdown, 'objectives'>): string | null => {
  let latest: string | null = null
  for (const objective of game.objectives) {
    if (objective.completedAt && (latest === null || objective.completedAt > latest)) {
      latest = objective.completedAt
    }
  }
  return latest
}

/**
 * A competitor has nothing left to play in a game once every objective is
 * either completed or failed. A game without objectives is never done.
 */
export const gameIsDone = (
  game: Pick<GameBreakdown, 'completedCount' | 'failedCount' | 'totalObjectives'>,
): boolean =>
  game.totalObjectives > 0 && game.completedCount + game.failedCount >= game.totalObjectives

/**
 * Whether anyone has an official result in a game yet. Before that, every
 * per-game rank is a tie, so `SharedPlace` would label everyone `#1`. Trial
 * progress does not count: it never reaches a rank.
 */
export const gameHasActivity = (
  entries: readonly Pick<ScoreboardEntry, 'games'>[],
  eventGameId: string,
): boolean =>
  entries.some((entry) =>
    entry.games.some(
      (game) =>
        game.eventGameId === eventGameId && (game.completedCount > 0 || game.failedCount > 0),
    ),
  )

export interface ActiveGameRow {
  entry: ScoreboardEntry
  /** The entry's breakdown for the game being played. */
  game: GameBreakdown
}

/**
 * The current-game standings: each entry paired with its breakdown for
 * `eventGameId`, ordered by the server's per-game `rank`. The client never
 * computes a rank; ties keep the server's entry order (the sort is stable).
 * Entries without a breakdown for the game are skipped.
 */
export const activeGameRows = (
  entries: readonly ScoreboardEntry[],
  eventGameId: string,
): ActiveGameRow[] =>
  entries
    .flatMap((entry) => {
      const game = entry.games.find((g) => g.eventGameId === eventGameId)
      return game ? [{ entry, game }] : []
    })
    .sort((a, b) => a.game.rank - b.game.rank)

/**
 * Case-insensitive substring match on display name or Twitch login. A blank
 * query matches everyone. Filtering only hides rows; the shown rank stays the
 * server's.
 */
export const matchesCompetitorSearch = (
  entry: Pick<ScoreboardEntry, 'displayName' | 'twitchLogin'>,
  query: string,
): boolean => {
  const needle = query.trim().toLocaleLowerCase()
  return (
    needle === '' ||
    entry.displayName.toLocaleLowerCase().includes(needle) ||
    entry.twitchLogin.toLocaleLowerCase().includes(needle)
  )
}

/** Accessible name of a game progress bar, e.g. "7 of 12 objectives". */
export const progressLabel = (completed: number, total: number): string =>
  `${completed} of ${total} objectives`

/** Progress bar value, 0–100. A game without objectives reads as 0. */
export const progressPercent = (completed: number, total: number): number =>
  total > 0 ? Math.round((completed / total) * 100) : 0

/** Fallback group label for objectives that have no category set. */
const UNCATEGORIZED_LABEL = 'Other'

/**
 * Groups a game's objectives by category, preserving the first-seen order of
 * both the categories and the objectives within each — same grouping as the
 * OBS overlay's "objectives" view, so the in-app breakdown and the overlay
 * never disagree about how objectives are organized.
 */
export const groupByCategory = (
  objectives: readonly ObjectiveDetail[],
): Array<[string, ObjectiveDetail[]]> => {
  const byCategory = new Map<string, ObjectiveDetail[]>()
  for (const objective of objectives) {
    const category = objective.category?.trim() || UNCATEGORIZED_LABEL
    const bucket = byCategory.get(category)
    if (bucket) bucket.push(objective)
    else byCategory.set(category, [objective])
  }
  return Array.from(byCategory)
}
