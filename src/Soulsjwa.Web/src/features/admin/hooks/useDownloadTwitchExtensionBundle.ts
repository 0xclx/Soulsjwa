import { useMutation } from '@tanstack/react-query'
import {
  twitchExtensionAdminApi,
  TWITCH_EXTENSION_BUNDLE_FILE_NAME,
} from '../api/twitchExtensionAdminApi'

/** Fetches the zip with the session's credentials and hands it to the browser as a file download. */
export const useDownloadTwitchExtensionBundle = () =>
  useMutation({
    mutationFn: async () => {
      const blob = await twitchExtensionAdminApi.downloadBundle()
      const url = URL.createObjectURL(blob)
      try {
        const anchor = document.createElement('a')
        anchor.href = url
        anchor.download = TWITCH_EXTENSION_BUNDLE_FILE_NAME
        document.body.appendChild(anchor)
        anchor.click()
        anchor.remove()
      } finally {
        URL.revokeObjectURL(url)
      }
    },
  })
