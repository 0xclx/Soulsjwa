import { useMutation, useQueryClient } from '@tanstack/react-query'
import { adminApi, ADMIN_QUERY_KEYS } from '../api/adminApi'

export const useCreateGame = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: adminApi.createGame,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ADMIN_QUERY_KEYS.games }),
  })
}
