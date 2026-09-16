import { useMutation, useQueryClient } from '@tanstack/react-query'
import { plannedRunsApi, PLANNED_RUNS_QUERY_KEYS } from '../api/plannedRunsApi'
import type { UpdatePlannedRunRequest } from '../../../types/calendar'
import { CALENDAR_QUERY_KEYS } from '../api/calendarApi'

export const useUpdatePlannedRun = (eventId: string, userId: string) => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({
      plannedRunId,
      request,
    }: {
      plannedRunId: string
      request: UpdatePlannedRunRequest
    }) => plannedRunsApi.update(eventId, userId, plannedRunId, request),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: PLANNED_RUNS_QUERY_KEYS.list(eventId, userId) })
      queryClient.invalidateQueries({ queryKey: CALENDAR_QUERY_KEYS.globalPrefix })
    },
  })
}
