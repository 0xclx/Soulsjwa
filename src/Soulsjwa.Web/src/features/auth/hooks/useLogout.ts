import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useNavigate } from 'react-router-dom'
import { authApi } from '../api/authApi'
import { tokenStore, tokenManager } from '../../../lib/axios'
import { SCOREBOARD_CACHE_NAME } from '../../../lib/serviceWorkerCacheNames'

export const useLogout = () => {
  const queryClient = useQueryClient()
  const navigate = useNavigate()

  return useMutation({
    mutationFn: authApi.revoke,
    onSettled: () => {
      tokenManager.stop()
      tokenStore.clearAccessToken()
      queryClient.clear()
      // Cached authenticated-session responses (the scoreboard) must not
      // outlive the session on a shared device.
      if ('caches' in window) void caches.delete(SCOREBOARD_CACHE_NAME)
      navigate('/')
    },
  })
}
