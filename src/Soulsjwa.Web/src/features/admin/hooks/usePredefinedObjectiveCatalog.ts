import { useQuery } from '@tanstack/react-query'
import { adminApi, ADMIN_QUERY_KEYS } from '../api/adminApi'

export const usePredefinedObjectiveCatalog = () =>
  useQuery({
    queryKey: ADMIN_QUERY_KEYS.predefinedObjectives,
    queryFn: adminApi.listPredefinedObjectives,
  })
