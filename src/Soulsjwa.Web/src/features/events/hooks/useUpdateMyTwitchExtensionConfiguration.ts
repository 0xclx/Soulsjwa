import { useMutation, useQueryClient } from '@tanstack/react-query'
import type { UpdateTwitchExtensionConfigurationRequest } from '../../../types/twitchExtension'
import { twitchExtensionApi, TWITCH_EXTENSION_QUERY_KEYS } from '../api/twitchExtensionApi'

export const useUpdateMyTwitchExtensionConfiguration = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (body: UpdateTwitchExtensionConfigurationRequest) =>
      twitchExtensionApi.updateMine(body),
    onSuccess: (configuration) =>
      queryClient.setQueryData(TWITCH_EXTENSION_QUERY_KEYS.mine, configuration),
  })
}
