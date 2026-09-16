/**
 * The slice of Twitch's Extension Helper this bundle uses. The helper is
 * loaded from Twitch's own host in every entry page (the only external script
 * Twitch's CSP permits) and puts itself on `window.Twitch.ext`. Reference:
 * https://dev.twitch.tv/docs/extensions/reference/
 */
export {}

declare global {
  namespace Twitch {
    interface ExtensionAuth {
      /** JWT Twitch signed for this viewer; the bearer token the API verifies. */
      token: string
      channelId: string
      clientId: string
      /** Opaque viewer id (`U…` signed in, `A…` anonymous). */
      userId: string
      helixToken?: string
    }

    type ExtensionTheme = 'light' | 'dark'
    type ExtensionMode = 'viewer' | 'dashboard' | 'config'

    interface ExtensionContext {
      theme?: ExtensionTheme
      mode?: ExtensionMode
      isFullScreen?: boolean
      isTheatreMode?: boolean
      arePlayerControlsVisible?: boolean
      language?: string
    }

    type ExtensionPubSubTarget = 'broadcast' | 'global' | `whisper-${string}`

    interface Extension {
      onAuthorized(callback: (auth: ExtensionAuth) => void): void
      onContext(
        callback: (
          context: ExtensionContext,
          changed: ReadonlyArray<keyof ExtensionContext>,
        ) => void,
      ): void
      onVisibilityChanged(callback: (isVisible: boolean, context?: ExtensionContext) => void): void
      listen(
        target: ExtensionPubSubTarget,
        callback: (target: string, contentType: string, message: string) => void,
      ): void
      unlisten(
        target: ExtensionPubSubTarget,
        callback: (target: string, contentType: string, message: string) => void,
      ): void
    }
  }

  /** Written by `extension-config.js`, which the API rewrites per deployment when the zip is downloaded. */
  interface SoulsjwaTwitchExtensionConfig {
    apiUrl: string
  }

  interface Window {
    Twitch?: { ext: Twitch.Extension }
    SOULSJWA_TWITCH_EXTENSION?: SoulsjwaTwitchExtensionConfig
  }
}
