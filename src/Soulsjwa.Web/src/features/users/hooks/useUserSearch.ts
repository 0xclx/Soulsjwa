import { useQuery, keepPreviousData } from '@tanstack/react-query'
import { usersApi, USERS_QUERY_KEYS } from '../api/usersApi'

/**
 * Live search over existing users by Twitch login / display name. The server
 * returns an empty list for queries shorter than 2 characters; the hook is
 * kept disabled until the query is non-trivial to avoid spam on every keystroke.
 */
export const useUserSearch = (query: string, enabled = true) => {
  const trimmed = query.trim()
  return useQuery({
    queryKey: USERS_QUERY_KEYS.search(trimmed),
    queryFn: () => usersApi.search(trimmed),
    enabled: enabled && trimmed.length >= 2,
    placeholderData: keepPreviousData,
    staleTime: 30_000,
  })
}
