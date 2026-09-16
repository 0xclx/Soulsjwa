/**
 * The streamer overlay is an OBS browser source configured through
 * query-string parameters, so a streamer can copy/paste one URL without
 * authenticating, and — once a look is saved on the token — through that
 * saved look, which wins over the URL so a source already on screen follows
 * later edits. Every knob has a default; a bare `/events/:id/overlay` URL
 * works.
 */

import {
  OVERLAY_THEMES,
  OVERLAY_VIEWS,
  type OverlayTheme,
  type OverlayTokenSettings,
  type OverlayView,
} from '../../../types/overlay'

export { OVERLAY_THEMES, OVERLAY_VIEWS }
export type { OverlayTheme, OverlayView }

export const OVERLAY_VIEW_LABELS: Record<OverlayView, string> = {
  objectives: 'Objectives (grouped by game & category)',
  scores: 'Scoreboard (competitor ranks)',
  games: 'Game-by-game (one page per game)',
}

export const OVERLAY_THEME_LABELS: Record<OverlayTheme, string> = {
  dark: 'Dark',
  light: 'Light',
}

/**
 * The ranges every numeric knob is clamped to, shared by the query-string
 * parser and the settings form. `OverlayTokenSettingsLimits` in the API
 * carries the same values; change both together.
 */
export const OVERLAY_LIMITS = {
  pageSize: { min: 1, max: 50 },
  cycleSeconds: { min: 0, max: 600 },
  refreshSeconds: { min: 2, max: 120 },
  highlightSeconds: { min: 1, max: 60 },
  panelOpacity: { min: 0, max: 100 },
  titleLength: 100,
} as const

/** What a bare overlay URL renders, and what a new token's look starts from. */
export const OVERLAY_DEFAULT_SETTINGS: OverlayTokenSettings = {
  view: 'objectives',
  theme: 'dark',
  gameIds: null,
  playerIds: null,
  pageSize: 10,
  cycleSeconds: 30,
  refreshSeconds: 5,
  showTitle: true,
  showProgress: true,
  showPagination: true,
  highlight: true,
  highlightSeconds: 6,
  animate: true,
  panelOpacity: 80,
  title: null,
}

/**
 * The query-string flag that puts the overlay route in preview mode, where it
 * renders what its parent window posts to it instead of polling — see
 * `overlayPreviewProtocol.ts`.
 */
export const OVERLAY_PREVIEW_PARAM = 'preview'

/** True when the query string asks for preview mode (`preview=1`, or any accepted truthy spelling). */
export function isOverlayPreviewRequest(params: URLSearchParams): boolean {
  return parseBool(params.get(OVERLAY_PREVIEW_PARAM), false)
}

/**
 * Everything the overlay renders from: the token's look plus the one
 * URL-only knob, the page background, which only matters when previewing
 * outside OBS (an OBS browser source is already transparent).
 */
export interface OverlayConfig extends OverlayTokenSettings {
  /** Validated CSS colour for the *page* background (not the panel), or `null` for transparent. */
  background: string | null
}

const TRUE_VALUES = new Set(['1', 'true', 'yes', 'on'])
const FALSE_VALUES = new Set(['0', 'false', 'no', 'off'])

function parseBool(value: string | null, fallback: boolean): boolean {
  if (value == null) return fallback
  const v = value.trim().toLowerCase()
  if (TRUE_VALUES.has(v)) return true
  if (FALSE_VALUES.has(v)) return false
  return fallback
}

function parseInt32(
  value: string | null,
  fallback: number,
  { min, max }: { min: number; max: number },
): number {
  if (value == null) return fallback
  const n = Number.parseInt(value, 10)
  if (!Number.isFinite(n)) return fallback
  return Math.min(max, Math.max(min, n))
}

function parseList(value: string | null): string[] | null {
  if (value == null) return null
  // The "all"/"*"/empty sentinels are matched case-insensitively. The actual
  // returned ids preserve their original casing because user/event-game ids
  // are case-sensitive Guid/string identifiers.
  const sentinel = value.trim().toLowerCase()
  if (sentinel === '' || sentinel === 'all' || sentinel === '*') return null
  return value
    .split(',')
    .map((s) => s.trim())
    .filter((s) => s.length > 0)
}

function parseTheme(value: string | null, fallback: OverlayTheme): OverlayTheme {
  return (OVERLAY_THEMES as readonly string[]).includes(value ?? '')
    ? (value as OverlayTheme)
    : fallback
}

function parseView(value: string | null, fallback: OverlayView): OverlayView {
  return (OVERLAY_VIEWS as readonly string[]).includes(value ?? '')
    ? (value as OverlayView)
    : fallback
}

const HEX_COLOR_PATTERN = /^#(?:[0-9a-f]{3,4}|[0-9a-f]{6}|[0-9a-f]{8})$/i
// rgb()/rgba()/hsl()/hsla() with only numeric, percent, comma, space, slash,
// dot, or minus content inside the parens — never anything that could close
// the declaration or open a new one.
const FUNCTIONAL_COLOR_PATTERN = /^(?:rgb|rgba|hsl|hsla)\(\s*[0-9.%,\s/-]+\)$/i
// Conservative allowlist, not the full CSS named-colour list.
const NAMED_COLORS = new Set([
  'transparent',
  'black',
  'white',
  'red',
  'green',
  'blue',
  'yellow',
  'orange',
  'purple',
  'pink',
  'gray',
  'grey',
  'cyan',
  'magenta',
  'brown',
  'navy',
  'teal',
  'lime',
  'maroon',
  'olive',
  'silver',
  'gold',
  'indigo',
  'violet',
  'coral',
  'salmon',
  'khaki',
  'crimson',
  'orchid',
  'plum',
  'tan',
  'beige',
  'ivory',
  'lavender',
  'turquoise',
])
// A value containing any of these can never be a real colour — it can only
// be an attempt to close the CSS declaration/rule early (a `background`
// style value is handed to Emotion, which serialises it into a real
// stylesheet by string interpolation and does not sanitise it).
const CSS_BREAKOUT_PATTERN = /[;{}\\]|\/\*|url\(/i

/**
 * Validates a user-supplied CSS colour value for use as the overlay's page
 * background. Returns `null` (never the raw input) for anything that isn't
 * recognisably a colour, so the caller's `?? 'transparent'` default applies.
 */
function parseCssColor(value: string | null): string | null {
  if (value == null) return null
  const trimmed = value.trim()
  if (trimmed === '' || CSS_BREAKOUT_PATTERN.test(trimmed)) return null
  if (NAMED_COLORS.has(trimmed.toLowerCase())) return trimmed.toLowerCase()
  if (HEX_COLOR_PATTERN.test(trimmed) || FUNCTIONAL_COLOR_PATTERN.test(trimmed)) return trimmed
  return null
}

export function parseOverlayConfig(search: URLSearchParams | string): OverlayConfig {
  const params = typeof search === 'string' ? new URLSearchParams(search) : search
  const d = OVERLAY_DEFAULT_SETTINGS
  return {
    gameIds: parseList(params.get('games')),
    playerIds: parseList(params.get('players')),
    pageSize: parseInt32(params.get('pageSize'), d.pageSize, OVERLAY_LIMITS.pageSize),
    cycleSeconds: parseInt32(params.get('cycle'), d.cycleSeconds, OVERLAY_LIMITS.cycleSeconds),
    showProgress: parseBool(params.get('showProgress'), d.showProgress),
    showPagination: parseBool(params.get('showPagination'), d.showPagination),
    showTitle: parseBool(params.get('showTitle'), d.showTitle),
    highlight: parseBool(params.get('highlight'), d.highlight),
    highlightSeconds: parseInt32(
      params.get('highlightSeconds'),
      d.highlightSeconds,
      OVERLAY_LIMITS.highlightSeconds,
    ),
    animate: parseBool(params.get('animate'), d.animate),
    theme: parseTheme(params.get('theme'), d.theme),
    background: parseCssColor(params.get('bg')),
    panelOpacity: parseInt32(
      params.get('panelOpacity'),
      d.panelOpacity,
      OVERLAY_LIMITS.panelOpacity,
    ),
    refreshSeconds: parseInt32(
      params.get('refresh'),
      d.refreshSeconds,
      OVERLAY_LIMITS.refreshSeconds,
    ),
    view: parseView(params.get('view'), d.view),
    title: params.get('title'),
  }
}

/**
 * The look the overlay actually renders: a look saved on the token wins over
 * the URL for every knob it carries, so a source already on screen follows
 * later edits without its URL being re-pasted. The page background stays the
 * URL's, being the one knob a saved look never carries. A token with nothing
 * saved (or a poll that has not answered yet) leaves the URL in charge.
 */
export function applyOverlaySettings(
  fromUrl: OverlayConfig,
  saved: OverlayTokenSettings | null | undefined,
): OverlayConfig {
  return saved ? { ...fromUrl, ...saved } : fromUrl
}
