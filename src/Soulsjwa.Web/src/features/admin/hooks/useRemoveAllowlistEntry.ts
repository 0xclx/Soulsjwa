import { useMutation, useQueryClient } from '@tanstack/react-query'
import { ADMIN_QUERY_KEYS, adminApi } from '../api/adminApi'

export const useRemoveAllowlistEntry = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => adminApi.removeAllowlist(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ADMIN_QUERY_KEYS.allowlist }),
  })
}
