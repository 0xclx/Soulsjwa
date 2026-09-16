import { useEffect, useState } from 'react'

export interface TwitchExtensionAuth {
  token: string
  channelId: string
  clientId: string
  opaqueUserId: string
}

export interface TwitchExtensionState {
  /** Null until Twitch has called `onAuthorized`; the helper does so on load and again on every token refresh. */
  auth: TwitchExtensionAuth | null
  theme: Twitch.ExtensionTheme
  /** False while Twitch reports the view hidden (collapsed panel, hidden overlay) — polling pauses. */
  isVisible: boolean
  /** True when the page runs outside Twitch (no helper on the window), e.g. opened directly during development. */
  helperMissing: boolean
}

const DEFAULT_THEME: Twitch.ExtensionTheme = 'dark'

/**
 * Subscribes to the Twitch Extension Helper once and mirrors what it reports
 * into React state. The helper is a global Twitch injects, so this is the one
 * place the bundle touches `window.Twitch`.
 */
export function useTwitchExtension(): TwitchExtensionState {
  const [auth, setAuth] = useState<TwitchExtensionAuth | null>(null)
  const [theme, setTheme] = useState<Twitch.ExtensionTheme>(DEFAULT_THEME)
  const [isVisible, setIsVisible] = useState(true)
  const [helperMissing, setHelperMissing] = useState(false)

  useEffect(() => {
    const ext = window.Twitch?.ext
    if (!ext) {
      // Deferred so the state update is not synchronous inside the effect.
      const handle = window.setTimeout(() => setHelperMissing(true), 0)
      return () => window.clearTimeout(handle)
    }

    ext.onAuthorized((next) => {
      setAuth({
        token: next.token,
        channelId: next.channelId,
        clientId: next.clientId,
        opaqueUserId: next.userId,
      })
    })
    ext.onContext((context, changed) => {
      if (context.theme && changed.includes('theme')) setTheme(context.theme)
      else if (context.theme) setTheme(context.theme)
    })
    ext.onVisibilityChanged((visible) => setIsVisible(visible))
  }, [])

  return { auth, theme, isVisible, helperMissing }
}
