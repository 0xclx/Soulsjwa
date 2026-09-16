import { useEffect, useMemo, useState } from 'react'
import type { ScoreboardEntry, ScoreboardResponse } from '../../../../types'
import { trialGames, trialTotals } from '../../scoreboard/scoreboardMetrics'
import { TRIAL_ACCENT_HEX, TRIAL_BADGE_TEXT_OVERLAY } from '../../scoreboard/trialPresentation'
import type { OverlayConfig } from '../overlayConfig'
import {
  applyFilters,
  buildGamePages,
  buildObjectiveItems,
  chunkEntries,
  chunkObjectiveItems,
  entryProgress,
  type ObjectiveItem,
  type ScopedEntry,
} from '../overlayScope'
import { ProgressBar } from './ProgressBar'
import { useChangeHighlights } from '../useChangeHighlights'
import { useFreshChanges } from '../useFreshChanges'
import { DARK_THEME, LIGHT_THEME, withPanelOpacity, type OverlayPalette } from '../overlayPalette'
import { ObjectivesView } from './ObjectivesView'
import { ScoreRow } from './ScoreRow'

/** Shown in the title row when neither the look nor the event supplies a name. */
export const OVERLAY_FALLBACK_TITLE = 'Soulsjwa'

type RowKey = (entry: ScoreboardEntry) => string

/** A competitor is on screen once in the scores view … */
const byCompetitor: RowKey = (entry) => entry.userId
/** … but once per game page in the games view, where each page narrows the timestamps to its game. */
const byCompetitorAndGame: RowKey = (entry) =>
  `${entry.userId}:${entry.games[0]?.eventGameId ?? ''}`

/** The lowest page holding an item whose key is in `keys`, or null when none is. */
const pageHolding = <T,>(
  pages: readonly (readonly T[])[],
  keyOf: (item: T) => string,
  keys: Iterable<string>,
): number | null => {
  const wanted = new Set(keys)
  if (wanted.size === 0) return null
  const index = pages.findIndex((page) => page.some((item) => wanted.has(keyOf(item))))
  return index === -1 ? null : index
}

const objectiveKey = (item: ObjectiveItem): string => item.objective.objectiveId
/**
 * Both completion timestamps an objective can show, tracked separately and
 * never collapsed into "the one being drawn": which one the row draws flips
 * when a trial pauses or resumes, and reading the flipped value would flash
 * the row with no completion behind it — the same false positive the row
 * highlights guard against.
 */
const objectiveShown = (item: ObjectiveItem): readonly (string | null)[] => [
  item.objective.completedAt,
  item.objective.trial?.completedAt ?? null,
]

/** How long the objectives view sweeps the strikethrough across a fresh completion. */
export const OBJECTIVE_STRIKE_SECONDS = 3.5

/** One half of the page cross-fade: the old page fades out for this long, then the new one fades in for as long. */
export const PAGE_FADE_MS = 350

interface OverlayRendererProps {
  config: OverlayConfig
  /** The scoreboard to draw; `undefined`/`null` while nothing has arrived yet. */
  scoreboard: ScoreboardResponse | null | undefined
  /** Falls back to `OVERLAY_FALLBACK_TITLE` in the title row when the look has no title of its own. */
  eventName: string | undefined
  /** Replaces the rows with a centred message — a misconfigured token, or a preview still waiting for data. */
  message: string | null
}

/**
 * Draws the overlay from a resolved look and a scoreboard, whichever way the
 * two arrived: polled with a token on the OBS route, or posted in by the app's
 * designer in preview mode. Owns the page cycling, the "Page X / N"
 * indicator and the change highlights, so the two modes cannot drift.
 */
export function OverlayRenderer({ config, scoreboard, eventName, message }: OverlayRendererProps) {
  const scope = useMemo(
    () => (scoreboard ? applyFilters(scoreboard, config) : null),
    [scoreboard, config],
  )

  const pages = useMemo(() => {
    if (!scope) return [] as ScopedEntry[][]
    return config.view === 'games'
      ? buildGamePages(scope.entries)
      : chunkEntries(scope.entries, config.pageSize)
  }, [scope, config.view, config.pageSize])

  // In the `objectives` view we render one competitor's full objective
  // checklist. Prefer the single filtered player if the look pins exactly one,
  // otherwise fall back to the top-ranked competitor in scope.
  const objectiveTarget = useMemo<ScopedEntry | null>(() => {
    if (!scope || scope.entries.length === 0) return null
    if (config.playerIds && config.playerIds.length === 1) {
      const pinned = scope.entries.find((e) => e.userId === config.playerIds![0])
      if (pinned) return pinned
    }
    return scope.entries[0] ?? null
  }, [scope, config.playerIds])

  // Display, not substitution: a run that has earned a score keeps it on
  // screen once paused, exactly as the scores view does. Keying this on
  // "still recording" made the two views of the same event disagree — pausing
  // erased the amber score here while the scores view went on showing it.
  const objectiveTrialTotals = useMemo(
    () => (objectiveTarget ? trialTotals(trialGames(objectiveTarget.games)) : null),
    [objectiveTarget],
  )

  const objectivePages = useMemo(
    () =>
      objectiveTarget
        ? chunkObjectiveItems(buildObjectiveItems(objectiveTarget), config.pageSize)
        : [[]],
    [objectiveTarget, config.pageSize],
  )

  // Both layouts paginate; unify the page count so the cycle timer, the
  // "Page X / N" indicator, and the fade animation are view-agnostic.
  const pageCount = config.view === 'objectives' ? objectivePages.length : pages.length

  const [pageIndex, setPageIndex] = useState(0)
  // Derive a safe index without an effect so `pageIndex` stays in range even
  // when the page count shrinks (filters change). The auto-cycle interval
  // wraps modulo `pageCount` so it self-corrects on its next tick.
  const safePageIndex = pageCount === 0 ? 0 : Math.min(pageIndex, pageCount - 1)

  // What is on screen lags the target page by a cross-fade: the old page
  // fades out, then the new one fades in — rather than a cut followed by a
  // fade-in, which read as a blink. Nothing fades on the first render: the
  // shown page starts at the target. Derived during render (React's own
  // pattern for reacting to a change) so no effect sets state synchronously;
  // the swap itself waits for the fade-out to finish.
  const [shown, setShown] = useState({ index: safePageIndex, fadingOut: false })
  if (shown.index !== safePageIndex && !shown.fadingOut) {
    setShown(
      config.animate
        ? { index: shown.index, fadingOut: true }
        : { index: safePageIndex, fadingOut: false },
    )
  }
  // Keyed on the target too: should it move again mid-fade, the swap waits
  // for the fade again and lands on the latest target.
  useEffect(() => {
    if (!shown.fadingOut) return
    const handle = window.setTimeout(
      () => setShown({ index: safePageIndex, fadingOut: false }),
      PAGE_FADE_MS,
    )
    return () => window.clearTimeout(handle)
  }, [shown.fadingOut, safePageIndex])
  const shownPageIndex = pageCount === 0 ? 0 : Math.min(shown.index, pageCount - 1)

  // Every flash restarts the cycle so its page gets a full cycle on screen
  // rather than being swapped away by a tick that was already due — and
  // holds it for at least the highlight's own duration, so a short cycle
  // cannot paginate away while the row is still flashing. `epoch` changes
  // force the restart even when the hold is unchanged.
  const [cycleRestart, setCycleRestart] = useState({ epoch: 0, holdMs: 0 })

  useEffect(() => {
    if (config.cycleSeconds <= 0 || pageCount <= 1) return
    const period = config.cycleSeconds * 1000
    const advance = () => setPageIndex((curr) => (curr + 1) % pageCount)
    let interval: number | null = null
    const first = window.setTimeout(
      () => {
        advance()
        interval = window.setInterval(advance, period)
      },
      Math.max(period, cycleRestart.holdMs),
    )
    return () => {
      window.clearTimeout(first)
      if (interval !== null) window.clearInterval(interval)
    }
  }, [config.cycleSeconds, pageCount, cycleRestart])

  const currentPage = pages[shownPageIndex] ?? []
  const currentObjectivePage = objectivePages[shownPageIndex] ?? []

  // Detected over the rows as paginated, not the event-wide entries: in the
  // games view each page narrows a competitor's timestamps to that game, so
  // only the page where the completion happened flashes — and is the page
  // worth jumping to.
  const pageRows = useMemo(() => pages.flat(), [pages])
  const rowKey = config.view === 'games' ? byCompetitorAndGame : byCompetitor
  const highlighted = useChangeHighlights(
    pageRows,
    config.highlight,
    config.highlightSeconds,
    rowKey,
  )

  // The objectives view has its own fresh-completion signal: an objective
  // whose shown completion just landed, which the view animates in.
  const objectiveItems = useMemo(() => objectivePages.flat(), [objectivePages])
  const freshObjectives = useFreshChanges(
    objectiveItems,
    objectiveKey,
    objectiveShown,
    config.highlight && config.view === 'objectives',
    config.highlightSeconds,
  )

  // A flash on a page that is not on screen would go unseen, so a freshly
  // flashing row (or freshly completed objective) pulls the overlay to its
  // page; on screen or not, it restarts the cycle and holds the page for the
  // highlight's duration. Only the onset counts: a set that merely shrank as
  // flashes expired, or a row still flashing from an earlier poll, must not
  // keep dragging the page back or extending the hold. Comparing against the
  // previous render's set is React's own pattern for reacting to a prop
  // change during render, and keeps this out of an effect.
  const flashing = config.view === 'objectives' ? freshObjectives : highlighted
  const [seenFlashing, setSeenFlashing] = useState(flashing)
  if (flashing !== seenFlashing) {
    setSeenFlashing(flashing)
    const fresh = [...flashing].filter((key) => !seenFlashing.has(key))
    const target =
      config.view === 'objectives'
        ? pageHolding(objectivePages, objectiveKey, fresh)
        : pageHolding(pages, rowKey, fresh)
    if (target !== null) {
      if (target !== safePageIndex) setPageIndex(target)
      setCycleRestart((r) => ({ epoch: r.epoch + 1, holdMs: config.highlightSeconds * 1000 }))
    }
  }

  const palette = config.theme === 'light' ? LIGHT_THEME : DARK_THEME
  const pageBg = config.background ?? 'transparent'
  const panelBg = withPanelOpacity(palette.panelBg, config.panelOpacity)

  const title = config.title ?? eventName ?? OVERLAY_FALLBACK_TITLE
  const animation = config.animate ? 'all 280ms ease' : 'none'

  return (
    <div
      data-testid="overlay-root"
      style={{
        minHeight: '100dvh',
        width: '100%',
        margin: 0,
        padding: 12,
        background: pageBg,
        color: palette.text,
        fontFamily:
          '"Inter", "Segoe UI", system-ui, -apple-system, "Helvetica Neue", Arial, sans-serif',
        boxSizing: 'border-box',
      }}
    >
      <div
        style={{
          background: panelBg,
          border: `1px solid ${palette.panelBorder}`,
          borderRadius: 12,
          backdropFilter: 'blur(8px)',
          boxShadow: '0 8px 32px rgba(0, 0, 0, 0.25)',
          padding: 16,
          display: 'flex',
          flexDirection: 'column',
          gap: 12,
          transition: animation,
        }}
      >
        {config.showTitle && (
          <div
            style={{
              display: 'flex',
              alignItems: 'baseline',
              gap: 12,
              justifyContent: 'space-between',
            }}
          >
            <div
              style={{
                fontWeight: 700,
                fontSize: 'clamp(14px, 2.2vw, 22px)',
                letterSpacing: '-0.01em',
              }}
            >
              {title}
              {config.view === 'objectives' && objectiveTarget && (
                <span
                  style={{
                    marginLeft: 8,
                    fontWeight: 500,
                    fontSize: '0.7em',
                    color: palette.subText,
                  }}
                >
                  {objectiveTarget.displayName} · {objectiveTarget.totalScore}
                  {objectiveTarget.scoringMaxScore > 0 && ` / ${objectiveTarget.scoringMaxScore}`}
                  {' pts'}
                  {objectiveTrialTotals && (
                    <span style={{ color: TRIAL_ACCENT_HEX }}>
                      {' · '}
                      {TRIAL_BADGE_TEXT_OVERLAY} {objectiveTrialTotals.score}
                    </span>
                  )}
                </span>
              )}
            </div>
            {config.showPagination && pageCount > 1 && (
              <div
                aria-label="Page indicator"
                style={{
                  fontSize: 'clamp(10px, 1.4vw, 13px)',
                  color: palette.subText,
                  fontVariantNumeric: 'tabular-nums',
                }}
              >
                Page {shownPageIndex + 1} / {pageCount}
              </div>
            )}
          </div>
        )}

        {config.view === 'objectives' && config.showProgress && objectiveTarget && (
          <ObjectivesProgress entry={objectiveTarget} palette={palette} animation={animation} />
        )}

        <div
          data-testid="overlay-page"
          data-fading-out={shown.fadingOut ? 'true' : undefined}
          style={{
            display: 'flex',
            flexDirection: 'column',
            gap: 4,
            opacity: shown.fadingOut ? 0 : 1,
            transition: config.animate ? `opacity ${PAGE_FADE_MS}ms ease-in-out` : 'none',
          }}
        >
          {message ? (
            <div
              data-testid="overlay-message"
              style={{
                padding: 24,
                textAlign: 'center',
                color: palette.subText,
                fontSize: 'clamp(12px, 1.6vw, 14px)',
              }}
            >
              {message}
            </div>
          ) : config.view === 'objectives' ? (
            <ObjectivesView
              items={currentObjectivePage}
              palette={palette}
              fresh={freshObjectives}
              strikeSeconds={Math.min(OBJECTIVE_STRIKE_SECONDS, config.highlightSeconds)}
            />
          ) : currentPage.length === 0 ? (
            <div
              style={{
                padding: 24,
                textAlign: 'center',
                color: palette.subText,
                fontSize: 'clamp(12px, 1.6vw, 14px)',
              }}
            >
              No completions yet.
            </div>
          ) : (
            currentPage.map((entry, idx) => (
              <ScoreRow
                key={entry.userId}
                entry={entry}
                index={idx}
                palette={palette}
                animation={animation}
                showProgress={config.showProgress}
                isFlashing={highlighted.has(rowKey(entry))}
              />
            ))
          )}
        </div>

        {config.view === 'games' &&
          (() => {
            const firstGame = currentPage[0]?.games[0]
            if (!firstGame) return null
            return (
              <div
                style={{
                  fontSize: 'clamp(10px, 1.4vw, 13px)',
                  color: palette.subText,
                  textAlign: 'center',
                  fontStyle: 'italic',
                }}
              >
                {firstGame.gameName}
              </div>
            )
          })()}
      </div>
    </div>
  )
}

/**
 * The objectives view's own progress: the shown competitor's completions
 * over the objectives on offer, under the title, since the checklist itself
 * only ever shows one page of them.
 */
function ObjectivesProgress({
  entry,
  palette,
  animation,
}: {
  entry: ScopedEntry
  palette: OverlayPalette
  animation: string
}) {
  const progress = entryProgress(entry)
  const color = progress.isTrial ? TRIAL_ACCENT_HEX : palette.accent
  return (
    <div data-testid="objectives-progress" style={{ display: 'grid', gap: 4 }}>
      <div
        style={{
          display: 'flex',
          justifyContent: 'space-between',
          gap: 8,
          color: progress.isTrial ? TRIAL_ACCENT_HEX : palette.subText,
          fontVariantNumeric: 'tabular-nums',
          fontSize: 'clamp(11px, 1.5vw, 14px)',
        }}
      >
        <span>
          {progress.isTrial && (
            <span style={{ fontWeight: 700, letterSpacing: '0.05em' }}>
              {TRIAL_BADGE_TEXT_OVERLAY}{' '}
            </span>
          )}
          {progress.completedCount}
          {progress.totalObjectives > 0 && ` / ${progress.totalObjectives}`} objectives
          {progress.failedCount > 0 && (
            <span style={{ color: '#ef4444' }}> ({progress.failedCount} failed)</span>
          )}
        </span>
        <span>{progress.percent}%</span>
      </div>
      <ProgressBar
        percent={progress.percent}
        palette={palette}
        color={color}
        animation={animation}
        label={`${entry.displayName} completion progress`}
      />
    </div>
  )
}
