import { Fragment, memo, useState } from 'react'
import Box from '@mui/material/Box'
import Collapse from '@mui/material/Collapse'
import Stack from '@mui/material/Stack'
import Table from '@mui/material/Table'
import TableBody from '@mui/material/TableBody'
import TableCell from '@mui/material/TableCell'
import TableRow from '@mui/material/TableRow'
import Typography from '@mui/material/Typography'
import { visuallyHidden } from '@mui/utils'
import CancelIcon from '@mui/icons-material/Cancel'
import CheckCircleIcon from '@mui/icons-material/CheckCircle'
import ExpandLessIcon from '@mui/icons-material/ExpandLess'
import ExpandMoreIcon from '@mui/icons-material/ExpandMore'
import RadioButtonUncheckedIcon from '@mui/icons-material/RadioButtonUnchecked'
import { CompetitorInfosEditor } from '../CompetitorInfosEditor'
import { TrialFigure } from './TrialFigure'
import { gameLastCompletedAt, objectiveState } from '../../scoreboard/scoreboardMetrics'
import {
  TRIAL_BADGE_TEXT,
  TRIAL_FIGURE_LABELS,
  TRIAL_MARKS_NOTICE,
  TRIAL_TOOLTIP,
} from '../../scoreboard/trialPresentation'
import type { GameBreakdown, ObjectiveDetail } from '../../../../types'

/** Fallback group label for objectives that have no category set. */
const UNCATEGORIZED_LABEL = 'Other'

/**
 * Each breakdown table is a separate `<table>`, so the browser sizes its
 * columns from its own content alone — one extra element (the death-clip
 * skull, say) shifts that table out of line with every other. Rendering this
 * exact `<colgroup>` plus `table-layout: fixed` in all of them pins the
 * indicator/name/score/date widths so rows line up regardless of content.
 */
export const BreakdownColGroup = () => (
  <colgroup>
    <col style={{ width: 40 }} />
    <col />
    <col style={{ width: 110 }} />
    <col style={{ width: 200 }} />
  </colgroup>
)

/**
 * Groups a game's objectives by category, preserving the first-seen order of
 * both the categories and the objectives within each — same grouping as the
 * OBS overlay's "objectives" view, so the in-app breakdown and the overlay
 * never disagree about how objectives are organized.
 */
function groupByCategory(objectives: ObjectiveDetail[]): Array<[string, ObjectiveDetail[]]> {
  const byCategory = new Map<string, ObjectiveDetail[]>()
  for (const objective of objectives) {
    const category = objective.category?.trim() || UNCATEGORIZED_LABEL
    const bucket = byCategory.get(category)
    if (bucket) bucket.push(objective)
    else byCategory.set(category, [objective])
  }
  return Array.from(byCategory)
}

/** One objective line inside an expanded competitor's per-game breakdown. */
export const ScoreboardObjectiveRow = memo(function ScoreboardObjectiveRow({
  objective,
  showTrial = false,
}: {
  objective: ObjectiveDetail
  showTrial?: boolean
}) {
  const state = objectiveState(objective, showTrial)
  return (
    <TableRow>
      <TableCell align="center">
        {state.isFailed ? (
          <CancelIcon fontSize="small" color="error" />
        ) : state.isCompleted ? (
          <CheckCircleIcon fontSize="small" color="success" />
        ) : (
          <RadioButtonUncheckedIcon fontSize="small" color="disabled" />
        )}
      </TableCell>
      <TableCell>{objective.name}</TableCell>
      <TableCell align="right">{objective.score} pts</TableCell>
      <TableCell align="right" sx={{ color: 'text.secondary', fontSize: '0.8rem' }}>
        {state.isFailed
          ? state.failedAt
            ? new Date(state.failedAt).toLocaleString()
            : '—'
          : state.completedAt
            ? new Date(state.completedAt).toLocaleString()
            : '—'}
      </TableCell>
    </TableRow>
  )
})

/**
 * A game group inside an expanded scoreboard entry: the game header row, each
 * objective, and the competitor-info editor when the viewer may edit or infos
 * already exist.
 */
export const ScoreboardGameBreakdown = memo(function ScoreboardGameBreakdown({
  game,
  eventId,
  competitorUserId,
  canEdit,
  showTrial = false,
}: {
  game: GameBreakdown
  eventId: string
  competitorUserId: string
  canEdit: boolean
  /**
   * Draw this game's marks and last-completed as the trial's rather than the
   * official record's. Decided by the parent via `showsTrial`, because only it
   * knows which games the surrounding figures are scoring — see
   * `scoreboardMetrics.showsTrial`.
   */
  showTrial?: boolean
}) {
  // Games still being actively played are the ones a viewer most likely
  // wants to see the detail of right away; finished/inactive games start
  // folded to keep a multi-game entry scannable.
  // A trialing game starts expanded too — it's the run the competitor is
  // actively playing, even when it isn't the event's enabled game.
  const [expanded, setExpanded] = useState(game.isEnabled || game.trial !== null)
  const showInfosSection = canEdit || game.infos.length > 0
  const lastCompletedAt = showTrial
    ? (game.trial?.lastCompletedAt ?? null)
    : gameLastCompletedAt(game)
  const deathClipUrl = game.infos.find((info) => info.type === 'DeathClip' && info.url)?.url
  const categoryGroups = groupByCategory(game.objectives)
  const showCategoryHeaders = categoryGroups.length > 1
  return (
    <>
      <TableRow
        hover
        onClick={() => setExpanded((value) => !value)}
        sx={{ bgcolor: 'action.hover', cursor: 'pointer' }}
      >
        <TableCell align="center" sx={{ verticalAlign: 'top' }}>
          {expanded ? <ExpandLessIcon fontSize="small" /> : <ExpandMoreIcon fontSize="small" />}
        </TableCell>
        <TableCell sx={{ fontWeight: 600, fontSize: '0.85rem' }}>
          <Stack direction="row" spacing={0.5} sx={{ alignItems: 'center' }}>
            <span>{game.gameName}</span>
            {game.hasDeathClip &&
              (deathClipUrl ? (
                <a
                  href={deathClipUrl}
                  target="_blank"
                  rel="noopener noreferrer"
                  aria-label="Watch death clip"
                  title="Watch the death clip"
                  onClick={(e) => e.stopPropagation()}
                  style={{ lineHeight: 1 }}
                >
                  💀
                </a>
              ) : (
                <span
                  role="img"
                  aria-label="Death recorded"
                  title="A death clip was recorded for this run"
                >
                  💀
                </span>
              ))}
          </Stack>
          <Typography variant="caption" color="text.secondary">
            {game.completedCount}/{game.totalObjectives} objectives
            {game.failedCount > 0 && ` (${game.failedCount} failed)`}
          </Typography>
          {game.trial && (
            <Typography
              variant="caption"
              title={TRIAL_TOOLTIP}
              sx={{ color: 'warning.main', display: 'block' }}
            >
              {game.trial.completedCount}/{game.totalObjectives} in trial
              {game.trial.failedCount > 0 && ` (${game.trial.failedCount} failed)`}
            </Typography>
          )}
        </TableCell>
        <TableCell align="right" sx={{ fontWeight: 600, verticalAlign: 'top' }}>
          {game.score} pts
          {game.trial && (
            <TrialFigure
              value={game.trial.score}
              label={TRIAL_BADGE_TEXT}
              srLabel={TRIAL_FIGURE_LABELS.score}
            />
          )}
        </TableCell>
        <TableCell
          align="right"
          sx={{
            // Amber when substituted: the counts in the cell to the left stay
            // official, so an unmarked trial timestamp here reads as the date
            // of those official completions.
            color: showTrial ? 'warning.main' : 'text.secondary',
            fontSize: '0.8rem',
            verticalAlign: 'top',
          }}
          title={showTrial ? TRIAL_TOOLTIP : undefined}
        >
          {showTrial && (
            <Box component="span" sx={visuallyHidden}>
              {TRIAL_FIGURE_LABELS.lastCompleted}:{' '}
            </Box>
          )}
          {lastCompletedAt ? new Date(lastCompletedAt).toLocaleString() : '—'}
        </TableCell>
      </TableRow>
      <TableRow>
        <TableCell colSpan={4} sx={{ p: 0, border: 0 }}>
          <Collapse in={expanded} timeout="auto" unmountOnExit>
            <Table size="small" sx={{ tableLayout: 'fixed' }}>
              <BreakdownColGroup />
              <TableBody>
                {showTrial && (
                  <TableRow>
                    <TableCell colSpan={4} sx={{ pl: 6, py: 0.5, border: 0 }}>
                      <Typography variant="caption" sx={{ color: 'warning.main', fontWeight: 600 }}>
                        {TRIAL_MARKS_NOTICE}
                      </Typography>
                    </TableCell>
                  </TableRow>
                )}
                {categoryGroups.map(([category, objectives]) => (
                  <Fragment key={category}>
                    {showCategoryHeaders && (
                      <TableRow>
                        <TableCell
                          colSpan={4}
                          sx={{
                            pl: 6,
                            py: 0.5,
                            fontWeight: 600,
                            fontSize: '0.75rem',
                            color: 'text.secondary',
                          }}
                        >
                          {category}
                        </TableCell>
                      </TableRow>
                    )}
                    {objectives.map((obj) => (
                      <ScoreboardObjectiveRow
                        key={obj.objectiveId}
                        objective={obj}
                        showTrial={showTrial}
                      />
                    ))}
                  </Fragment>
                ))}
                {showInfosSection && (
                  <TableRow>
                    <TableCell colSpan={4} sx={{ pl: 6, py: 1, bgcolor: 'background.default' }}>
                      <CompetitorInfosEditor
                        eventId={eventId}
                        eventGameId={game.eventGameId}
                        userId={competitorUserId}
                        canEdit={canEdit}
                        initialInfos={game.infos}
                      />
                    </TableCell>
                  </TableRow>
                )}
              </TableBody>
            </Table>
          </Collapse>
        </TableCell>
      </TableRow>
    </>
  )
})
