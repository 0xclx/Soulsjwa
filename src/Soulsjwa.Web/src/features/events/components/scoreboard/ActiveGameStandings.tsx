import { useMemo } from 'react'
import Avatar from '@mui/material/Avatar'
import Box from '@mui/material/Box'
import ButtonBase from '@mui/material/ButtonBase'
import Chip from '@mui/material/Chip'
import LinearProgress from '@mui/material/LinearProgress'
import Paper from '@mui/material/Paper'
import Stack from '@mui/material/Stack'
import Table from '@mui/material/Table'
import TableBody from '@mui/material/TableBody'
import TableCell from '@mui/material/TableCell'
import TableContainer from '@mui/material/TableContainer'
import TableHead from '@mui/material/TableHead'
import TableRow from '@mui/material/TableRow'
import Typography from '@mui/material/Typography'
import useMediaQuery from '@mui/material/useMediaQuery'
import { useTheme } from '@mui/material/styles'
import { LiveDot } from './LiveDot'
import { TrialFigure } from './TrialFigure'
import { TwitchLink } from './TwitchLink'
import {
  activeGameRows,
  gameIsDone,
  matchesCompetitorSearch,
  progressLabel,
  progressPercent,
  trialGames,
  type ActiveGameRow,
} from '../../scoreboard/scoreboardMetrics'
import {
  TRIAL_BADGE_TEXT,
  TRIAL_FIGURE_LABELS,
  TRIAL_TOOLTIP,
  trialBadgeLabel,
} from '../../scoreboard/trialPresentation'
import type { GameBreakdown, ScoreboardEntry } from '../../../../types'

const DONE_LABEL = 'Done'
const STANDINGS_LABEL = 'Current game standings'

interface ActiveGameStandingsProps {
  entries: readonly ScoreboardEntry[]
  /** The enabled game. Rows are ordered by the server's rank for it. */
  eventGameId: string
  /** Search text; rows that don't match are hidden, never renumbered. */
  query: string
  /** Opens a competitor's objectives for the game. */
  onSelect: (userId: string, eventGameId: string) => void
}

/** The current game view's table: one row per competitor, in game-rank order. */
export const ActiveGameStandings = ({
  entries,
  eventGameId,
  query,
  onSelect,
}: ActiveGameStandingsProps) => {
  const rows = useMemo(
    () =>
      activeGameRows(entries, eventGameId).filter((row) =>
        matchesCompetitorSearch(row.entry, query),
      ),
    [entries, eventGameId, query],
  )
  const theme = useTheme()
  const isWide = useMediaQuery(theme.breakpoints.up('md'))

  if (!isWide) {
    return (
      <Stack
        component="ol"
        spacing={1}
        aria-label={STANDINGS_LABEL}
        sx={{ listStyle: 'none', p: 0, m: 0 }}
      >
        {rows.map((row) => (
          <StandingsCard key={row.entry.userId} row={row} onSelect={onSelect} />
        ))}
      </Stack>
    )
  }

  return (
    <TableContainer component={Paper} variant="outlined">
      <Table aria-label={STANDINGS_LABEL}>
        <TableHead>
          <TableRow>
            <TableCell sx={{ width: 40 }}>#</TableCell>
            <TableCell>Player</TableCell>
            <TableCell sx={{ width: '30%' }}>Progress</TableCell>
            <TableCell align="right">Failed</TableCell>
            <TableCell align="right">Score</TableCell>
            <TableCell align="right">Event</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {rows.map((row) => (
            <StandingsRow key={row.entry.userId} row={row} onSelect={onSelect} />
          ))}
        </TableBody>
      </Table>
    </TableContainer>
  )
}

const StandingsRow = ({
  row: { entry, game },
  onSelect,
}: {
  row: ActiveGameRow
  onSelect: ActiveGameStandingsProps['onSelect']
}) => {
  const isTop3 = game.rank <= 3
  // Amber figures only for the game on screen; a trial elsewhere is a badge.
  const trial = game.trial

  return (
    // The whole row is a mouse target; the name is the keyboard one. It is a
    // native button, so Enter and Space click it, the click bubbles here, and
    // the row keeps its table semantics (role="button" on a <tr> would not).
    <TableRow
      hover
      onClick={() => onSelect(entry.userId, game.eventGameId)}
      sx={{ cursor: 'pointer' }}
    >
      <TableCell sx={{ fontWeight: isTop3 ? 700 : 400 }}>{game.rank}</TableCell>
      <TableCell>
        <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
          <LiveDot isLive={entry.isLive} />
          <Avatar
            src={entry.profileImageUrl ?? undefined}
            alt={entry.displayName}
            sx={{ width: 28, height: 28 }}
          />
          <NameButton entry={entry} game={game} bold={isTop3} />
          <StatusChips entry={entry} game={game} />
          <TwitchLink twitchLogin={entry.twitchLogin} displayName={entry.displayName} />
        </Stack>
      </TableCell>
      <TableCell>
        <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
          <LinearProgress
            variant="determinate"
            value={progressPercent(game.completedCount, game.totalObjectives)}
            aria-label={progressLabel(game.completedCount, game.totalObjectives)}
            sx={{ flex: 1, height: 8, borderRadius: 1 }}
          />
          <Typography variant="body2" color="text.secondary" aria-hidden sx={{ minWidth: 48 }}>
            {game.completedCount}/{game.totalObjectives}
          </Typography>
        </Stack>
        {trial && (
          <TrialFigure value={trial.completedCount} srLabel={TRIAL_FIGURE_LABELS.completed} />
        )}
      </TableCell>
      <TableCell align="right" sx={{ color: 'error.main' }}>
        {game.failedCount > 0 && (
          <Box component="span" aria-label={`${game.failedCount} failed`}>
            {game.failedCount}
          </Box>
        )}
        {trial && <TrialFigure value={trial.failedCount} srLabel={TRIAL_FIGURE_LABELS.failed} />}
      </TableCell>
      <TableCell align="right" sx={{ fontWeight: 600, fontSize: '1rem' }}>
        {game.score}
        {trial && (
          <TrialFigure
            value={trial.score}
            label={TRIAL_BADGE_TEXT}
            srLabel={TRIAL_FIGURE_LABELS.score}
          />
        )}
      </TableCell>
      <TableCell align="right">
        {entry.totalScore}
        <Box component="span" sx={{ color: 'text.secondary' }}>
          {' '}
          · #{entry.rank}
        </Box>
      </TableCell>
    </TableRow>
  )
}

/**
 * The keyboard target of a row or card. A native button, so Enter and Space
 * click it and the click bubbles to the row or card's own handler.
 */
const NameButton = ({
  entry,
  game,
  bold,
}: {
  entry: ScoreboardEntry
  game: GameBreakdown
  bold: boolean
}) => (
  <ButtonBase
    aria-label={`${entry.displayName}: ${game.gameName} objectives`}
    sx={{
      typography: 'body2',
      fontWeight: bold ? 600 : 400,
      borderRadius: 0.5,
      textAlign: 'left',
      minWidth: 0,
      '&.Mui-focusVisible': { outline: 2, outlineColor: 'primary.main' },
    }}
  >
    {entry.displayName}
  </ButtonBase>
)

/** "Done" for this game, and the trial badge (naming the game when it's another). */
const StatusChips = ({ entry, game }: { entry: ScoreboardEntry; game: GameBreakdown }) => {
  const trialing = trialGames(entry.games)
  return (
    <>
      {gameIsDone(game) && <Chip label={DONE_LABEL} size="small" color="success" />}
      {trialing.length > 0 && (
        <Chip
          label={trialBadgeLabel(trialing, game.eventGameId)}
          size="small"
          color="warning"
          variant="outlined"
          title={TRIAL_TOOLTIP}
        />
      )}
    </>
  )
}

/** The narrow-screen standing: rank, name, progress, game score and event total. */
const StandingsCard = ({
  row: { entry, game },
  onSelect,
}: {
  row: ActiveGameRow
  onSelect: ActiveGameStandingsProps['onSelect']
}) => {
  const isTop3 = game.rank <= 3
  return (
    <Paper
      component="li"
      variant="outlined"
      onClick={() => onSelect(entry.userId, game.eventGameId)}
      sx={{ p: 1.5, cursor: 'pointer', '&:hover': { bgcolor: 'action.hover' } }}
    >
      <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center', minWidth: 0 }}>
        <Typography
          data-testid="standings-rank"
          sx={{ fontWeight: isTop3 ? 700 : 400, minWidth: 24, textAlign: 'center' }}
        >
          {game.rank}
        </Typography>
        <Stack spacing={0.75} sx={{ flex: 1, minWidth: 0 }}>
          <Stack
            direction="row"
            spacing={1}
            sx={{ alignItems: 'center', flexWrap: 'wrap', rowGap: 0.5 }}
          >
            <LiveDot isLive={entry.isLive} />
            <NameButton entry={entry} game={game} bold={isTop3} />
            <StatusChips entry={entry} game={game} />
          </Stack>
          <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
            <LinearProgress
              variant="determinate"
              value={progressPercent(game.completedCount, game.totalObjectives)}
              aria-label={progressLabel(game.completedCount, game.totalObjectives)}
              sx={{ flex: 1, height: 6, borderRadius: 1 }}
            />
            <Typography variant="caption" color="text.secondary" aria-hidden>
              {game.completedCount}/{game.totalObjectives}
            </Typography>
          </Stack>
        </Stack>
        <Stack sx={{ alignItems: 'flex-end', flexShrink: 0 }}>
          <Typography data-testid="standings-score" sx={{ fontWeight: 600 }}>
            {game.score}
          </Typography>
          {game.trial && (
            <TrialFigure
              value={game.trial.score}
              label={TRIAL_BADGE_TEXT}
              srLabel={TRIAL_FIGURE_LABELS.score}
            />
          )}
          <Typography data-testid="standings-event" variant="caption" color="text.secondary">
            {entry.totalScore} · #{entry.rank}
          </Typography>
        </Stack>
      </Stack>
    </Paper>
  )
}
