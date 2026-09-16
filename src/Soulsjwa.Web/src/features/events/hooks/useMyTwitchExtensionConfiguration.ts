import { useQuery } from '@tanstack/react-query'
import { twitchExtensionApi, TWITCH_EXTENSION_QUERY_KEYS } from '../api/twitchExtensionApi'

export const useMyTwitchExtensionConfiguration = (enabled = true) =>
  useQuery({
    queryKey: TWITCH_EXTENSION_QUERY_KEYS.mine,
    queryFn: () => twitchExtensionApi.mine(),
    enabled,
  })
