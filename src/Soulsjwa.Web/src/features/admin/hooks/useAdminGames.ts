import { useQuery } from '@tanstack/react-query'
import { adminApi, ADMIN_QUERY_KEYS } from '../api/adminApi'

export const useAdminGames = () =>
  useQuery({
    queryKey: ADMIN_QUERY_KEYS.games,
    queryFn: adminApi.listGames,
  })
