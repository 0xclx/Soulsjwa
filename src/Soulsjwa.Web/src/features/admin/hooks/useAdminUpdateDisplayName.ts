import { useMutation, useQueryClient } from '@tanstack/react-query'
import { adminApi } from '../api/adminApi'
import { USERS_QUERY_KEYS } from '../../users/api/usersApi'

/** Sets (or, with `null`, clears) any user's display name. */
export const useAdminUpdateDisplayName = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (vars: { id: string; displayName: string | null }) =>
      adminApi.setUserDisplayName(vars.id, vars.displayName),
    onSuccess: () =>
      Promise.all([
        // Any page of the paginated, searchable list may show the user.
        queryClient.invalidateQueries({ queryKey: ['admin', 'users'] }),
        // The admin may have renamed themselves.
        queryClient.invalidateQueries({ queryKey: USERS_QUERY_KEYS.me }),
      ]),
  })
}
