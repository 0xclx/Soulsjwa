import { useMutation, useQueryClient } from '@tanstack/react-query'
import { adminApi } from '../api/adminApi'
import type { UserRole } from '../../../types'

export const useSetUserRole = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (vars: { id: string; role: UserRole }) => adminApi.setUserRole(vars.id, vars.role),
    // The user list is paginated/searched and any role change can affect any
    // page, so invalidate the whole prefix.
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['admin', 'users'] }),
  })
}
