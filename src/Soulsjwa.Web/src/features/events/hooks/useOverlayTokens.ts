import { useQuery } from '@tanstack/react-query'
import { overlayTokensApi, OVERLAY_TOKENS_QUERY_KEYS } from '../api/overlayTokensApi'

export const useOverlayTokens = (eventId: string, enabled = true) =>
  useQuery({
    queryKey: OVERLAY_TOKENS_QUERY_KEYS.list(eventId),
    queryFn: () => overlayTokensApi.list(eventId),
    enabled: enabled && !!eventId,
  })
