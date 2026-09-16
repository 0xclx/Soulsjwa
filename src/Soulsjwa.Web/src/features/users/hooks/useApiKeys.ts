import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { usersApi, USERS_QUERY_KEYS } from '../api/usersApi'

export const useApiKeys = () => {
  return useQuery({
    queryKey: USERS_QUERY_KEYS.apiKeys,
    queryFn: usersApi.getApiKeys,
  })
}

export const useCreateApiKey = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: usersApi.createApiKey,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: USERS_QUERY_KEYS.apiKeys })
    },
  })
}

export const useDeleteApiKey = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: usersApi.deleteApiKey,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: USERS_QUERY_KEYS.apiKeys })
    },
  })
}
