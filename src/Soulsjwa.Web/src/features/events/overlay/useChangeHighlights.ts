import type { ScoreboardEntry } from '../../../types'
import { trialGames, trialTotals } from '../scoreboard/scoreboardMetrics'
import { useFreshChanges } from './useFreshChanges'

/**
 * The two newest-completion timestamps a row can be showing, tracked
 * separately and never collapsed into one.
 *
 * Switching a single tracked value between the official and trial sources
 * made the flash lie in both directions: pausing or resuming a run changed
 * which source was read, so the value moved with no completion behind it and
 * the row flashed as if the competitor had just scored — reproducible on
 * every Stop and every Start — while a genuine official completion made
 * during a recording run changed only the untracked source and never flashed
 * at all. Both figures are on screen together, so both are watched.
 *
 * The trial side reads the display set (`trialGames`), not the recording one:
 * a pause must not drop the timestamp to null and have the resume "restore"
 * it, which is the same false positive by another route.
 */
const shownCompletions = (entry: ScoreboardEntry): readonly (string | null)[] => [
  entry.lastCompletedAt,
  trialTotals(trialGames(entry.games))?.lastCompletedAt ?? null,
]

const byCompetitor = (entry: ScoreboardEntry): string => entry.userId

/**
 * Track which entries have a "fresh" completion since the last render and
 * flash them via `data-highlight` for a configurable duration — see
 * `useFreshChanges` for the mechanics.
 */
export function useChangeHighlights(
  entries: ScoreboardEntry[],
  enabled: boolean,
  durationSeconds: number,
  /**
   * Identifies a row. Defaults to the competitor, which is right when each
   * competitor is on screen once; the games view shows the same competitor
   * once per game page with per-game timestamps, so it keys on both.
   */
  keyOf: (entry: ScoreboardEntry) => string = byCompetitor,
): Set<string> {
  return useFreshChanges(entries, keyOf, shownCompletions, enabled, durationSeconds)
}
