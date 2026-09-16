import { Link as RouterLink } from 'react-router-dom'
import Button from '@mui/material/Button'
import Chip from '@mui/material/Chip'
import Stack from '@mui/material/Stack'
import Typography from '@mui/material/Typography'
import { useFeaturedEvent } from '../features/events/hooks/useFeaturedEvent'
import { EventScoreboardView } from '../features/events/components/scoreboard/EventScoreboardView'
import { PageHeader } from '../components/ui/PageHeader'
import { getEventPath } from '../features/events/eventUrl'
import type { EventResponse } from '../types'

/**
 * Every visitor — signed in or not — lands here and sees the featured
 * scoreboard when one exists; authenticated users go to their
 * own dashboard via the "My Events" nav entry (`/my-events`) instead of
 * being redirected away from this page.
 */
export const HomePage = () => {
  const { data: featuredEvent, isLoading, isError } = useFeaturedEvent()
  const hasFeaturedEvent = !isLoading && !isError && !!featuredEvent

  // Once an event is featured, its scoreboard carries the page on its own —
  // the generic marketing hero would just be redundant clutter above it.
  if (hasFeaturedEvent) {
    return (
      <Stack spacing={3}>
        <FeaturedEventHeader event={featuredEvent} />
        <EventScoreboardView
          eventId={featuredEvent.id}
          event={featuredEvent}
          currentUser={undefined}
        />
      </Stack>
    )
  }

  return (
    <Stack spacing={4} sx={{ maxWidth: '70ch' }}>
      <header>
        <Typography
          component="h1"
          sx={{
            fontSize: { xs: '2rem', md: '2.5rem' },
            lineHeight: { xs: 1.2, md: 1.15 },
            fontWeight: 700,
          }}
        >
          Soulsjwa events
        </Typography>
        <Typography variant="body1" color="text.secondary" sx={{ mt: 2 }}>
          Browse Souls events, follow live scoreboards, and manage event objectives with your Twitch
          account.
        </Typography>
      </header>

      <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2}>
        <Button component={RouterLink} to="/events" variant="contained" size="large">
          Browse events
        </Button>
      </Stack>
    </Stack>
  )
}

const FeaturedEventHeader = ({ event }: { event: EventResponse }) => {
  const activeGames = event.games.filter((game) => game.isEnabled)

  return (
    <PageHeader
      eyebrow="Featured event"
      title={
        <Button
          component={RouterLink}
          to={getEventPath(event.id, event.urlAlias)}
          color="inherit"
          sx={{ p: 0, minWidth: 0, textTransform: 'none', font: 'inherit', textAlign: 'left' }}
        >
          {event.name}
        </Button>
      }
      description={event.description || 'No description provided yet.'}
      meta={
        <Stack spacing={1.5}>
          <Stack
            direction="row"
            spacing={1}
            sx={{ alignItems: 'center', flexWrap: 'wrap', gap: 1 }}
          >
            <Chip
              label={event.isStarted ? 'Live' : 'Not started'}
              color={event.isStarted ? 'success' : 'default'}
              size="small"
            />
            <Typography variant="caption" color="text.disabled">
              {activeGames.length} active {activeGames.length === 1 ? 'game' : 'games'}
            </Typography>
          </Stack>
          {activeGames.length > 0 && (
            <Stack direction="row" spacing={1} sx={{ flexWrap: 'wrap', gap: 1 }}>
              {activeGames.map((game) => (
                <Chip
                  key={game.eventGameId}
                  label={game.gameName}
                  size="small"
                  variant="outlined"
                />
              ))}
            </Stack>
          )}
        </Stack>
      }
    />
  )
}
