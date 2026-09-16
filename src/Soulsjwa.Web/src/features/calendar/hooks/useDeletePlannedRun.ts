import { useMutation, useQueryClient } from '@tanstack/react-query'
import { plannedRunsApi, PLANNED_RUNS_QUERY_KEYS } from '../api/plannedRunsApi'
import { CALENDAR_QUERY_KEYS } from '../api/calendarApi'

export const useDeletePlannedRun = (eventId: string, userId: string) => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (plannedRunId: string) => plannedRunsApi.remove(eventId, userId, plannedRunId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: PLANNED_RUNS_QUERY_KEYS.list(eventId, userId) })
      queryClient.invalidateQueries({ queryKey: CALENDAR_QUERY_KEYS.globalPrefix })
    },
  })
}
