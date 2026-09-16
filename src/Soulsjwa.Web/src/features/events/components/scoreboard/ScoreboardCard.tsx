import { memo, useState } from 'react'
import Avatar from '@mui/material/Avatar'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Chip from '@mui/material/Chip'
import Collapse from '@mui/material/Collapse'
import Grid from '@mui/material/Grid'
import IconButton from '@mui/material/IconButton'
import Stack from '@mui/material/Stack'
import Table from '@mui/material/Table'
import TableBody from '@mui/material/TableBody'
import Typography from '@mui/material/Typography'
import ExpandMoreIcon from '@mui/icons-material/ExpandMore'
import ExpandLessIcon from '@mui/icons-material/ExpandLess'
import { Surface } from '../../../../components/ui'
import { TwitchIcon } from './TwitchIcon'
import { BreakdownColGroup, ScoreboardGameBreakdown } from './ScoreboardGameBreakdown'
import { TrialFigure } from './TrialFigure'
import { canEditCompetitorInfo } from '../../eventDetail/permissions'
import { entryView, showsTrial } from '../../scoreboard/scoreboardMetrics'
import {
  TRIAL_BADGE_TEXT,
  TRIAL_FIGURE_LABELS,
  TRIAL_TOOLTIP,
  trialBadgeLabel,
} from '../../scoreboard/trialPresentation'
import type { EventResponse, ScoreboardEntry, User } from '../../../../types'

interface ScoreboardCardProps {
  entry: ScoreboardEntry
  eventId: string
  event: EventResponse | undefined
  currentUser: User | undefined
}

/** Mobile-friendly scoreboard entry card with an expandable game breakdown. */
export const ScoreboardCard = memo(function ScoreboardCard({
  entry,
  eventId,
  event,
  currentUser,
}: ScoreboardCardProps) {
  const [expanded, setExpanded] = useState(false)
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
    <Surface>
      <Stack spacing={2}>
        <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center' }}>
          <Typography variant="h5" color="primary.main">
            #{entry.rank}
          </Typography>
          <Avatar src={entry.profileImageUrl ?? undefined} alt={entry.displayName} />
          <Box sx={{ minWidth: 0, flex: 1 }}>
            <Typography variant="h6" noWrap>
              {entry.displayName}
            </Typography>
            <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
              <Box
                sx={{
                  width: 8,
                  height: 8,
                  borderRadius: '50%',
                  bgcolor: entry.isLive ? 'success.main' : 'error.main',
                }}
              />
              <Typography variant="caption" color="text.secondary">
                {entry.isLive ? 'Live' : 'Offline'}
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
            </Stack>
          </Box>
          <IconButton
            component="a"
            href={`https://twitch.tv/${entry.twitchLogin}`}
            target="_blank"
            rel="noopener noreferrer"
            aria-label={`Watch ${entry.displayName} on Twitch`}
            sx={{ color: 'primary.main' }}
          >
            <TwitchIcon />
          </IconButton>
        </Stack>
        <Grid container spacing={1.5}>
          <Grid size={4}>
            <Typography variant="caption" color="text.secondary">
              Total Completed
            </Typography>
            <Typography variant="h6">{entry.completedCount}</Typography>
            {trials && (
              <TrialFigure value={trials.completedCount} srLabel={TRIAL_FIGURE_LABELS.completed} />
            )}
          </Grid>
          <Grid size={4}>
            <Typography variant="caption" color="text.secondary">
              Total Failed
            </Typography>
            <Typography variant="h6" color={entry.failedCount > 0 ? 'error.main' : undefined}>
              {entry.failedCount}
            </Typography>
            {trials && (
              <TrialFigure value={trials.failedCount} srLabel={TRIAL_FIGURE_LABELS.failed} />
            )}
          </Grid>
          <Grid size={4}>
            <Typography variant="caption" color="text.secondary">
              Total Score
            </Typography>
            <Typography variant="h6">{entry.totalScore}</Typography>
            {trials && (
              <TrialFigure
                value={trials.score}
                label={TRIAL_BADGE_TEXT}
                srLabel={TRIAL_FIGURE_LABELS.score}
              />
            )}
          </Grid>
        </Grid>
        {hasBreakdown && (
          <Button
            onClick={() => setExpanded((value) => !value)}
            endIcon={expanded ? <ExpandLessIcon /> : <ExpandMoreIcon />}
            variant="outlined"
            size="small"
          >
            {expanded ? 'Hide breakdown' : 'Show breakdown'}
          </Button>
        )}
        <Collapse in={expanded && hasBreakdown} timeout="auto" unmountOnExit>
          <Table
            size="small"
            sx={{ tableLayout: 'fixed' }}
            aria-label={`${entry.displayName} objective breakdown`}
          >
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
      </Stack>
    </Surface>
  )
})
