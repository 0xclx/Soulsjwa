import { useQuery } from '@tanstack/react-query'
import { ADMIN_QUERY_KEYS, adminApi } from '../api/adminApi'

export const useAllowlist = () =>
  useQuery({
    queryKey: ADMIN_QUERY_KEYS.allowlist,
    queryFn: adminApi.listAllowlist,
  })
