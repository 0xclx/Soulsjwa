import Stack from '@mui/material/Stack'
import Typography from '@mui/material/Typography'
import { useEventRoute } from '../features/events/hooks/useEventRoute'
import { EventScoreboardView } from '../features/events/components/scoreboard/EventScoreboardView'
import { LiveStatusLegend } from '../features/events/components/scoreboard/LiveStatusLegend'

const SCOREBOARD_DESCRIPTION =
  'Select a competitor to inspect scores, objective progress, clips, links, and notes.'

export const EventScoreboardPage = () => {
  const { eventId, event, currentUser } = useEventRoute()

  return (
    <Stack spacing={3}>
      <section aria-labelledby="scoreboard-heading">
        <Typography id="scoreboard-heading" component="h2" variant="h2">
          Scoreboard
        </Typography>
        <Typography color="text.secondary" sx={{ mt: 1 }}>
          {SCOREBOARD_DESCRIPTION}
        </Typography>
      </section>

      <EventScoreboardView eventId={eventId} event={event} currentUser={currentUser} />

      <LiveStatusLegend />
    </Stack>
  )
}
