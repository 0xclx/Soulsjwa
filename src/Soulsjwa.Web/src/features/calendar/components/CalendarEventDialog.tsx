import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Dialog from '@mui/material/Dialog'
import DialogActions from '@mui/material/DialogActions'
import DialogContent from '@mui/material/DialogContent'
import DialogTitle from '@mui/material/DialogTitle'
import Divider from '@mui/material/Divider'
import IconButton from '@mui/material/IconButton'
import Stack from '@mui/material/Stack'
import Typography from '@mui/material/Typography'
import CalendarMonthIcon from '@mui/icons-material/CalendarMonth'
import CloseIcon from '@mui/icons-material/Close'
import ScheduleIcon from '@mui/icons-material/Schedule'
import { MarkdownView } from '../../markdown/MarkdownView'
import type { GlobalCalendarEntry, GlobalPlannedRun } from '../../../types/calendar'

export type SelectedCalendarEvent =
  { kind: 'entry'; entry: GlobalCalendarEntry } | { kind: 'plannedRun'; run: GlobalPlannedRun }

interface CalendarEventDialogProps {
  event: SelectedCalendarEvent | null
  onClose: () => void
  onViewEvent: (eventId: string) => void
}

const formatDate = (iso: string) =>
  new Date(iso).toLocaleDateString(undefined, {
    weekday: 'long',
    year: 'numeric',
    month: 'long',
    day: 'numeric',
  })

const formatTimeRange = (startsAt: string, endsAt: string) => {
  const opts: Intl.DateTimeFormatOptions = { timeStyle: 'short' }
  return `${new Date(startsAt).toLocaleTimeString(undefined, opts)} – ${new Date(endsAt).toLocaleTimeString(undefined, opts)}`
}

/**
 * Click-through detail popup for one global-calendar item — an
 * admin calendar entry or a competitor's planned run — over a blurred
 * backdrop, replacing the old click-to-navigate-straight-to-the-event
 * behavior with an at-a-glance view; "View event" still gets you there.
 */
export const CalendarEventDialog = ({ event, onClose, onViewEvent }: CalendarEventDialogProps) => {
  const open = event !== null

  const title = event ? (event.kind === 'entry' ? event.entry.title : event.run.gameName) : ''
  const eventId = event ? (event.kind === 'entry' ? event.entry.eventId : event.run.eventId) : ''
  const eventName = event
    ? event.kind === 'entry'
      ? event.entry.eventName
      : event.run.eventName
    : ''

  return (
    <Dialog
      open={open}
      onClose={onClose}
      fullWidth
      maxWidth="sm"
      slotProps={{
        backdrop: { sx: { backdropFilter: 'blur(4px)' } },
      }}
    >
      {event && (
        <>
          <DialogTitle sx={{ pr: 6 }}>
            {title}
            <Typography variant="body2" color="text.secondary">
              {eventName}
              {event.kind === 'plannedRun' && ` · ${event.run.competitorName}`}
            </Typography>
            <IconButton
              aria-label="Close"
              onClick={onClose}
              sx={{ position: 'absolute', right: 8, top: 8 }}
            >
              <CloseIcon />
            </IconButton>
          </DialogTitle>
          <DialogContent>
            <Stack spacing={2}>
              {event.kind === 'entry' && event.entry.imageUrl && (
                <Box
                  component="img"
                  src={event.entry.imageUrl}
                  alt=""
                  sx={{ width: '100%', borderRadius: 1, display: 'block' }}
                />
              )}

              <Stack spacing={1}>
                <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
                  <CalendarMonthIcon fontSize="small" color="action" />
                  <Typography variant="body2">
                    {formatDate(event.kind === 'entry' ? event.entry.startsAt : event.run.startsAt)}
                  </Typography>
                </Stack>
                {!(event.kind === 'entry' && event.entry.isAllDay) && (
                  <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
                    <ScheduleIcon fontSize="small" color="action" />
                    <Typography variant="body2">
                      {formatTimeRange(
                        event.kind === 'entry' ? event.entry.startsAt : event.run.startsAt,
                        event.kind === 'entry' ? event.entry.endsAt : event.run.endsAt,
                      )}
                    </Typography>
                  </Stack>
                )}
              </Stack>

              {event.kind === 'entry' && event.entry.descriptionMarkdown && (
                <>
                  <Divider />
                  <MarkdownView source={event.entry.descriptionMarkdown} />
                </>
              )}
            </Stack>
          </DialogContent>
          <DialogActions>
            <Button onClick={() => onViewEvent(eventId)}>View event</Button>
            <Button variant="contained" onClick={onClose}>
              Close
            </Button>
          </DialogActions>
        </>
      )}
    </Dialog>
  )
}
