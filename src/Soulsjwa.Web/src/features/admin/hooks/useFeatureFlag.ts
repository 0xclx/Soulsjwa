import { useQuery } from '@tanstack/react-query'
import { adminApi, ADMIN_QUERY_KEYS } from '../api/adminApi'

export const useFeatureFlag = (key: string) =>
  useQuery({
    queryKey: [...ADMIN_QUERY_KEYS.featureFlags, key],
    queryFn: () => adminApi.getFeatureFlag(key),
  })
