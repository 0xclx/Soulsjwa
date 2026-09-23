import { useMemo } from 'react'
import Avatar from '@mui/material/Avatar'
import Box from '@mui/material/Box'
import ButtonBase from '@mui/material/ButtonBase'
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
import useMediaQuery from '@mui/material/useMediaQuery'
import { useTheme } from '@mui/material/styles'
import { LiveDot } from './LiveDot'
import { TrialFigure } from './TrialFigure'
import { TwitchLink } from './TwitchLink'
import { gameHasActivity, matchesCompetitorSearch } from '../../scoreboard/scoreboardMetrics'
import { TRIAL_BADGE_TEXT, TRIAL_FIGURE_LABELS } from '../../scoreboard/trialPresentation'
import type { EventGame, GameBreakdown, ScoreboardEntry } from '../../../../types'

const MATRIX_LABEL = 'Whole event standings'
const NOW_PLAYING_LABEL = 'Now playing'
const FINISHED_LABEL = 'Finished'
/** Shown in place of a per-game rank before anyone has a result in the game. */
const NO_RANK = '—'

interface EventGameMatrixProps {
  entries: readonly ScoreboardEntry[]
  /** The event's games, in the order the columns appear. */
  games: readonly EventGame[]
  /** Search text; rows that don't match are hidden, never renumbered. */
  query: string
  /** Opens a competitor's objectives for one game. */
  onSelect: (userId: string, eventGameId: string) => void
}

/**
 * The whole event view: one row per competitor in event-rank order, one
 * column per game. Every rank shown is the server's.
 */
export const EventGameMatrix = ({ entries, games, query, onSelect }: EventGameMatrixProps) => {
  const rows = useMemo(
    () =>
      entries
        .filter((entry) => matchesCompetitorSearch(entry, query))
        .sort((a, b) => a.rank - b.rank),
    [entries, query],
  )
  // Before anyone has a result, every per-game rank is a tie — and under
  // SharedPlace that labels every cell #1, which means nothing. Judged over
  // every competitor, not just the ones the search leaves.
  const rankedGameIds = useMemo(
    () =>
      new Set(
        games
          .filter((game) => gameHasActivity(entries, game.eventGameId))
          .map((game) => game.eventGameId),
      ),
    [entries, games],
  )
  const theme = useTheme()
  const isWide = useMediaQuery(theme.breakpoints.up('md'))

  if (!isWide) {
    return (
      <Stack
        component="ol"
        spacing={1}
        aria-label={MATRIX_LABEL}
        sx={{ listStyle: 'none', p: 0, m: 0 }}
      >
        {rows.map((entry) => (
          <Paper
            key={entry.userId}
            component="li"
            variant="outlined"
            aria-label={entry.displayName}
            sx={{ p: 1.5 }}
          >
            <Stack direction="row" spacing={1} sx={{ alignItems: 'center', mb: 1, minWidth: 0 }}>
              <LiveDot isLive={entry.isLive} />
              <Typography sx={{ fontWeight: 600, flex: 1, minWidth: 0 }} noWrap>
                {entry.displayName}
              </Typography>
              {entry.isFinished && <Chip label={FINISHED_LABEL} size="small" color="success" />}
              <Typography variant="body2" sx={{ flexShrink: 0 }}>
                {entry.totalScore} · #{entry.rank}
              </Typography>
            </Stack>
            <Stack spacing={0.5}>
              {games.map((eventGame) => {
                const game = entry.games.find((g) => g.eventGameId === eventGame.eventGameId)
                return (
                  game && (
                    <GameCell
                      key={eventGame.eventGameId}
                      displayName={entry.displayName}
                      game={game}
                      ranked={rankedGameIds.has(game.eventGameId)}
                      onClick={() => onSelect(entry.userId, game.eventGameId)}
                      layout="line"
                    />
                  )
                )
              })}
            </Stack>
          </Paper>
        ))}
      </Stack>
    )
  }

  return (
    <TableContainer component={Paper} variant="outlined">
      <Table aria-label={MATRIX_LABEL} size="small">
        <TableHead>
          <TableRow>
            <TableCell sx={{ width: 40 }}>#</TableCell>
            <TableCell>Player</TableCell>
            {games.map((game) => (
              <TableCell key={game.eventGameId} align="center">
                {game.gameName}
                {game.isEnabled && (
                  <Chip
                    label={NOW_PLAYING_LABEL}
                    size="small"
                    color="success"
                    variant="outlined"
                    sx={{ ml: 1 }}
                  />
                )}
              </TableCell>
            ))}
            <TableCell align="right">Total</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {rows.map((entry) => (
            <TableRow key={entry.userId}>
              <TableCell sx={{ fontWeight: entry.rank <= 3 ? 700 : 400 }}>{entry.rank}</TableCell>
              <TableCell>
                <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
                  <LiveDot isLive={entry.isLive} />
                  <Avatar
                    src={entry.profileImageUrl ?? undefined}
                    alt={entry.displayName}
                    sx={{ width: 24, height: 24 }}
                  />
                  <Typography variant="body2" noWrap>
                    {entry.displayName}
                  </Typography>
                  {entry.isFinished && <Chip label={FINISHED_LABEL} size="small" color="success" />}
                  <TwitchLink twitchLogin={entry.twitchLogin} displayName={entry.displayName} />
                </Stack>
              </TableCell>
              {games.map((eventGame) => {
                const game = entry.games.find((g) => g.eventGameId === eventGame.eventGameId)
                return (
                  <TableCell key={eventGame.eventGameId} align="center" sx={{ p: 0.5 }}>
                    {game ? (
                      <GameCell
                        displayName={entry.displayName}
                        game={game}
                        ranked={rankedGameIds.has(game.eventGameId)}
                        onClick={() => onSelect(entry.userId, game.eventGameId)}
                      />
                    ) : (
                      NO_RANK
                    )}
                  </TableCell>
                )
              })}
              <TableCell align="right" sx={{ fontWeight: 600 }}>
                {entry.totalScore}
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </TableContainer>
  )
}

interface GameCellProps {
  displayName: string
  game: GameBreakdown
  ranked: boolean
  onClick: () => void
  /** A stacked table cell, or one full-width line of a narrow-screen card. */
  layout?: 'cell' | 'line'
}

const GameCell = ({ displayName, game, ranked, onClick, layout = 'cell' }: GameCellProps) => {
  const isLine = layout === 'line'
  return (
    <ButtonBase
      onClick={onClick}
      aria-label={`${displayName}: ${game.gameName} objectives`}
      sx={{
        display: 'flex',
        flexDirection: isLine ? 'row' : 'column',
        justifyContent: isLine ? 'space-between' : 'center',
        alignItems: isLine ? 'baseline' : 'center',
        gap: isLine ? 1 : 0,
        width: '100%',
        px: 1,
        py: 0.5,
        borderRadius: 1,
        textAlign: 'left',
        '&:hover': { bgcolor: 'action.hover' },
        '&.Mui-focusVisible': { outline: 2, outlineColor: 'primary.main' },
      }}
    >
      {isLine && (
        <Typography variant="body2" noWrap sx={{ flex: 1, minWidth: 0 }}>
          {game.gameName}
          {game.isEnabled && (
            <Box component="span" sx={{ color: 'success.main', ml: 1, typography: 'caption' }}>
              {NOW_PLAYING_LABEL}
            </Box>
          )}
        </Typography>
      )}
      <Typography variant="body2" sx={{ fontWeight: 600 }}>
        {game.score}
      </Typography>
      <Typography variant="caption" color="text.secondary" sx={{ whiteSpace: 'nowrap' }}>
        {game.completedCount}/{game.totalObjectives}
        <Box component="span" sx={{ ml: 0.75 }}>
          {ranked ? `#${game.rank}` : NO_RANK}
        </Box>
      </Typography>
      {game.trial && (
        <TrialFigure
          value={game.trial.score}
          label={TRIAL_BADGE_TEXT}
          srLabel={TRIAL_FIGURE_LABELS.score}
        />
      )}
    </ButtonBase>
  )
}
