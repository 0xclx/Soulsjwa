import { apiClient } from '../../../lib/axios'
import type {
  TwitchExtensionConfiguration,
  TwitchExtensionStatus,
  UpdateTwitchExtensionConfigurationRequest,
} from '../../../types/twitchExtension'

export const TWITCH_EXTENSION_QUERY_KEYS = {
  status: ['twitch-extension', 'status'] as const,
  mine: ['me', 'twitch-extension'] as const,
}

/** Twitch's extension manager for a channel, where the broadcaster installs and activates an extension. */
export const twitchExtensionInstallUrl = (clientId: string): string =>
  `https://dashboard.twitch.tv/extensions/${encodeURIComponent(clientId)}`

/**
 * The web app's side of the Twitch extension: whether this server backs one,
 * and the signed-in user's own channel settings (the same settings the
 * extension's config view edits, keyed on the user's Twitch id).
 */
export const twitchExtensionApi = {
  status: async (): Promise<TwitchExtensionStatus> => {
    const { data } = await apiClient.get<TwitchExtensionStatus>('/twitch-extension/status')
    return data
  },

  mine: async (): Promise<TwitchExtensionConfiguration> => {
    const { data } = await apiClient.get<TwitchExtensionConfiguration>('/me/twitch-extension')
    return data
  },

  updateMine: async (
    body: UpdateTwitchExtensionConfigurationRequest,
  ): Promise<TwitchExtensionConfiguration> => {
    const { data } = await apiClient.put<TwitchExtensionConfiguration>('/me/twitch-extension', body)
    return data
  },
}
