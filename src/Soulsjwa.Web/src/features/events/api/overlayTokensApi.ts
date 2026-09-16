import { apiClient } from '../../../lib/axios'
import type { OverlayToken, OverlayTokenWithSecret } from '../../../types'
import type {
  CreateOverlayTokenRequest,
  OverlayScoreboardResponse,
  OverlayTokenSettings,
} from '../../../types/overlay'

export const OVERLAY_TOKENS_QUERY_KEYS = {
  list: (eventId: string) => ['events', eventId, 'overlay-tokens'] as const,
  overlayScoreboard: (eventId: string, token: string) =>
    ['events', eventId, 'overlay-scoreboard', token] as const,
}

export const overlayTokensApi = {
  list: async (eventId: string): Promise<OverlayToken[]> => {
    const { data } = await apiClient.get<OverlayToken[]>(`/events/${eventId}/overlay-tokens`)
    return data
  },

  create: async (
    eventId: string,
    body: CreateOverlayTokenRequest,
  ): Promise<OverlayTokenWithSecret> => {
    const { data } = await apiClient.post<OverlayTokenWithSecret>(
      `/events/${eventId}/overlay-tokens`,
      body,
    )
    return data
  },

  /** Saves how the token's OBS source looks; a source already polling picks it up on its next refresh. */
  updateSettings: async (
    eventId: string,
    tokenId: string,
    settings: OverlayTokenSettings,
  ): Promise<OverlayToken> => {
    const { data } = await apiClient.put<OverlayToken>(
      `/events/${eventId}/overlay-tokens/${tokenId}/settings`,
      settings,
    )
    return data
  },

  revoke: async (eventId: string, tokenId: string): Promise<void> => {
    await apiClient.delete(`/events/${eventId}/overlay-tokens/${tokenId}`)
  },

  /**
   * Token-gated overlay poll: the scoreboard plus the token's saved look. Not
   * authenticated — the OBS browser source carries the token in the URL, so
   * we deliberately bypass the default `X-Api-Key` apparatus.
   */
  getOverlayScoreboard: async (
    eventId: string,
    token: string,
  ): Promise<OverlayScoreboardResponse> => {
    const { data } = await apiClient.get<OverlayScoreboardResponse>(
      `/events/${eventId}/overlay-scoreboard`,
      { params: { token } },
    )
    return data
  },
}
