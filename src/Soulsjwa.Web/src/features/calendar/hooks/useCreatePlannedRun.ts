import { useMutation, useQueryClient } from '@tanstack/react-query'
import { plannedRunsApi, PLANNED_RUNS_QUERY_KEYS } from '../api/plannedRunsApi'
import type { CreatePlannedRunRequest } from '../../../types/calendar'
import { CALENDAR_QUERY_KEYS } from '../api/calendarApi'

export const useCreatePlannedRun = (eventId: string, userId: string) => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (request: CreatePlannedRunRequest) =>
      plannedRunsApi.create(eventId, userId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: PLANNED_RUNS_QUERY_KEYS.list(eventId, userId) })
      queryClient.invalidateQueries({ queryKey: CALENDAR_QUERY_KEYS.globalPrefix })
    },
  })
}
