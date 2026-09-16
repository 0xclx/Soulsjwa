import type { Theme } from '@mui/material/styles'
import { CALENDAR_ENTRY_COLOR_SLOTS, type CalendarEntryColor } from '../../types/calendar'

/** Resolves a `CalendarEntryColor` slot to the actual colour value on the
 * active MUI theme — the only place a calendar colour becomes a real colour.
 * Never a hex value stored on the entry itself. */
export const resolveCalendarEntryColor = (theme: Theme, color: CalendarEntryColor): string => {
  const path = CALENDAR_ENTRY_COLOR_SLOTS[color]
  const value = path
    .split('.')
    .reduce<unknown>(
      (acc, key) => (acc as Record<string, unknown> | undefined)?.[key],
      theme.palette,
    )
  return typeof value === 'string' ? value : theme.palette.action.selected
}
