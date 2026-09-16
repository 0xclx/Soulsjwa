import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { calendarApi, CALENDAR_QUERY_KEYS } from '../api/calendarApi'

/**
 * `from`/`to` are ISO-8601 strings for the visible date window; omit either
 * to use the server's default.
 *
 * `keepPreviousData` matters here beyond the usual "avoid a flash of empty
 * state": FullCalendar's own view/date navigation drives `from`/`to` (see
 * `CalendarPage`'s `datesSet`), so every prev/next click or month↔week↔list
 * switch changes this query's key. Without it, `isLoading` goes true on
 * every one of those, the caller's `isLoading` guard unmounts the
 * `<FullCalendar>` element entirely, and remounting it resets FullCalendar's
 * own internal view/date state back to its `initialView`/today — the
 * calendar can never actually get anywhere. Keeping the previous page's data
 * on screen during a refetch means only the very first load is ever
 * `isLoading`.
 */
export const useGlobalCalendar = (from?: string, to?: string) =>
  useQuery({
    queryKey: CALENDAR_QUERY_KEYS.global(from, to),
    queryFn: () => calendarApi.getGlobal(from, to),
    placeholderData: keepPreviousData,
  })
