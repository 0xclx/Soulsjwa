/** The two score views a started event with an enabled game can switch between. */
export const SCOREBOARD_VIEWS = ['game', 'event'] as const
export type ScoreboardView = (typeof SCOREBOARD_VIEWS)[number]
export const DEFAULT_SCOREBOARD_VIEW: ScoreboardView = 'game'
export const SCOREBOARD_VIEW_PARAM = 'view'
export const SCOREBOARD_VIEW_LABELS: Record<ScoreboardView, string> = {
  game: 'Current game',
  event: 'Whole event',
}

/** The one place a raw `?view=` value is narrowed; unknown values fall back to the default. */
export const parseScoreboardView = (raw: string | null): ScoreboardView =>
  SCOREBOARD_VIEWS.find((view) => view === raw) ?? DEFAULT_SCOREBOARD_VIEW
