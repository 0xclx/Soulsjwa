// const tuple + derived type, iterable AND type-safe — mirrors
// Soulsjwa.Api.Features.Calendar.Entities.CalendarEntryColor exactly.
export const CALENDAR_ENTRY_COLORS = [
  'Default',
  'Accent',
  'Danger',
  'Info',
  'Success',
  'Highlight',
] as const
export type CalendarEntryColor = (typeof CALENDAR_ENTRY_COLORS)[number]

export const CALENDAR_ENTRY_COLOR_LABELS: Record<CalendarEntryColor, string> = {
  Default: 'Default',
  Accent: 'Accent',
  Danger: 'Danger',
  Info: 'Info',
  Success: 'Success',
  Highlight: 'Highlight',
}

/** Slot → MUI theme path. The only place a calendar colour becomes a real colour. */
export const CALENDAR_ENTRY_COLOR_SLOTS: Record<CalendarEntryColor, string> = {
  Default: 'action.selected',
  Accent: 'primary.main',
  Danger: 'error.main',
  Info: 'secondary.main',
  Success: 'success.main',
  Highlight: 'warning.main',
}

export interface CalendarEntry {
  id: string
  eventId: string
  title: string
  descriptionMarkdown: string | null
  startsAt: string
  endsAt: string
  isAllDay: boolean
  isHighlighted: boolean
  color: CalendarEntryColor
  imageAssetId: string | null
  imageUrl: string | null
  createdById: string
  createdAt: string
  updatedAt: string
  /** Postgres `xmin` concurrency token. Round-trip it into the next update's `version` to get a `409` instead of silently losing a concurrent edit. */
  version: number
}

export interface UpsertCalendarEntryRequest {
  title: string
  descriptionMarkdown: string | null
  startsAt: string
  endsAt: string
  isAllDay: boolean
  isHighlighted: boolean
  color: CalendarEntryColor
  imageAssetId: string | null
}

export interface PlannedRun {
  id: string
  eventId: string
  eventGameId: string
  userId: string
  startsAt: string
  endsAt: string
  color: CalendarEntryColor
  createdAt: string
  updatedAt: string
}

export interface CreatePlannedRunRequest {
  eventGameId: string
  startsAt: string
  endsAt: string
  color: CalendarEntryColor
}

export interface UpdatePlannedRunRequest {
  startsAt: string
  endsAt: string
  color: CalendarEntryColor
}

/** One admin-authored entry as it appears on the aggregated global calendar,
 * including its body — the global calendar's click-through detail popup (see
 * `CalendarEventDialog`) renders it, so unlike the list-row-only fields above it isn't
 * fetched separately. */
export interface GlobalCalendarEntry {
  id: string
  eventId: string
  eventName: string
  title: string
  descriptionMarkdown: string | null
  startsAt: string
  endsAt: string
  isAllDay: boolean
  isHighlighted: boolean
  color: CalendarEntryColor
  imageAssetId: string | null
  imageUrl: string | null
}

/** One competitor's planned run as it appears on the aggregated global calendar —
 * rendered as "Event name · game · competitor · time", colored per `color`
 * (the same named-slot picker the competitor/manager chose from when
 * creating the run — see `PlannedRunEditorDialog`). */
export interface GlobalPlannedRun {
  id: string
  eventId: string
  eventName: string
  eventGameId: string
  gameName: string
  userId: string
  competitorName: string
  startsAt: string
  endsAt: string
  color: CalendarEntryColor
}

export interface GlobalCalendarResponse {
  entries: GlobalCalendarEntry[]
  plannedRuns: GlobalPlannedRun[]
  /** True when either collection hit its server-side cap for the requested window. */
  truncated: boolean
}
