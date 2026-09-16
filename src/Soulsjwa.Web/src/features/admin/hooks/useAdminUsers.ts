import { useQuery } from '@tanstack/react-query'
import { ADMIN_QUERY_KEYS, adminApi } from '../api/adminApi'

export const useAdminUsers = (page: number, search?: string) =>
  useQuery({
    queryKey: ADMIN_QUERY_KEYS.users(page, search),
    queryFn: () => adminApi.listUsers(page, search),
  })
