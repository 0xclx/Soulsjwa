/**
 * How often every scoreboard surface re-reads the scoreboard and the event it
 * belongs to. Near-live is enough for viewers and keeps server load down; a
 * change to the event (start, enabled game) shows within one interval.
 */
export const SCOREBOARD_REFRESH_INTERVAL_MS = 30_000
