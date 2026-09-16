import { useMutation, useQueryClient } from '@tanstack/react-query'
import type { CreateOverlayTokenRequest } from '../../../types/overlay'
import { overlayTokensApi, OVERLAY_TOKENS_QUERY_KEYS } from '../api/overlayTokensApi'

export const useCreateOverlayToken = (eventId: string) => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (body: CreateOverlayTokenRequest) => overlayTokensApi.create(eventId, body),
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: OVERLAY_TOKENS_QUERY_KEYS.list(eventId) }),
  })
}
