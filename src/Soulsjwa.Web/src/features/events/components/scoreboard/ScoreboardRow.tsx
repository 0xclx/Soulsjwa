import { memo, useCallback, useState } from 'react'
import Avatar from '@mui/material/Avatar'
import Box from '@mui/material/Box'
import Chip from '@mui/material/Chip'
import Collapse from '@mui/material/Collapse'
import IconButton from '@mui/material/IconButton'
import Stack from '@mui/material/Stack'
import Table from '@mui/material/Table'
import TableBody from '@mui/material/TableBody'
import TableCell from '@mui/material/TableCell'
import TableRow from '@mui/material/TableRow'
import Typography from '@mui/material/Typography'
import ExpandMoreIcon from '@mui/icons-material/ExpandMore'
import ExpandLessIcon from '@mui/icons-material/ExpandLess'
import { TwitchIcon } from './TwitchIcon'
import { BreakdownColGroup, ScoreboardGameBreakdown } from './ScoreboardGameBreakdown'
import { canEditCompetitorInfo } from '../../eventDetail/permissions'
import { TrialFigure } from './TrialFigure'
import { entryView, showsTrial } from '../../scoreboard/scoreboardMetrics'
import {
  TRIAL_BADGE_TEXT,
  TRIAL_FIGURE_LABELS,
  TRIAL_TOOLTIP,
  trialBadgeLabel,
} from '../../scoreboard/trialPresentation'
import type { EventResponse, ScoreboardEntry, User } from '../../../../types'

interface ScoreboardRowProps {
  entry: ScoreboardEntry
  rank: number
  eventId: string
  event: EventResponse | undefined
  currentUser: User | undefined
}

/** A single expandable competitor row in the dense desktop scoreboard table. */
export const ScoreboardRow = memo(function ScoreboardRow({
  entry,
  rank,
  eventId,
  event,
  currentUser,
}: ScoreboardRowProps) {
  const [expanded, setExpanded] = useState(false)
  const toggle = useCallback(() => setExpanded((prev) => !prev), [])
  const isTop3 = rank <= 3
  const canEdit = canEditCompetitorInfo(event, currentUser, entry.userId)
  const {
    activeGame: game,
    scoringGameIds,
    trialing,
    trials,
    breakdownGames,
  } = entryView(entry.games)
  const hasBreakdown = breakdownGames.length > 0

  return (
    <>
      <TableRow
        hover={hasBreakdown}
        onClick={hasBreakdown ? toggle : undefined}
        sx={{
          cursor: hasBreakdown ? 'pointer' : 'default',
          '& > *': { borderBottom: expanded ? 'unset' : undefined },
        }}
      >
        <TableCell sx={{ fontWeight: isTop3 ? 700 : 400, width: 40 }}>{rank}</TableCell>
        <TableCell>
          <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
            <Box
              sx={{
                width: 10,
                height: 10,
                borderRadius: '50%',
                bgcolor: entry.isLive ? 'success.main' : 'error.main',
                flexShrink: 0,
              }}
              title={entry.isLive ? 'Live' : 'Offline'}
            />
            <Avatar
              src={entry.profileImageUrl ?? undefined}
              alt={entry.displayName}
              sx={{ width: 28, height: 28 }}
            />
            <Typography variant="body2" sx={{ fontWeight: isTop3 ? 600 : 400 }}>
              {entry.displayName}
            </Typography>
            {entry.isFinished && <Chip label="Finished" size="small" color="success" />}
            {trials && (
              <Chip
                label={trialBadgeLabel(trialing, game?.eventGameId)}
                size="small"
                color="warning"
                variant="outlined"
                title={TRIAL_TOOLTIP}
              />
            )}
            <IconButton
              component="a"
              href={`https://twitch.tv/${entry.twitchLogin}`}
              target="_blank"
              rel="noopener noreferrer"
              size="small"
              title={`Watch ${entry.displayName} on Twitch`}
              onClick={(e: React.MouseEvent) => e.stopPropagation()}
              sx={{ color: 'primary.main' }}
            >
              <TwitchIcon />
            </IconButton>
          </Stack>
        </TableCell>
        <TableCell align="right" sx={{ color: 'text.secondary' }}>
          {entry.completedCount}
          {trials && (
            <TrialFigure value={trials.completedCount} srLabel={TRIAL_FIGURE_LABELS.completed} />
          )}
        </TableCell>
        <TableCell
          align="right"
          sx={{ color: entry.failedCount > 0 ? 'error.main' : 'text.secondary' }}
        >
          {entry.failedCount}
          {trials && (
            <TrialFigure value={trials.failedCount} srLabel={TRIAL_FIGURE_LABELS.failed} />
          )}
        </TableCell>
        <TableCell align="right" sx={{ fontWeight: 600, fontSize: '1rem' }}>
          {entry.totalScore}
          {trials && (
            <TrialFigure
              value={trials.score}
              label={TRIAL_BADGE_TEXT}
              srLabel={TRIAL_FIGURE_LABELS.score}
            />
          )}
        </TableCell>
        <TableCell align="center" sx={{ width: 40 }}>
          {hasBreakdown &&
            (expanded ? <ExpandLessIcon fontSize="small" /> : <ExpandMoreIcon fontSize="small" />)}
        </TableCell>
      </TableRow>
      <TableRow>
        <TableCell colSpan={6} sx={{ p: 0, border: 0 }}>
          <Collapse in={expanded} timeout="auto" unmountOnExit>
            <Table size="small" sx={{ bgcolor: 'background.default', tableLayout: 'fixed' }}>
              <BreakdownColGroup />
              <TableBody>
                {breakdownGames.map((breakdownGame) => (
                  <ScoreboardGameBreakdown
                    key={breakdownGame.eventGameId}
                    game={breakdownGame}
                    eventId={eventId}
                    competitorUserId={entry.userId}
                    canEdit={canEdit}
                    showTrial={showsTrial(breakdownGame, scoringGameIds)}
                  />
                ))}
              </TableBody>
            </Table>
          </Collapse>
        </TableCell>
      </TableRow>
    </>
  )
})
