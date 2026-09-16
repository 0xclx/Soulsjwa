import { useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import FormControlLabel from '@mui/material/FormControlLabel'
import MenuItem from '@mui/material/MenuItem'
import Stack from '@mui/material/Stack'
import Switch from '@mui/material/Switch'
import TextField from '@mui/material/TextField'
import Tooltip from '@mui/material/Tooltip'
import Typography from '@mui/material/Typography'
import BoltIcon from '@mui/icons-material/Bolt'
import type { OverlayTokenSettings } from '../../../types/overlay'
import { useScoreboard } from '../hooks/useScoreboard'
import { OVERLAY_PREVIEW_PARAM } from '../overlay/overlayConfig'
import { isOverlayPreviewReadyMessage, postOverlayPreview } from '../overlay/overlayPreviewProtocol'
import {
  CUSTOM_SIZE_ID,
  CUSTOM_SIZE_LABEL,
  OVERLAY_PREVIEW_BACKDROPS,
  OVERLAY_PREVIEW_BACKDROP_LABELS,
  OVERLAY_PREVIEW_BACKDROP_STYLES,
  OVERLAY_PREVIEW_SIZES,
  OVERLAY_PREVIEW_SIZE_LIMITS,
  type OverlayPreviewBackdrop,
  type OverlayPreviewSizeId,
} from '../overlay/overlayPreviewOptions'
import { applyFlashNudges, pickNudgeTarget, type FlashNudge } from '../overlay/overlayFlashNudge'
import { buildSampleScoreboard, type SampleGame } from '../overlay/overlaySampleData'

export const SAMPLE_DATA_LABEL = 'Use sample data'
export const SAMPLE_DATA_HINT =
  'Fictional competitors over this event’s games; every few seconds one of them completes an objective, so highlights, page jumps and progress can be seen. A pinned real player has no sample row, so the top-ranked sample competitor is shown instead.'
export const SIMULATE_COMPLETION_LABEL = 'Simulate a completion'
export const SIMULATE_COMPLETION_HINT =
  'Pretends a competitor just completed their next objective — in the objectives view the one on screen, otherwise the next competitor in turn — so the overlay reacts as it would live: the row flashes or the tick animates in, and the page jumps if needed. Only the preview sees it.'
/** How often the sample scoreboard completes another objective per competitor. */
const SAMPLE_TICK_MS = 8_000
/** The preview polls the real scoreboard this often, like the app's live tabs do. */
const LIVE_PREVIEW = true

interface OverlayPreviewProps {
  eventId: string
  eventName: string
  /** The event's games, for the sample scoreboard. */
  games: SampleGame[]
  settings: OverlayTokenSettings
}

/**
 * A live preview of the OBS overlay at a chosen source size. Embeds the real
 * overlay route in preview mode and posts it the look and the scoreboard
 * (real, polled from the public scoreboard; or fictional, when the event has
 * nothing to show yet), re-posting on every change — see
 * `overlayPreviewProtocol.ts` for why the frame is not simply given a URL.
 */
export function OverlayPreview({ eventId, eventName, games, settings }: OverlayPreviewProps) {
  const [sizeId, setSizeId] = useState<OverlayPreviewSizeId>(OVERLAY_PREVIEW_SIZES[0].id)
  const [custom, setCustom] = useState<{ width: number; height: number }>({
    width: OVERLAY_PREVIEW_SIZES[0].width,
    height: OVERLAY_PREVIEW_SIZES[0].height,
  })
  const [backdrop, setBackdrop] = useState<OverlayPreviewBackdrop>(OVERLAY_PREVIEW_BACKDROPS[0])
  const [useSample, setUseSample] = useState(false)
  const [sampleTick, setSampleTick] = useState(0)
  // Fixed for the life of the preview so sample timestamps only move when a
  // sample competitor completes something, never merely because time passed.
  const [sampleBase] = useState(() => new Date())
  const [nudges, setNudges] = useState<FlashNudge[]>([])
  const [nudgeTurn, setNudgeTurn] = useState(0)

  const size = useMemo(() => {
    const preset = OVERLAY_PREVIEW_SIZES.find((s) => s.id === sizeId)
    return preset ? { width: preset.width, height: preset.height } : custom
  }, [sizeId, custom])

  const { data: liveScoreboard } = useScoreboard(eventId, LIVE_PREVIEW)

  useEffect(() => {
    if (!useSample) return
    const handle = window.setInterval(() => setSampleTick((t) => t + 1), SAMPLE_TICK_MS)
    return () => window.clearInterval(handle)
  }, [useSample])

  const baseScoreboard = useMemo(
    () =>
      useSample ? buildSampleScoreboard(games, sampleTick, sampleBase) : (liveScoreboard ?? null),
    [useSample, games, sampleTick, sampleBase, liveScoreboard],
  )
  const scoreboard = useMemo(
    () => (baseScoreboard ? applyFlashNudges(baseScoreboard, nudges) : null),
    [baseScoreboard, nudges],
  )

  // The objectives view shows one competitor — the pinned one, else the
  // leader — so that is who completes something there. Elsewhere each press
  // picks the next competitor in the board's order, so repeated presses walk
  // through every page rather than re-flashing one row.
  const simulateCompletion = () => {
    // Chosen from the board as currently shown, earlier nudges included, so
    // each press completes something new rather than re-stamping the last.
    const entries = scoreboard?.entries ?? []
    if (entries.length === 0) return
    const shown =
      settings.view === 'objectives'
        ? (entries.find((e) => settings.playerIds?.includes(e.userId)) ??
          [...entries].sort(
            (a, b) => b.totalScore - a.totalScore || a.displayName.localeCompare(b.displayName),
          )[0]!)
        : entries[nudgeTurn % entries.length]!
    const nudge: FlashNudge = {
      userId: shown.userId,
      objectiveId: pickNudgeTarget(shown)?.objectiveId ?? null,
      at: new Date().toISOString(),
    }
    setNudges((current) => [...current, nudge])
    setNudgeTurn(nudgeTurn + 1)
  }

  // The frame announces when it is listening; until then a post would be
  // lost, and after that every change is posted as it happens. Counted, not
  // flagged, so a frame that reloads (and announces again) is fed again.
  const frameRef = useRef<HTMLIFrameElement>(null)
  const [frameReadyCount, setFrameReadyCount] = useState(0)
  useEffect(() => {
    const onMessage = (event: MessageEvent) => {
      if (event.source !== frameRef.current?.contentWindow) return
      if (isOverlayPreviewReadyMessage(event)) setFrameReadyCount((n) => n + 1)
    }
    window.addEventListener('message', onMessage)
    return () => window.removeEventListener('message', onMessage)
  }, [])

  useEffect(() => {
    const target = frameRef.current?.contentWindow
    if (frameReadyCount === 0 || !target) return
    // The backdrop is drawn behind the frame, so the page itself stays
    // transparent exactly as it is in OBS.
    postOverlayPreview(target, {
      config: { ...settings, background: null },
      scoreboard,
      eventName,
    })
  }, [frameReadyCount, settings, scoreboard, eventName])

  // Shrink the frame to fit the space available without changing its
  // viewport, so text sizes stay what OBS would show at that source size.
  const stageRef = useRef<HTMLDivElement>(null)
  const [stageWidth, setStageWidth] = useState(0)
  useLayoutEffect(() => {
    const stage = stageRef.current
    if (!stage) return
    const measure = () => setStageWidth(stage.clientWidth)
    measure()
    if (typeof ResizeObserver === 'undefined') {
      window.addEventListener('resize', measure)
      return () => window.removeEventListener('resize', measure)
    }
    const observer = new ResizeObserver(measure)
    observer.observe(stage)
    return () => observer.disconnect()
  }, [])
  const scale = stageWidth > 0 ? Math.min(1, stageWidth / size.width) : 1

  const previewUrl = `/events/${eventId}/overlay?${OVERLAY_PREVIEW_PARAM}=1`

  return (
    <Stack spacing={1.5}>
      <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1} sx={{ alignItems: 'center' }}>
        <TextField
          select
          size="small"
          label="Source size"
          value={sizeId}
          onChange={(e) => setSizeId(e.target.value as OverlayPreviewSizeId)}
          fullWidth
        >
          {OVERLAY_PREVIEW_SIZES.map((s) => (
            <MenuItem key={s.id} value={s.id}>
              {s.label}
            </MenuItem>
          ))}
          <MenuItem value={CUSTOM_SIZE_ID}>{CUSTOM_SIZE_LABEL}</MenuItem>
        </TextField>
        {sizeId === CUSTOM_SIZE_ID && (
          <>
            <TextField
              type="number"
              size="small"
              label="Width"
              value={custom.width}
              onChange={(e) =>
                setCustom((c) => ({ ...c, width: clampSize(e.target.value, c.width) }))
              }
              slotProps={{ htmlInput: OVERLAY_PREVIEW_SIZE_LIMITS }}
            />
            <TextField
              type="number"
              size="small"
              label="Height"
              value={custom.height}
              onChange={(e) =>
                setCustom((c) => ({ ...c, height: clampSize(e.target.value, c.height) }))
              }
              slotProps={{ htmlInput: OVERLAY_PREVIEW_SIZE_LIMITS }}
            />
          </>
        )}
        <TextField
          select
          size="small"
          label="Behind it"
          value={backdrop}
          onChange={(e) => setBackdrop(e.target.value as OverlayPreviewBackdrop)}
          fullWidth
        >
          {OVERLAY_PREVIEW_BACKDROPS.map((b) => (
            <MenuItem key={b} value={b}>
              {OVERLAY_PREVIEW_BACKDROP_LABELS[b]}
            </MenuItem>
          ))}
        </TextField>
      </Stack>

      <Stack
        direction="row"
        spacing={2}
        sx={{ alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap' }}
      >
        <Tooltip title={SAMPLE_DATA_HINT}>
          <FormControlLabel
            control={
              <Switch
                size="small"
                checked={useSample}
                onChange={(e) => setUseSample(e.target.checked)}
              />
            }
            label={SAMPLE_DATA_LABEL}
          />
        </Tooltip>
        <Tooltip title={SIMULATE_COMPLETION_HINT}>
          <span>
            <Button
              size="small"
              variant="outlined"
              startIcon={<BoltIcon />}
              onClick={simulateCompletion}
              disabled={!baseScoreboard || baseScoreboard.entries.length === 0}
            >
              {SIMULATE_COMPLETION_LABEL}
            </Button>
          </span>
        </Tooltip>
      </Stack>

      <Box
        ref={stageRef}
        data-testid="overlay-preview-stage"
        sx={{
          width: '100%',
          height: size.height * scale,
          overflow: 'hidden',
          borderRadius: 1,
          border: 1,
          borderColor: 'divider',
          ...OVERLAY_PREVIEW_BACKDROP_STYLES[backdrop],
        }}
      >
        <iframe
          ref={frameRef}
          title="Overlay preview"
          src={previewUrl}
          width={size.width}
          height={size.height}
          style={{
            border: 0,
            display: 'block',
            transform: `scale(${scale})`,
            transformOrigin: 'top left',
            // Only the frame's own content is interactive, and none of it is
            // meant to be clicked from here.
            pointerEvents: 'none',
            colorScheme: 'normal',
          }}
        />
      </Box>
      <Typography variant="caption" color="text.secondary">
        Shown at {size.width}×{size.height}
        {scale < 1 ? `, scaled to ${Math.round(scale * 100)}% to fit` : ''}. Text sizes follow the
        source size, as they do in OBS.
      </Typography>
    </Stack>
  )
}

function clampSize(raw: string, fallback: number): number {
  const n = Number.parseInt(raw, 10)
  if (!Number.isFinite(n)) return fallback
  return Math.min(OVERLAY_PREVIEW_SIZE_LIMITS.max, Math.max(OVERLAY_PREVIEW_SIZE_LIMITS.min, n))
}
