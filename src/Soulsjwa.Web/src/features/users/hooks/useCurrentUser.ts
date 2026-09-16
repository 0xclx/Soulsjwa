import { useQuery } from '@tanstack/react-query'
import { usersApi, USERS_QUERY_KEYS } from '../api/usersApi'
import { useIsAuthenticated } from '../../../lib/axios'

export const useCurrentUser = () => {
  const isAuthenticated = useIsAuthenticated()

  return useQuery({
    queryKey: USERS_QUERY_KEYS.me,
    queryFn: usersApi.getMe,
    enabled: isAuthenticated,
  })
}
