import { useMutation, useQueryClient } from '@tanstack/react-query'
import { adminApi, ADMIN_QUERY_KEYS } from '../api/adminApi'

export const useUpdateFeatureFlag = (key: string) => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (enabled: boolean) => adminApi.updateFeatureFlag(key, enabled),
    onSuccess: (flag) => {
      queryClient.setQueryData([...ADMIN_QUERY_KEYS.featureFlags, key], flag)
    },
  })
}
