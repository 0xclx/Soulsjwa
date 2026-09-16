import { Link as RouterLink, useLocation } from 'react-router-dom'
import Button from '@mui/material/Button'
import Chip from '@mui/material/Chip'
import Paper from '@mui/material/Paper'
import Stack from '@mui/material/Stack'
import Typography from '@mui/material/Typography'
import { EmptyState, ErrorMessage, LoadingState, PageHeader } from '../components/ui'
import { MyEventsPanel } from '../features/myEvents/components/MyEventsPanel'
import { MyEventsTabs } from '../features/myEvents/components/MyEventsTabs'
import { MyTrialRunsPanel } from '../features/myEvents/components/MyTrialRunsPanel'
import { useMyEvents } from '../features/myEvents/hooks/useMyEvents'
import { getMyEventsTab, type MyEventsTab } from '../features/myEvents/myEventsTabs'
import { MY_EVENT_ACTIVITY_LABELS, MY_EVENT_STATUS_LABELS, type MyOwnedEvent } from '../types'
import { getEventPath } from '../features/events/eventUrl'

const OwnedEventCard = ({ event }: { event: MyOwnedEvent }) => (
  <Paper variant="outlined" sx={{ p: 2 }}>
    <Stack
      direction={{ xs: 'column', sm: 'row' }}
      spacing={1.5}
      sx={{ alignItems: { sm: 'center' } }}
    >
      <Typography
        component={RouterLink}
        to={getEventPath(event.eventId, event.urlAlias)}
        sx={{ flex: 1, color: 'primary.main', fontWeight: 700 }}
      >
        {event.eventName}
      </Typography>
      <Chip
        label={MY_EVENT_STATUS_LABELS[event.status]}
        size="small"
        color={event.status === 'live' ? 'success' : 'default'}
        variant={event.status === 'live' ? 'filled' : 'outlined'}
      />
      <Typography variant="body2">
        {event.competitorCount} {event.competitorCount === 1 ? 'competitor' : 'competitors'}
      </Typography>
      {event.lastActivity && event.lastActivityType && (
        <Typography variant="caption" color="text.secondary">
          Last: {MY_EVENT_ACTIVITY_LABELS[event.lastActivityType]}
        </Typography>
      )}
    </Stack>
  </Paper>
)

export const MyEventsPage = () => {
  const { data, isLoading, isError } = useMyEvents()
  // Tab comes from the URL, not component state, so a refresh or a shared
  // link lands back on the same tab.
  const activeTab: MyEventsTab = getMyEventsTab(useLocation().pathname)

  if (isLoading) return <LoadingState label="Loading your events…" />
  if (isError || !data) return <ErrorMessage message="Failed to load your events." />

  // Trial runs exist per competitor, so the whole dashboard — the Trial tab
  // included — only has anything to show someone who is competing, delegated
  // for a competitor, or owning an event with them in it.
  const hasEvents = data.competitor.length + data.delegated.length + data.owned.length > 0

  return (
    <Stack spacing={3}>
      <PageHeader
        eyebrow="Dashboard"
        title="My Events"
        description="Track events where you compete, manage another competitor, or own the event."
      />
      {!hasEvents ? (
        <EmptyState
          title="You're not part of any events yet."
          description="Browse events to join one."
          action={
            <Button component={RouterLink} to="/events" variant="contained">
              Browse events
            </Button>
          }
        />
      ) : (
        <>
          <MyEventsTabs
            activeTab={activeTab}
            counts={{
              competing: data.competitor.length,
              delegated: data.delegated.length,
              owned: data.owned.length,
              // Trial runs come from their own query, loaded with the tab.
            }}
          />
          {activeTab === 'competing' && (
            <MyEventsPanel
              events={data.competitor}
              quickCompleteEnabled={data.quickCompleteEnabled}
              emptyMessage="You're not competing in any events."
            />
          )}
          {activeTab === 'delegated' && (
            <MyEventsPanel
              events={data.delegated}
              quickCompleteEnabled={data.quickCompleteEnabled}
              emptyMessage="You're not managing any other competitors."
            />
          )}
          {activeTab === 'owned' && (
            <Stack spacing={1.5}>
              {data.owned.length === 0 ? (
                <EmptyState title="You don't own any events." />
              ) : (
                data.owned.map((event) => <OwnedEventCard key={event.eventId} event={event} />)
              )}
            </Stack>
          )}
          {activeTab === 'trial' && <MyTrialRunsPanel />}
        </>
      )}
    </Stack>
  )
}
