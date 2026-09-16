import { useMutation, useQueryClient } from '@tanstack/react-query'
import { adminApi, ADMIN_QUERY_KEYS } from '../api/adminApi'

export const useCreatePredefinedObjective = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: adminApi.createPredefinedObjective,
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: ADMIN_QUERY_KEYS.predefinedObjectives }),
  })
}
