import { useQuery } from '@tanstack/react-query'
import { twitchExtensionApi, TWITCH_EXTENSION_QUERY_KEYS } from '../api/twitchExtensionApi'

/** Whether this server backs a Twitch extension; the answer only changes with a redeploy. */
export const useTwitchExtensionStatus = () =>
  useQuery({
    queryKey: TWITCH_EXTENSION_QUERY_KEYS.status,
    queryFn: () => twitchExtensionApi.status(),
    staleTime: Infinity,
  })
