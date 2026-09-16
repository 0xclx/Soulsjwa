import { useQuery } from '@tanstack/react-query'
import {
  twitchExtensionAdminApi,
  TWITCH_EXTENSION_ADMIN_QUERY_KEYS,
} from '../api/twitchExtensionAdminApi'

export const useTwitchExtensionAdmin = () =>
  useQuery({
    queryKey: TWITCH_EXTENSION_ADMIN_QUERY_KEYS.info,
    queryFn: () => twitchExtensionAdminApi.info(),
  })
