/**
 * Wire types for the OBS streamer overlay: the settings saved on an overlay
 * token (mirrors `OverlayTokenSettings` in `Soulsjwa.Api`) and the body the
 * token-gated overlay poll returns. The view/theme tuples live here rather
 * than in the overlay feature so the wire contract has no dependency on UI
 * code; `features/events/overlay/overlayConfig.ts` re-exports them for the
 * overlay's own use.
 */

import type { ScoreboardResponse } from './index'

export const OVERLAY_VIEWS = ['objectives', 'scores', 'games'] as const
export type OverlayView = (typeof OVERLAY_VIEWS)[number]

export const OVERLAY_THEMES = ['dark', 'light'] as const
export type OverlayTheme = (typeof OVERLAY_THEMES)[number]

/**
 * How one overlay token's OBS source looks. Every knob mirrors a query-string
 * parameter of the overlay route; once saved on the token these take
 * precedence over the URL for every knob they carry, so a source already on
 * screen picks a change up on its next poll. The page background (`bg`) is
 * deliberately not here: it exists for previewing outside OBS, not for the
 * source, and stays URL-only.
 */
export interface OverlayTokenSettings {
  view: OverlayView
  theme: OverlayTheme
  /** Event-game ids to restrict to, or `null` for every game. */
  gameIds: string[] | null
  /** Competitor user ids to restrict to, or `null` for everyone. */
  playerIds: string[] | null
  pageSize: number
  /** Seconds between automatic page rotation; `0` disables auto-cycle. */
  cycleSeconds: number
  /** Seconds between overlay polls. */
  refreshSeconds: number
  showTitle: boolean
  showProgress: boolean
  showPagination: boolean
  highlight: boolean
  highlightSeconds: number
  animate: boolean
  /** 0–100 opacity of the panel behind the rows; the theme's panel colour is kept. */
  panelOpacity: number
  /** Title-row override, or `null` for the event name. */
  title: string | null
}

export interface CreateOverlayTokenRequest {
  name: string
  /** The look to save with the token, so the designed overlay and its URL are minted together. */
  settings: OverlayTokenSettings | null
}

/** What the OBS browser source polls: the scoreboard plus the token's saved look. */
export interface OverlayScoreboardResponse {
  scoreboard: ScoreboardResponse
  settings: OverlayTokenSettings | null
}
