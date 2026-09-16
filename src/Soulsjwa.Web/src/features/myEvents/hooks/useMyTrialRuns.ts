import { useQuery } from '@tanstack/react-query'
import { myTrialRunsApi, MY_TRIAL_RUNS_QUERY_KEYS } from '../api/myTrialRunsApi'

const TRIAL_RUNS_REFRESH_INTERVAL_MS = 5000

export const useMyTrialRuns = (enabled = true) =>
  useQuery({
    queryKey: MY_TRIAL_RUNS_QUERY_KEYS.all,
    queryFn: myTrialRunsApi.list,
    enabled,
    staleTime: TRIAL_RUNS_REFRESH_INTERVAL_MS,
    refetchInterval: enabled ? TRIAL_RUNS_REFRESH_INTERVAL_MS : false,
  })
