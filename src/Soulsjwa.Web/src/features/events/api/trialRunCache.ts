import type { QueryClient } from '@tanstack/react-query'
import { MY_EVENTS_QUERY_KEYS } from '../../myEvents/api/myEventsApi'
import { MY_TRIAL_RUNS_QUERY_KEYS } from '../../myEvents/api/myTrialRunsApi'
import { invalidateEventScope } from './eventCache'

/**
 * A trial's own progress changed — an objective was ticked or failed inside a
 * run. The trial list (which shows each run's score and count), the selected
 * run's objectives, and the scoreboard/overlay payload (which carries the
 * run's figures) all have to be dropped.
 *
 * `MY_TRIAL_RUNS_QUERY_KEYS.all` is a prefix of `.objectives(id)`, so this
 * covers the open run's objective list too.
 *
 * Deliberately does *not* touch `MY_EVENTS_QUERY_KEYS`: nothing a trial tick
 * writes can appear there — the other tabs report the official record only —
 * so invalidating it would refetch every tab's objectives on every tick.
 */
export const invalidateTrialProgress = (queryClient: QueryClient, eventId: string): void => {
  queryClient.invalidateQueries({ queryKey: MY_TRIAL_RUNS_QUERY_KEYS.all })
  invalidateEventScope(queryClient, eventId, 'progress')
}

/**
 * A run's existence or state changed — enabled, started, stopped, reset or
 * disabled. Everything `invalidateTrialProgress` covers, plus the My Events
 * summaries: those carry the per-game `isTrialActive` flag that decides
 * whether the regular tabs' checkboxes are read-only, and disabling a run
 * cascades its completions away entirely.
 */
export const invalidateTrialState = (queryClient: QueryClient, eventId: string): void => {
  invalidateTrialProgress(queryClient, eventId)
  queryClient.invalidateQueries({ queryKey: MY_EVENTS_QUERY_KEYS.all })
}
