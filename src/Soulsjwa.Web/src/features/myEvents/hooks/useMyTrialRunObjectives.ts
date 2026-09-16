import { useQuery } from '@tanstack/react-query'
import { myTrialRunsApi, MY_TRIAL_RUNS_QUERY_KEYS } from '../api/myTrialRunsApi'

const OBJECTIVES_REFRESH_INTERVAL_MS = 5000

export const useMyTrialRunObjectives = (
  trialRunId: string,
  enabled: boolean,
  /** Pause polling while a toggle is in flight so a stale response cannot
   * overwrite the optimistic value — same reasoning as useMyEventObjectives. */
  hasPendingToggle = false,
) =>
  useQuery({
    queryKey: MY_TRIAL_RUNS_QUERY_KEYS.objectives(trialRunId),
    queryFn: () => myTrialRunsApi.getObjectives(trialRunId),
    enabled,
    staleTime: OBJECTIVES_REFRESH_INTERVAL_MS,
    refetchInterval: enabled && !hasPendingToggle ? OBJECTIVES_REFRESH_INTERVAL_MS : false,
  })
