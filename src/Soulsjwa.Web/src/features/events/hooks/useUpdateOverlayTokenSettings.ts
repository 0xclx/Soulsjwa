import { useMutation, useQueryClient } from '@tanstack/react-query'
import type { OverlayTokenSettings } from '../../../types/overlay'
import { overlayTokensApi, OVERLAY_TOKENS_QUERY_KEYS } from '../api/overlayTokensApi'

export const useUpdateOverlayTokenSettings = (eventId: string) => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ tokenId, settings }: { tokenId: string; settings: OverlayTokenSettings }) =>
      overlayTokensApi.updateSettings(eventId, tokenId, settings),
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: OVERLAY_TOKENS_QUERY_KEYS.list(eventId) }),
  })
}
