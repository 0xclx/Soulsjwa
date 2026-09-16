import { useParams } from 'react-router-dom'
import Stack from '@mui/material/Stack'
import Typography from '@mui/material/Typography'
import { useEvent } from '../features/events/hooks/useEvent'
import { EventScoreboardView } from '../features/events/components/scoreboard/EventScoreboardView'
import { ErrorMessage, LoadingState } from '../components/ui'

/**
 * Public, chrome-free deep link to one event's scoreboard:
 * `/scoreboard/:eventIdentifier`, resolving by event id or UrlAlias
 * (useEvent already supports both). Rendered outside AppShell — no nav, no
 * tabs, no footer — so multiple parallel events can each hand out their own
 * shareable link.
 */
export const PublicScoreboardPage = () => {
  const { eventIdentifier } = useParams<{ eventIdentifier: string }>()
  const { data: event, isLoading, isError } = useEvent(eventIdentifier ?? '')

  if (isLoading) return <LoadingState label="Loading scoreboard…" />
  if (isError || !event) return <ErrorMessage message="Event not found." />

  return (
    <Stack spacing={3} sx={{ maxWidth: 960, mx: 'auto', p: { xs: 2, md: 4 } }}>
      <Typography component="h1" variant="h4">
        {event.name}
      </Typography>
      <EventScoreboardView eventId={event.id} event={event} currentUser={undefined} />
    </Stack>
  )
}
