import { useId, useMemo } from 'react'
import Avatar from '@mui/material/Avatar'
import Box from '@mui/material/Box'
import Chip from '@mui/material/Chip'
import Stack from '@mui/material/Stack'
import Typography from '@mui/material/Typography'
import { Surface } from '../../../../components/ui'
import { LiveDot } from './LiveDot'
import { TwitchLink } from './TwitchLink'
import type { EventCompetitor, EventGame, EventResponse } from '../../../../types'

const PRE_START_TEXT = {
  games: 'Games',
  competitors: 'Competitors',
  noGames: 'No games added yet',
  noCompetitors: 'No competitors yet',
  custom: 'Custom',
} as const

const plural = (count: number, one: string, many: string) => `${count} ${count === 1 ? one : many}`

const listSx = { listStyle: 'none', p: 0, m: 0 } as const

interface PreStartOverviewProps {
  event: EventResponse
}

/**
 * What an unstarted event shows in place of the scoreboard: the games to be
 * played and who is playing them. Built from the event alone — there are no
 * scores to fetch yet.
 */
export const PreStartOverview = ({ event }: PreStartOverviewProps) => {
  const competitors = useMemo(
    () => [...event.competitors].sort((a, b) => a.displayName.localeCompare(b.displayName)),
    [event.competitors],
  )

  return (
    <Stack spacing={3}>
      <OverviewSection title={PRE_START_TEXT.games}>
        {event.games.length === 0 ? (
          <EmptyLine text={PRE_START_TEXT.noGames} />
        ) : (
          <Stack component="ul" spacing={1} sx={listSx}>
            {event.games.map((game) => (
              <GameLine key={game.eventGameId} game={game} />
            ))}
          </Stack>
        )}
      </OverviewSection>
      <OverviewSection title={PRE_START_TEXT.competitors}>
        {competitors.length === 0 ? (
          <EmptyLine text={PRE_START_TEXT.noCompetitors} />
        ) : (
          <Box
            component="ul"
            sx={{
              ...listSx,
              display: 'grid',
              gap: 1,
              gridTemplateColumns: { xs: '1fr', sm: 'repeat(2, 1fr)', md: 'repeat(3, 1fr)' },
            }}
          >
            {competitors.map((competitor) => (
              <CompetitorLine key={competitor.userId} competitor={competitor} />
            ))}
          </Box>
        )}
      </OverviewSection>
    </Stack>
  )
}

const OverviewSection = ({ title, children }: { title: string; children: React.ReactNode }) => {
  const headingId = useId()
  return (
    <Box component="section" aria-labelledby={headingId}>
      <Surface>
        <Typography id={headingId} component="h3" variant="h6" sx={{ mb: 1.5 }}>
          {title}
        </Typography>
        {children}
      </Surface>
    </Box>
  )
}

const EmptyLine = ({ text }: { text: string }) => (
  <Typography color="text.secondary">{text}</Typography>
)

const GameLine = ({ game }: { game: EventGame }) => {
  const points = game.objectives.reduce((total, objective) => total + objective.score, 0)
  return (
    <Stack
      component="li"
      direction="row"
      spacing={1}
      sx={{ alignItems: 'center', flexWrap: 'wrap', rowGap: 0.5 }}
    >
      <Typography sx={{ fontWeight: 600 }}>{game.gameName}</Typography>
      {game.isCustomGame && <Chip label={PRE_START_TEXT.custom} size="small" variant="outlined" />}
      <Typography variant="body2" color="text.secondary">
        {plural(game.objectives.length, 'objective', 'objectives')} ·{' '}
        {plural(points, 'point', 'points')}
      </Typography>
    </Stack>
  )
}

const CompetitorLine = ({ competitor }: { competitor: EventCompetitor }) => (
  <Stack component="li" direction="row" spacing={1} sx={{ alignItems: 'center', minWidth: 0 }}>
    <LiveDot isLive={competitor.isLive} />
    <Avatar
      src={competitor.profileImageUrl ?? undefined}
      alt={competitor.displayName}
      sx={{ width: 28, height: 28 }}
    />
    <Typography data-testid="competitor-name" noWrap sx={{ minWidth: 0 }}>
      {competitor.displayName}
    </Typography>
    <TwitchLink twitchLogin={competitor.twitchLogin} displayName={competitor.displayName} />
  </Stack>
)
