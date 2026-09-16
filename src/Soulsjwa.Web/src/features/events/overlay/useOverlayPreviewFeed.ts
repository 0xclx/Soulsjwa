import { useEffect, useState } from 'react'
import {
  isOverlayPreviewMessage,
  postOverlayPreviewReady,
  type OverlayPreviewPayload,
} from './overlayPreviewProtocol'

/**
 * The overlay route's data source in preview mode: whatever the embedding
 * window last posted (see `overlayPreviewProtocol.ts`), or `null` until it
 * has posted anything. Announces itself to the parent on mount so the parent
 * can send the current state straight away rather than waiting for its next
 * change. Only the embedding window is listened to, so a stray message from
 * another same-origin frame cannot redraw the preview. Opened directly
 * rather than embedded, `window.parent` is the page itself: the ready
 * message goes nowhere useful and nothing ever posts, which the page's
 * "waiting" state says.
 */
export const useOverlayPreviewFeed = (): OverlayPreviewPayload | null => {
  const [feed, setFeed] = useState<OverlayPreviewPayload | null>(null)

  useEffect(() => {
    const parent = window.parent
    const onMessage = (event: MessageEvent) => {
      if (event.source !== parent || !isOverlayPreviewMessage(event)) return
      const { config, scoreboard, eventName } = event.data
      setFeed({ config, scoreboard, eventName })
    }
    window.addEventListener('message', onMessage)
    postOverlayPreviewReady(parent)
    return () => window.removeEventListener('message', onMessage)
  }, [])

  return feed
}
