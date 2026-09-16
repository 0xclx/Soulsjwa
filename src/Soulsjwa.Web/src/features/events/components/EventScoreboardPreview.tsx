import { memo } from 'react'
import { Link as RouterLink } from 'react-router-dom'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Chip from '@mui/material/Chip'
import Paper from '@mui/material/Paper'
import Stack from '@mui/material/Stack'
import Table from '@mui/material/Table'
import TableBody from '@mui/material/TableBody'
import TableCell from '@mui/material/TableCell'
import TableContainer from '@mui/material/TableContainer'
import TableHead from '@mui/material/TableHead'
import TableRow from '@mui/material/TableRow'
import Typography from '@mui/material/Typography'
import ScoreboardIcon from '@mui/icons-material/Leaderboard'
import { TRIAL_BADGE_TEXT, TRIAL_TOOLTIP } from '../scoreboard/trialPresentation'
import type { ScoreEntry } from '../../../types'

const ScoreRow = memo(function ScoreRow({ score, rank }: { score: ScoreEntry; rank: number }) {
  const isTop3 = rank <= 3
  return (
    <TableRow>
      <TableCell sx={{ fontWeight: isTop3 ? 700 : 400 }}>{rank}</TableCell>
      <TableCell>
        <Stack direction="row" spacing={0.5} sx={{ alignItems: 'center' }}>
          <Box
            sx={{
              width: 8,
              height: 8,
              borderRadius: '50%',
              bgcolor: score.isLive ? 'success.main' : 'error.main',
              flexShrink: 0,
            }}
            title={score.isLive ? 'Live' : 'Offline'}
          />
          <span>{score.displayName}</span>
        </Stack>
        {score.isFinished && <Chip label="Finished" size="small" color="success" sx={{ ml: 1 }} />}
        {score.isTrialing && (
          <Chip
            label={TRIAL_BADGE_TEXT}
            size="small"
            color="warning"
            variant="outlined"
            title={TRIAL_TOOLTIP}
            sx={{ ml: 1 }}
          />
        )}
      </TableCell>
      <TableCell align="right" sx={{ fontWeight: 600 }}>
        {score.totalScore}
      </TableCell>
      <TableCell align="right" sx={{ color: 'text.secondary' }}>
        {score.completedCount}
      </TableCell>
      <TableCell
        align="right"
        sx={{ color: score.failedCount > 0 ? 'error.main' : 'text.secondary' }}
      >
        {score.failedCount}
      </TableCell>
    </TableRow>
  )
})

interface EventScoreboardPreviewProps {
  eventId: string
  scores: ScoreEntry[]
}

/**
 * Compact scoreboard shown on the event overview tab, with a link through to
 * the full standalone scoreboard page.
 */
export const EventScoreboardPreview = ({ eventId, scores }: EventScoreboardPreviewProps) => (
  <Box component="section" aria-labelledby="scoreboard-heading">
    <Stack direction="row" spacing={2} sx={{ alignItems: 'center', mb: 1 }}>
      <Typography variant="h5" component="h2" id="scoreboard-heading">
        Scoreboard
      </Typography>
      <Button
        component={RouterLink}
        to={`/events/${eventId}/scoreboard`}
        size="small"
        variant="outlined"
        startIcon={<ScoreboardIcon />}
      >
        Full Scoreboard
      </Button>
    </Stack>
    <TableContainer component={Paper} variant="outlined" sx={{ borderRadius: 1 }}>
      <Table size="small" aria-label="Scoreboard">
        <TableHead>
          <TableRow>
            <TableCell>#</TableCell>
            <TableCell>Player</TableCell>
            <TableCell align="right">Score</TableCell>
            <TableCell align="right">Completed</TableCell>
            <TableCell align="right">Failed</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {scores.map((score, i) => (
            <ScoreRow key={score.userId} score={score} rank={i + 1} />
          ))}
        </TableBody>
      </Table>
    </TableContainer>
  </Box>
)
