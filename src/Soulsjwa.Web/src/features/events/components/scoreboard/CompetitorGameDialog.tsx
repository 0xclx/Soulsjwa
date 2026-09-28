import { Fragment, useId } from 'react'
import Dialog from '@mui/material/Dialog'
import DialogContent from '@mui/material/DialogContent'
import DialogTitle from '@mui/material/DialogTitle'
import IconButton from '@mui/material/IconButton'
import Stack from '@mui/material/Stack'
import Table from '@mui/material/Table'
import TableBody from '@mui/material/TableBody'
import TableCell from '@mui/material/TableCell'
import TableRow from '@mui/material/TableRow'
import Typography from '@mui/material/Typography'
import useMediaQuery from '@mui/material/useMediaQuery'
import { useTheme } from '@mui/material/styles'
import CloseIcon from '@mui/icons-material/Close'
import { CompetitorInfosEditor } from '../CompetitorInfosEditor'
import { BreakdownColGroup, ScoreboardObjectiveRow } from './ScoreboardGameBreakdown'
import { TrialFigure } from './TrialFigure'
import { groupByCategory } from '../../scoreboard/scoreboardMetrics'
import {
  TRIAL_BADGE_TEXT,
  TRIAL_FIGURE_LABELS,
  TRIAL_MARKS_NOTICE,
} from '../../scoreboard/trialPresentation'
import type { GameBreakdown, ScoreboardEntry } from '../../../../types'

interface CompetitorGameDialogProps {
  open: boolean
  onClose: () => void
  entry: ScoreboardEntry
  game: GameBreakdown
  eventId: string
  /** From `canEditCompetitorInfo` for this viewer and competitor. */
  canEdit: boolean
  /**
   * Draw the marks as the trial's rather than the official record's. Decided
   * by the caller via `showsTrial`, which knows the view's scoring games.
   */
  showTrial: boolean
}

/**
 * One competitor's objectives for one game, opened from a standings row or a
 * whole-event cell. MUI's Dialog closes on Esc and backdrop click and returns
 * focus to the element that opened it.
 */
export const CompetitorGameDialog = ({
  open,
  onClose,
  entry,
  game,
  eventId,
  canEdit,
  showTrial,
}: CompetitorGameDialogProps) => {
  const titleId = useId()
  const theme = useTheme()
  const fullScreen = useMediaQuery(theme.breakpoints.down('sm'))
  const categoryGroups = groupByCategory(game.objectives)
  const showCategoryHeaders = categoryGroups.length > 1
  const showInfos = canEdit || game.infos.length > 0

  return (
    <Dialog
      open={open}
      onClose={onClose}
      aria-labelledby={titleId}
      fullWidth
      maxWidth="md"
      fullScreen={fullScreen}
    >
      <DialogTitle id={titleId} sx={{ pr: 6 }}>
        {entry.displayName} · {game.gameName}
      </DialogTitle>
      <IconButton
        aria-label="Close"
        onClick={onClose}
        sx={{ position: 'absolute', right: 8, top: 8 }}
      >
        <CloseIcon />
      </IconButton>
      <DialogContent dividers>
        <Stack spacing={2}>
          <Stack direction="row" spacing={2} sx={{ alignItems: 'baseline' }}>
            <Typography variant="body2" color="text.secondary">
              {game.completedCount}/{game.totalObjectives} objectives
              {game.failedCount > 0 && ` · ${game.failedCount} failed`} · {game.score} pts
            </Typography>
            {game.trial && (
              <TrialFigure
                value={game.trial.score}
                label={TRIAL_BADGE_TEXT}
                srLabel={TRIAL_FIGURE_LABELS.score}
              />
            )}
          </Stack>
          {showTrial && (
            <Typography variant="caption" sx={{ color: 'warning.main', fontWeight: 600 }}>
              {TRIAL_MARKS_NOTICE}
            </Typography>
          )}
          <Table size="small" sx={{ tableLayout: 'fixed' }} aria-label="Objectives">
            <BreakdownColGroup />
            <TableBody>
              {categoryGroups.map(([category, objectives]) => (
                <Fragment key={category}>
                  {showCategoryHeaders && (
                    <TableRow>
                      <TableCell
                        colSpan={4}
                        sx={{
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
                  {objectives.map((objective) => (
                    <ScoreboardObjectiveRow
                      key={objective.objectiveId}
                      objective={objective}
                      showTrial={showTrial}
                    />
                  ))}
                </Fragment>
              ))}
            </TableBody>
          </Table>
          {showInfos && (
            <CompetitorInfosEditor
              eventId={eventId}
              eventGameId={game.eventGameId}
              userId={entry.userId}
              canEdit={canEdit}
              initialInfos={game.infos}
            />
          )}
        </Stack>
      </DialogContent>
    </Dialog>
  )
}
