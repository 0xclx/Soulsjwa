import { useState } from 'react'
import { Link as RouterLink } from 'react-router-dom'
import Alert from '@mui/material/Alert'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Chip from '@mui/material/Chip'
import CircularProgress from '@mui/material/CircularProgress'
import List from '@mui/material/List'
import ListItemButton from '@mui/material/ListItemButton'
import Paper from '@mui/material/Paper'
import Stack from '@mui/material/Stack'
import Typography from '@mui/material/Typography'
import { EmptyState } from '../../../components/ui'
import { MyEventObjectiveList } from './MyEventObjectiveList'
import { useMyEventObjectives } from '../hooks/useMyEventObjectives'
import { useToggleMyEventObjective } from '../hooks/useToggleMyEventObjective'
import { useToggleMyEventObjectiveFailure } from '../hooks/useToggleMyEventObjectiveFailure'
import { useFailRemainingMyEventObjectives } from '../hooks/useFailRemainingMyEventObjectives'
import { getEventPath } from '../../events/eventUrl'
import { trialBlockReason } from '../../events/scoreboard/trialPresentation'
import {
  MY_EVENT_STATUS_LABELS,
  type MyCompetitorEvent,
  type MyDelegatedEvent,
  type ObjectiveOutcome,
} from '../../../types'

type PanelEvent = MyCompetitorEvent | MyDelegatedEvent

/**
 * `ObjectiveOutcome`'s own labels ("Pending") read fine per-objective, but
 * reused for a whole event's aggregate status they're misleading — a
 * competitor who has completed 9 of 10 objectives is not usefully described
 * as "Pending" (which reads as "not started"). This chip gets its own
 * wording; `Completed`/`Failed` stay the same as the shared labels.
 */
const EVENT_COMPLETION_LABELS: Record<ObjectiveOutcome, string> = {
  Pending: 'In progress',
  Completed: 'Completed',
  Failed: 'Failed',
}

interface MyEventsPanelProps {
  events: PanelEvent[]
  quickCompleteEnabled: boolean
  emptyMessage: string
}

const isDelegated = (event: PanelEvent): event is MyDelegatedEvent => 'competitorId' in event

const eventKey = (event: PanelEvent) =>
  isDelegated(event) ? `${event.eventId}-${event.competitorId}` : event.eventId

/**
 * One tab's events as a pick-one-at-a-time list: selecting a row reveals that
 * event's objectives beside it. Only the selected event fetches, so a
 * competitor in a dozen events pays for one request rather than twelve.
 */
export const MyEventsPanel = ({
  events,
  quickCompleteEnabled,
  emptyMessage,
}: MyEventsPanelProps) => {
  const [selectedKey, setSelectedKey] = useState<string | null>(
    events.length > 0 ? eventKey(events[0]!) : null,
  )
  const selected = events.find((event) => eventKey(event) === selectedKey) ?? events[0]

  if (events.length === 0) {
    return <EmptyState title={emptyMessage} />
  }

  return (
    <Stack direction={{ xs: 'column', md: 'row' }} spacing={2} sx={{ alignItems: 'flex-start' }}>
      <Paper variant="outlined" sx={{ width: { xs: '100%', md: 320 }, flexShrink: 0 }}>
        <List disablePadding>
          {events.map((event) => {
            const key = eventKey(event)
            const completed =
              event.totalObjectives - event.incompleteObjectives - event.failedObjectives
            return (
              <ListItemButton
                key={key}
                selected={key === eventKey(selected!)}
                onClick={() => setSelectedKey(key)}
                sx={{ display: 'block' }}
              >
                <Typography sx={{ fontWeight: 600 }}>{event.eventName}</Typography>
                {isDelegated(event) && (
                  <Typography variant="body2" color="text.secondary">
                    Managing {event.competitorName}
                  </Typography>
                )}
                <Stack direction="row" spacing={1} sx={{ mt: 0.5, alignItems: 'center' }}>
                  <Chip
                    label={MY_EVENT_STATUS_LABELS[event.status]}
                    size="small"
                    color={event.status === 'live' ? 'success' : 'default'}
                    variant={event.status === 'live' ? 'filled' : 'outlined'}
                  />
                  <Typography variant="caption" color="text.secondary">
                    {event.score.toLocaleString()} pts · #{event.rank} · {completed}/
                    {event.totalObjectives}
                  </Typography>
                </Stack>
              </ListItemButton>
            )
          })}
        </List>
      </Paper>
      {selected && (
        <Box sx={{ flex: 1, minWidth: 0, width: '100%' }}>
          <SelectedEventObjectives event={selected} quickCompleteEnabled={quickCompleteEnabled} />
        </Box>
      )}
    </Stack>
  )
}

const SelectedEventObjectives = ({
  event,
  quickCompleteEnabled,
}: {
  event: PanelEvent
  quickCompleteEnabled: boolean
}) => {
  const competitorId = isDelegated(event) ? event.competitorId : undefined
  const toggle = useToggleMyEventObjective(event.eventId, competitorId)
  const toggleFailure = useToggleMyEventObjectiveFailure(event.eventId, competitorId)
  const failRemaining = useFailRemainingMyEventObjectives(event.eventId, competitorId)
  const isPending = toggle.isPending || toggleFailure.isPending || failRemaining.isPending
  const objectives = useMyEventObjectives(event.eventId, competitorId, true, isPending)
  const eventIsLive = quickCompleteEnabled && event.status === 'live'

  return (
    <Paper variant="outlined" sx={{ p: 2 }}>
      <Stack
        direction="row"
        spacing={1}
        sx={{ alignItems: 'center', justifyContent: 'space-between', mb: 1.5 }}
      >
        <Typography
          component={RouterLink}
          to={getEventPath(event.eventId, event.urlAlias)}
          sx={{ color: 'primary.main', fontWeight: 700 }}
        >
          {event.eventName}
        </Typography>
        <Chip
          label={EVENT_COMPLETION_LABELS[event.completionStatus]}
          size="small"
          color={event.completionStatus === 'Failed' ? 'error' : 'default'}
          variant="outlined"
        />
      </Stack>
      {objectives.isLoading && (
        <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
          <CircularProgress size={20} />
          <Typography>Loading objectives…</Typography>
        </Stack>
      )}
      {objectives.isError && <Alert severity="error">Failed to load objectives.</Alert>}
      {toggle.isError && <Alert severity="error">Could not update the objective.</Alert>}
      {toggleFailure.isError && (
        <Alert severity="error">Could not update the objective&apos;s failure status.</Alert>
      )}
      {failRemaining.isError && (
        <Alert severity="error">Could not fail the remaining objectives.</Alert>
      )}
      {objectives.data && (
        <Stack spacing={2}>
          <MyEventObjectiveList
            games={objectives.data.games}
            // Trial mode on for a game makes it read-only here, whether the
            // run is recording or not: the server refuses every official write
            // while a slot exists, so offering the checkbox would only produce
            // a 409.
            canToggle={(game) => eventIsLive && !game.hasTrialRun}
            disabledHint={(game) => trialBlockReason(game, true)}
            isPending={isPending}
            onToggleCompleted={(game, objectiveId, completed) =>
              toggle.mutate({ eventGameId: game.gameId, objectiveId, completed })
            }
            onToggleFailed={(game, objectiveId, failed) =>
              toggleFailure.mutate({ eventGameId: game.gameId, objectiveId, failed })
            }
            onFailRemaining={(game) =>
              failRemaining.mutateAsync({ eventGameId: game.gameId }).catch(() => undefined)
            }
            failRemainingPending={failRemaining.isPending}
            targetName={competitorId ? objectives.data.competitorName : undefined}
          />
          {!quickCompleteEnabled && (
            <Alert
              severity="info"
              action={
                <Button
                  component={RouterLink}
                  to={`${getEventPath(event.eventId, event.urlAlias)}/games`}
                  size="small"
                >
                  Go to event
                </Button>
              }
            >
              Quick complete is disabled. Objectives are read-only here.
            </Alert>
          )}
        </Stack>
      )}
    </Paper>
  )
}
