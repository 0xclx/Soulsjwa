import { useMutation, useQueryClient } from '@tanstack/react-query'
import { adminApi, ADMIN_QUERY_KEYS } from '../api/adminApi'

export const useUpdateGame = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({
      gameId,
      payload,
    }: {
      gameId: number
      payload: { name: string; description: string }
    }) => adminApi.updateGame(gameId, payload),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ADMIN_QUERY_KEYS.games }),
  })
}
