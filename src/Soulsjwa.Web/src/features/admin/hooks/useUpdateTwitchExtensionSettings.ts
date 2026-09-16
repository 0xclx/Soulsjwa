import { useMutation, useQueryClient } from '@tanstack/react-query'
import type { UpdateTwitchExtensionSettingsRequest } from '../../../types/twitchExtension'
import {
  twitchExtensionAdminApi,
  TWITCH_EXTENSION_ADMIN_QUERY_KEYS,
} from '../api/twitchExtensionAdminApi'

export const useUpdateTwitchExtensionSettings = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (body: UpdateTwitchExtensionSettingsRequest) =>
      twitchExtensionAdminApi.updateSettings(body),
    onSuccess: (info) => queryClient.setQueryData(TWITCH_EXTENSION_ADMIN_QUERY_KEYS.info, info),
  })
}
