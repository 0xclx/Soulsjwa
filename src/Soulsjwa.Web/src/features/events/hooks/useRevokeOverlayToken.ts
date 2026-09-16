import { useMutation, useQueryClient } from '@tanstack/react-query'
import { overlayTokensApi, OVERLAY_TOKENS_QUERY_KEYS } from '../api/overlayTokensApi'

export const useRevokeOverlayToken = (eventId: string) => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (tokenId: string) => overlayTokensApi.revoke(eventId, tokenId),
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: OVERLAY_TOKENS_QUERY_KEYS.list(eventId) }),
  })
}
