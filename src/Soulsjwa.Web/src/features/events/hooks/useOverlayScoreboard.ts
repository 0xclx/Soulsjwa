import { useQuery } from '@tanstack/react-query'
import { overlayTokensApi, OVERLAY_TOKENS_QUERY_KEYS } from '../api/overlayTokensApi'

/**
 * Polls the token-gated overlay scoreboard. Disabled until both an event id
 * and a token are present so the overlay page can show a clear "missing
 * token" hint without surfacing a 401 from a missing query param.
 *
 * `urlRefreshSeconds` is the cadence the URL asked for; once a poll has
 * answered, the look saved on the token wins (as it does for every knob), so
 * changing the refresh in the app reaches the OBS source like any other edit.
 */
export const useOverlayScoreboard = (
  eventId: string | undefined,
  token: string | null,
  urlRefreshSeconds: number,
) =>
  useQuery({
    queryKey: OVERLAY_TOKENS_QUERY_KEYS.overlayScoreboard(eventId ?? '', token ?? ''),
    queryFn: () => overlayTokensApi.getOverlayScoreboard(eventId!, token!),
    enabled: !!eventId && !!token,
    refetchInterval: (query) =>
      (query.state.data?.settings?.refreshSeconds ?? urlRefreshSeconds) * 1000,
    // Keep polling while hidden — an OBS browser source is never the
    // foreground document, so pausing this like `useMyEventObjectives` does
    // would freeze live stream output. Do not "fix" this to match that one.
    refetchIntervalInBackground: true,
  })
