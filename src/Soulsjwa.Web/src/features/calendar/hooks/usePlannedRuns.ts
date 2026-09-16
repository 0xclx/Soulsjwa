import { useQuery } from '@tanstack/react-query'
import { plannedRunsApi, PLANNED_RUNS_QUERY_KEYS } from '../api/plannedRunsApi'

export const usePlannedRuns = (eventId: string, userId: string) =>
  useQuery({
    queryKey: PLANNED_RUNS_QUERY_KEYS.list(eventId, userId),
    queryFn: () => plannedRunsApi.list(eventId, userId),
    enabled: !!eventId && !!userId,
  })
