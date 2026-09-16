import Box from '@mui/material/Box'
import Chip from '@mui/material/Chip'
import Divider from '@mui/material/Divider'
import FormControlLabel from '@mui/material/FormControlLabel'
import MenuItem from '@mui/material/MenuItem'
import Slider from '@mui/material/Slider'
import Stack from '@mui/material/Stack'
import Switch from '@mui/material/Switch'
import TextField from '@mui/material/TextField'
import Typography from '@mui/material/Typography'
import type { OverlayTokenSettings } from '../../../types/overlay'
import {
  OVERLAY_LIMITS,
  OVERLAY_THEMES,
  OVERLAY_THEME_LABELS,
  OVERLAY_VIEWS,
  OVERLAY_VIEW_LABELS,
  type OverlayTheme,
  type OverlayView,
} from '../overlay/overlayConfig'

/** Minimal competitor descriptor used to populate the "pin to player" picker. */
export interface OverlaySettingsCompetitor {
  userId: string
  displayName: string
}

/** Minimal game descriptor used to populate the "restrict to game" picker. */
export interface OverlaySettingsGame {
  eventGameId: string
  gameName: string
  /** The event's single currently-scored game. */
  isEnabled: boolean
  /** Whether any competitor currently has a recording trial run on this game. */
  hasActiveTrial: boolean
}

interface OverlaySettingsFormProps {
  value: OverlayTokenSettings
  onChange: (next: OverlayTokenSettings) => void
  /** Competitors available to pin the overlay to (for the objectives view). */
  competitors?: OverlaySettingsCompetitor[]
  games?: OverlaySettingsGame[]
}

const ALL_PLAYERS = '__all__'
const ALL_GAMES = '__all__'

export const TOP_RANKED_LABEL = 'Top-ranked competitor'
export const ALL_GAMES_LABEL = 'All games'
export const PANEL_OPACITY_LABEL = 'Panel opacity'

type NumericKnob = keyof Pick<
  OverlayTokenSettings,
  'pageSize' | 'cycleSeconds' | 'refreshSeconds' | 'highlightSeconds' | 'panelOpacity'
>

type ToggleKnob = keyof Pick<
  OverlayTokenSettings,
  'showTitle' | 'showProgress' | 'showPagination' | 'highlight' | 'animate'
>

const NUMERIC_KNOB_LABELS: Record<NumericKnob, string> = {
  pageSize: 'Page size',
  cycleSeconds: 'Cycle (s)',
  refreshSeconds: 'Refresh (s)',
  highlightSeconds: 'Highlight (s)',
  panelOpacity: PANEL_OPACITY_LABEL,
}

const TOGGLE_KNOB_LABELS: Record<ToggleKnob, string> = {
  showTitle: 'Title',
  showProgress: 'Progress',
  showPagination: 'Pagination',
  highlight: 'Highlight',
  animate: 'Animate',
}

const NUMERIC_FIELDS: readonly NumericKnob[] = [
  'pageSize',
  'cycleSeconds',
  'refreshSeconds',
  'highlightSeconds',
]

const TOGGLES: readonly ToggleKnob[] = [
  'showTitle',
  'showProgress',
  'showPagination',
  'highlight',
  'animate',
]

function clampInt(value: number, min: number, max: number, fallback: number): number {
  if (!Number.isFinite(value)) return fallback
  return Math.min(max, Math.max(min, Math.trunc(value)))
}

/** The first pinned id, or the "everyone" sentinel: the form pins at most one player and one game. */
const pinnedValue = (ids: string[] | null, all: string): string => ids?.[0] ?? all

const pinFromValue = (value: string, all: string): string[] | null =>
  value === all ? null : [value]

/**
 * Edits an overlay token's look — view, pinned player, restricted game, page
 * size, cycle/refresh timings, transitions, theme, panel opacity, and the
 * visibility toggles (see docs/streamer-overlay.md). Controlled: the parent
 * owns the settings so the live preview beside this form can render every
 * change as it is made.
 */
export function OverlaySettingsForm({
  value,
  onChange,
  competitors,
  games,
}: OverlaySettingsFormProps) {
  const set = <K extends keyof OverlayTokenSettings>(key: K, next: OverlayTokenSettings[K]) =>
    onChange({ ...value, [key]: next })

  return (
    <Box>
      <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1} sx={{ mb: 1 }}>
        <TextField
          select
          size="small"
          label="View"
          value={value.view}
          onChange={(e) => set('view', e.target.value as OverlayView)}
          fullWidth
        >
          {OVERLAY_VIEWS.map((v) => (
            <MenuItem key={v} value={v}>
              {OVERLAY_VIEW_LABELS[v]}
            </MenuItem>
          ))}
        </TextField>
        <TextField
          select
          size="small"
          label="Theme"
          value={value.theme}
          onChange={(e) => set('theme', e.target.value as OverlayTheme)}
          fullWidth
        >
          {OVERLAY_THEMES.map((t) => (
            <MenuItem key={t} value={t}>
              {OVERLAY_THEME_LABELS[t]}
            </MenuItem>
          ))}
        </TextField>
      </Stack>

      {competitors && competitors.length > 0 && (
        <TextField
          select
          size="small"
          label="Pin to player (objectives view)"
          value={pinnedValue(value.playerIds, ALL_PLAYERS)}
          onChange={(e) => set('playerIds', pinFromValue(e.target.value, ALL_PLAYERS))}
          fullWidth
          sx={{ mb: 1 }}
        >
          <MenuItem value={ALL_PLAYERS}>{TOP_RANKED_LABEL}</MenuItem>
          {competitors.map((c) => (
            <MenuItem key={c.userId} value={c.userId}>
              {c.displayName}
            </MenuItem>
          ))}
        </TextField>
      )}

      {games && games.length > 0 && (
        <TextField
          select
          size="small"
          label="Restrict to game"
          value={pinnedValue(value.gameIds, ALL_GAMES)}
          onChange={(e) => set('gameIds', pinFromValue(e.target.value, ALL_GAMES))}
          fullWidth
          sx={{ mb: 1 }}
        >
          <MenuItem value={ALL_GAMES}>{ALL_GAMES_LABEL}</MenuItem>
          {games.map((g) => (
            <MenuItem key={g.eventGameId} value={g.eventGameId}>
              <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
                <span>{g.gameName}</span>
                {g.isEnabled && (
                  <Chip label="Active" size="small" color="success" variant="outlined" />
                )}
                {g.hasActiveTrial && (
                  <Chip label="Trial" size="small" color="warning" variant="outlined" />
                )}
              </Stack>
            </MenuItem>
          ))}
        </TextField>
      )}

      <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1} sx={{ mb: 1 }}>
        {NUMERIC_FIELDS.map((knob) => {
          const { min, max } = OVERLAY_LIMITS[knob]
          return (
            <TextField
              key={knob}
              type="number"
              size="small"
              label={NUMERIC_KNOB_LABELS[knob]}
              value={value[knob]}
              onChange={(e) => set(knob, clampInt(Number(e.target.value), min, max, value[knob]))}
              slotProps={{ htmlInput: { min, max } }}
              fullWidth
            />
          )
        })}
      </Stack>

      <TextField
        size="small"
        label="Title override (optional)"
        value={value.title ?? ''}
        onChange={(e) => set('title', e.target.value === '' ? null : e.target.value)}
        slotProps={{ htmlInput: { maxLength: OVERLAY_LIMITS.titleLength } }}
        fullWidth
        sx={{ mb: 1 }}
      />

      <Box sx={{ display: 'flex', flexWrap: 'wrap', columnGap: 2 }}>
        {TOGGLES.map((knob) => (
          <FormControlLabel
            key={knob}
            control={
              <Switch
                size="small"
                checked={value[knob]}
                onChange={(e) => set(knob, e.target.checked)}
              />
            }
            label={TOGGLE_KNOB_LABELS[knob]}
          />
        ))}
      </Box>

      <Divider sx={{ my: 1.5 }} />

      <Typography variant="body2" sx={{ fontWeight: 600, mb: 0.5 }}>
        {PANEL_OPACITY_LABEL}
      </Typography>
      <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 1 }}>
        The box behind the title, objectives, and rows — separate from the page itself, which OBS
        already renders transparent. Lower this to fade the panel toward see-through.
      </Typography>
      <Stack direction="row" spacing={2} sx={{ alignItems: 'center' }}>
        <Stack sx={{ flex: 1 }}>
          <Slider
            size="small"
            value={value.panelOpacity}
            onChange={(_e, next) => set('panelOpacity', next as number)}
            min={OVERLAY_LIMITS.panelOpacity.min}
            max={OVERLAY_LIMITS.panelOpacity.max}
            valueLabelDisplay="auto"
            valueLabelFormat={(v) => `${v}%`}
            aria-label={PANEL_OPACITY_LABEL}
          />
        </Stack>
        <Typography variant="body2" color="text.secondary" sx={{ minWidth: 90 }}>
          {value.panelOpacity}%
        </Typography>
      </Stack>
    </Box>
  )
}
