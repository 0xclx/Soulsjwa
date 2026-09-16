import { useQuery } from '@tanstack/react-query'
import { myEventsApi, MY_EVENTS_QUERY_KEYS } from '../api/myEventsApi'

const OBJECTIVES_REFRESH_INTERVAL_MS = 5000

export const useMyEventObjectives = (
  eventId: string,
  competitorId: string | undefined,
  enabled: boolean,
  /** Pause polling while a toggle mutation for this card is in flight, so a
   * stale poll response can't overwrite the optimistic value before the
   * mutation settles (see `useToggleMyEventObjective`/`useToggleMyEventObjectiveFailure`). */
  hasPendingToggle = false,
) =>
  useQuery({
    queryKey: MY_EVENTS_QUERY_KEYS.objectives(eventId, competitorId),
    queryFn: () => myEventsApi.getObjectives(eventId, competitorId),
    enabled,
    staleTime: OBJECTIVES_REFRESH_INTERVAL_MS,
    refetchInterval: enabled && !hasPendingToggle ? OBJECTIVES_REFRESH_INTERVAL_MS : false,
    // No refetchIntervalInBackground here (unlike the overlay's scoreboard
    // poll): a hidden My Events tab has no viewer watching it, so pausing the
    // poll while hidden saves battery/data with no visible cost. TanStack
    // Query resumes polling immediately when the tab becomes visible again.
  })
