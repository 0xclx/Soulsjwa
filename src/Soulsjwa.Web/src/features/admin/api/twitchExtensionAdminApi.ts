import { apiClient } from '../../../lib/axios'
import type {
  TwitchExtensionAdminInfo,
  UpdateTwitchExtensionSettingsRequest,
} from '../../../types/twitchExtension'

export const TWITCH_EXTENSION_ADMIN_QUERY_KEYS = {
  info: ['admin', 'twitch-extension'] as const,
}

const ROUTE = '/admin/twitch-extension'

/** Name the browser saves the download under; the server sends the same one. */
export const TWITCH_EXTENSION_BUNDLE_FILE_NAME = 'soulsjwa-twitch-extension.zip'

/**
 * The admin page's side of the Twitch extension: status of the configured
 * credentials, the extension-wide rules, and the zip to upload to Twitch.
 */
export const twitchExtensionAdminApi = {
  info: async (): Promise<TwitchExtensionAdminInfo> => {
    const { data } = await apiClient.get<TwitchExtensionAdminInfo>(ROUTE)
    return data
  },

  updateSettings: async (
    body: UpdateTwitchExtensionSettingsRequest,
  ): Promise<TwitchExtensionAdminInfo> => {
    const { data } = await apiClient.put<TwitchExtensionAdminInfo>(`${ROUTE}/settings`, body)
    return data
  },

  /** The zip as a blob; the caller hands it to the browser as a download. */
  downloadBundle: async (): Promise<Blob> => {
    const { data } = await apiClient.get<Blob>(`${ROUTE}/bundle`, { responseType: 'blob' })
    return data
  },
}
