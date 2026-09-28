import { Link as RouterLink } from 'react-router-dom'
import Button from '@mui/material/Button'
import Stack from '@mui/material/Stack'
import ScoreboardIcon from '@mui/icons-material/Leaderboard'
import { EventScoreboardView } from './scoreboard/EventScoreboardView'
import { EVENT_SECTION_NAV } from '../eventDetail/sections'
import type { EventResponse, User } from '../../../types'

const scoreboardPath = (eventUrlIdentifier: string) =>
  EVENT_SECTION_NAV.find((item) => item.section === 'scoreboard')?.to(eventUrlIdentifier) ?? ''

interface EventOverviewSectionProps {
  eventId: string
  /** Alias or id, for the link to the Scoreboard tab. */
  eventUrlIdentifier: string
  event: EventResponse
  currentUser: User | undefined
}

/**
 * Overview tab: the same three-state scoreboard as every other surface, with
 * every row, plus a way to the Scoreboard tab. OBS overlay tokens live in
 * their own dedicated tab.
 */
export const EventOverviewSection = ({
  eventId,
  eventUrlIdentifier,
  event,
  currentUser,
}: EventOverviewSectionProps) => (
  <Stack spacing={2}>
    <EventScoreboardView eventId={eventId} event={event} currentUser={currentUser} />
    <Button
      component={RouterLink}
      to={scoreboardPath(eventUrlIdentifier)}
      startIcon={<ScoreboardIcon />}
      sx={{ alignSelf: 'flex-start' }}
    >
      Full scoreboard
    </Button>
  </Stack>
)
