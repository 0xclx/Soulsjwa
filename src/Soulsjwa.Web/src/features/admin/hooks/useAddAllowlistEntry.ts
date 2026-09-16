import { useMutation, useQueryClient } from '@tanstack/react-query'
import { ADMIN_QUERY_KEYS, adminApi } from '../api/adminApi'

export const useAddAllowlistEntry = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload: { twitchLogin: string; note?: string }) => adminApi.addAllowlist(payload),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ADMIN_QUERY_KEYS.allowlist }),
  })
}
