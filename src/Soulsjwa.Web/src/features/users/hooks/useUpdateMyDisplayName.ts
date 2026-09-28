import { useMutation, useQueryClient } from '@tanstack/react-query'
import { usersApi, USERS_QUERY_KEYS } from '../api/usersApi'

/** Sets (or, with `null`, clears) the signed-in user's display name. */
export const useUpdateMyDisplayName = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (displayName: string | null) => usersApi.updateMyDisplayName(displayName),
    onSuccess: (user) => queryClient.setQueryData(USERS_QUERY_KEYS.me, user),
  })
}
