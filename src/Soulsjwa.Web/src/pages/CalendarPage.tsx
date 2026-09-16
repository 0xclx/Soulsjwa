import { useMemo, useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import FullCalendar from '@fullcalendar/react'
import dayGridPlugin from '@fullcalendar/daygrid'
import timeGridPlugin from '@fullcalendar/timegrid'
import listPlugin from '@fullcalendar/list'
import type { DatesSetArg, EventContentArg, EventInput } from '@fullcalendar/core'
import Box from '@mui/material/Box'
import Chip from '@mui/material/Chip'
import Stack from '@mui/material/Stack'
import Typography from '@mui/material/Typography'
import useMediaQuery from '@mui/material/useMediaQuery'
import { useTheme } from '@mui/material/styles'
import { PageHeader, ErrorMessage, LoadingState } from '../components/ui'
import { useGlobalCalendar } from '../features/calendar/hooks/useGlobalCalendar'
import { resolveCalendarEntryColor } from '../features/calendar/colorResolver'
import {
  CalendarEventDialog,
  type SelectedCalendarEvent,
} from '../features/calendar/components/CalendarEventDialog'
import { getEventPath } from '../features/events/eventUrl'
import type { GlobalCalendarEntry, GlobalPlannedRun } from '../types/calendar'

type CalendarEventMeta =
  ({ kind: 'entry' } & GlobalCalendarEntry) | ({ kind: 'plannedRun' } & GlobalPlannedRun)

/**
 * Global `/calendar` page: every non-archived event's admin calendar entries
 * and competitor planned runs in one month view, themed from the MUI
 * palette. Falls back to a list view on narrow screens.
 */
export const CalendarPage = () => {
  const theme = useTheme()
  const navigate = useNavigate()
  const isNarrow = useMediaQuery(theme.breakpoints.down('sm'))
  // FullCalendar's visible range, kept a little wider than the strict month
  // view (it shows leading/trailing days from neighbouring months) — updated
  // via datesSet so the backend's bounded /calendar window always covers
  // what's on screen. Undefined until the first render so the initial
  // request uses the server's own sensible default.
  const [visibleRange, setVisibleRange] = useState<{ from?: string; to?: string }>({})
  const { data, isLoading, isError } = useGlobalCalendar(visibleRange.from, visibleRange.to)
  const calendarRef = useRef<FullCalendar | null>(null)
  const [selectedEvent, setSelectedEvent] = useState<SelectedCalendarEvent | null>(null)

  const handleDatesSet = (arg: DatesSetArg) => {
    setVisibleRange({ from: arg.start.toISOString(), to: arg.end.toISOString() })
  }

  const events = useMemo<EventInput[]>(() => {
    if (!data) return []

    const entryEvents: EventInput[] = data.entries.map((entry) => {
      const bg = resolveCalendarEntryColor(theme, entry.color)
      return {
        id: `entry-${entry.id}`,
        title: entry.isHighlighted ? `★ ${entry.title}` : entry.title,
        start: entry.startsAt,
        end: entry.endsAt,
        allDay: entry.isAllDay,
        backgroundColor: bg,
        borderColor: bg,
        // Every slot resolves to a different swatch (and a custom site theme
        // can repaint them all), so the readable text colour has to be
        // computed per event rather than fixed — MUI's own contrast formula
        // is what already decides every `*.contrastText` in the theme.
        textColor: theme.palette.getContrastText(bg),
        extendedProps: { kind: 'entry', ...entry } satisfies CalendarEventMeta,
      }
    })

    const plannedRunEvents: EventInput[] = data.plannedRuns.map((run) => {
      const color = resolveCalendarEntryColor(theme, run.color)
      return {
        id: `planned-run-${run.id}`,
        title: `${run.eventName} · ${run.gameName} · ${run.competitorName}`,
        start: run.startsAt,
        end: run.endsAt,
        backgroundColor: color,
        borderColor: color,
        textColor: theme.palette.getContrastText(color),
        extendedProps: { kind: 'plannedRun', ...run } satisfies CalendarEventMeta,
      }
    })

    return [...entryEvents, ...plannedRunEvents]
  }, [data, theme])

  if (isLoading) return <LoadingState label="Loading calendar…" />
  if (isError || !data) return <ErrorMessage message="Failed to load the calendar." />

  const renderEventContent = (arg: EventContentArg) => {
    const meta = arg.event.extendedProps as CalendarEventMeta
    // FullCalendar only applies `textColor` to its own default rendering;
    // since `eventContent` replaces that, the color has to be re-applied by
    // hand here to actually take effect. Only for a filled block, though: the
    // list view never paints the event's color as a background (just a small
    // dot beside plain text on the page's own background), so the contrast
    // color computed for pairing against that fill would be the wrong color
    // to read against there — the list view keeps the page's normal text
    // color regardless of the event's own color.
    const isListView = arg.view.type.startsWith('list')
    const textColor = isListView ? undefined : arg.event.textColor || undefined

    // A planned run's three parts (event, game, competitor) each matter —
    // truncating the joined title with an ellipsis was hiding whichever one
    // ran out of room first. Each gets its own line and is allowed to wrap
    // instead, so all three stay readable regardless of the cell's width.
    if (meta.kind === 'plannedRun') {
      return (
        <Stack spacing={0} sx={{ px: 0.5, py: 0.25, overflow: 'hidden', color: textColor }}>
          <Typography variant="caption" sx={{ fontWeight: 600, lineHeight: 1.2, color: 'inherit' }}>
            {meta.eventName}
          </Typography>
          <Typography variant="caption" sx={{ lineHeight: 1.2, color: 'inherit' }}>
            {meta.gameName}
          </Typography>
          <Typography variant="caption" sx={{ lineHeight: 1.2, color: 'inherit' }}>
            {meta.competitorName}
          </Typography>
        </Stack>
      )
    }

    return (
      <Stack
        direction="row"
        spacing={0.5}
        sx={{ alignItems: 'center', overflow: 'hidden', px: 0.5, color: textColor }}
      >
        <Typography
          variant="caption"
          noWrap
          sx={{ fontWeight: meta.isHighlighted ? 700 : 400, color: 'inherit' }}
        >
          {arg.event.title}
        </Typography>
      </Stack>
    )
  }

  const handleEventClick = (meta: CalendarEventMeta) => {
    setSelectedEvent(
      meta.kind === 'entry' ? { kind: 'entry', entry: meta } : { kind: 'plannedRun', run: meta },
    )
  }

  return (
    <Box component="section" aria-labelledby="calendar-heading">
      <PageHeader
        eyebrow="Calendar"
        title={
          <span id="calendar-heading" style={{ display: 'contents' }}>
            Global Calendar
          </span>
        }
        description="Every event's milestones, announcements, and competitors' planned runs in one place."
      />

      {events.length === 0 && (
        <Chip label="No calendar entries or planned runs yet" size="small" sx={{ mt: 2, mb: 1 }} />
      )}

      <Box sx={{ mt: 2, overflowX: 'auto' }}>
        <Box
          sx={{
            minWidth: 320,
            // FullCalendar's own CSS variables (main.css) default to a fixed
            // light neutral background/text pair for header cells, buttons,
            // and today's highlight — never adapted for dark mode. Without
            // this, the day-of-week header row keeps its light background
            // while its text inherits the page's (white, in dark mode)
            // color, so the header reads as a blank bar. Driving every slot
            // from the active MUI theme keeps both modes readable without
            // hardcoding either.
            '--fc-page-bg-color': theme.palette.background.paper,
            '--fc-neutral-bg-color': theme.palette.action.hover,
            '--fc-neutral-text-color': theme.palette.text.secondary,
            '--fc-border-color': theme.palette.divider,
            '--fc-button-text-color': theme.palette.primary.contrastText,
            '--fc-button-bg-color': theme.palette.primary.main,
            '--fc-button-border-color': theme.palette.primary.main,
            '--fc-button-hover-bg-color': theme.palette.primary.dark,
            '--fc-button-hover-border-color': theme.palette.primary.dark,
            '--fc-button-active-bg-color': theme.palette.primary.dark,
            '--fc-button-active-border-color': theme.palette.primary.dark,
            '--fc-today-bg-color': theme.palette.action.selected,
            '--fc-list-event-hover-bg-color': theme.palette.action.hover,
            color: theme.palette.text.primary,
          }}
        >
          <FullCalendar
            ref={calendarRef}
            plugins={[dayGridPlugin, timeGridPlugin, listPlugin]}
            initialView={isNarrow ? 'listMonth' : 'dayGridMonth'}
            // dayGridMonth's default "auto" display renders events as a tiny
            // colored dot beside plain text rather than a filled block — the
            // background/border color each event carries never shows up
            // there. "block" forces the filled style everywhere (it's what
            // timeGrid/list already use by default), so the color is visible
            // in every view, not just week/day.
            eventDisplay="block"
            headerToolbar={{
              left: 'prev,next today',
              center: 'title',
              right: isNarrow ? 'listMonth' : 'dayGridMonth,timeGridWeek,listMonth',
            }}
            height="auto"
            events={events}
            datesSet={handleDatesSet}
            eventContent={renderEventContent}
            eventClick={(arg) => handleEventClick(arg.event.extendedProps as CalendarEventMeta)}
          />
        </Box>
      </Box>

      <CalendarEventDialog
        event={selectedEvent}
        onClose={() => setSelectedEvent(null)}
        onViewEvent={(eventId) => {
          setSelectedEvent(null)
          navigate(getEventPath(eventId))
        }}
      />
    </Box>
  )
}
