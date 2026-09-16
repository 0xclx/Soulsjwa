import type { ScoreboardResponse } from '../../../types'
import type { OverlayConfig } from './overlayConfig'

/**
 * How the in-app designer drives a live preview of the overlay route.
 *
 * The overlay sizes its text against the viewport, so a faithful preview has
 * to be the real route in its own frame rather than the same components
 * inlined in the app. The frame is opened in preview mode
 * (`?preview=1`, see `OVERLAY_PREVIEW_PARAM`) and then does no polling of its
 * own: the parent window posts the look and the scoreboard to render, and
 * re-posts whenever either changes. That keeps the frame from reloading on
 * every edit (so page cycling and highlight flashes keep running) and means
 * a preview URL opened on its own renders nothing — there is no token-less
 * way to put live data on screen.
 *
 * Messages are same-origin only in both directions, and the frame accepts
 * them only from the window that embeds it.
 */

export const OVERLAY_PREVIEW_MESSAGE_TYPE = 'soulsjwa:overlay-preview'
export const OVERLAY_PREVIEW_READY_MESSAGE_TYPE = 'soulsjwa:overlay-preview-ready'

export interface OverlayPreviewPayload {
  config: OverlayConfig
  scoreboard: ScoreboardResponse | null
  eventName: string
}

export interface OverlayPreviewMessage extends OverlayPreviewPayload {
  type: typeof OVERLAY_PREVIEW_MESSAGE_TYPE
}

/** Sent by the frame once it is listening, so the parent knows to post the current state. */
export interface OverlayPreviewReadyMessage {
  type: typeof OVERLAY_PREVIEW_READY_MESSAGE_TYPE
}

const hasType = (data: unknown, type: string): boolean =>
  typeof data === 'object' && data !== null && (data as { type?: unknown }).type === type

export function isOverlayPreviewMessage(
  event: MessageEvent,
): event is MessageEvent<OverlayPreviewMessage> {
  return (
    event.origin === window.location.origin && hasType(event.data, OVERLAY_PREVIEW_MESSAGE_TYPE)
  )
}

export function isOverlayPreviewReadyMessage(
  event: MessageEvent,
): event is MessageEvent<OverlayPreviewReadyMessage> {
  return (
    event.origin === window.location.origin &&
    hasType(event.data, OVERLAY_PREVIEW_READY_MESSAGE_TYPE)
  )
}

export function postOverlayPreview(target: Window, payload: OverlayPreviewPayload): void {
  const message: OverlayPreviewMessage = { type: OVERLAY_PREVIEW_MESSAGE_TYPE, ...payload }
  target.postMessage(message, window.location.origin)
}

export function postOverlayPreviewReady(target: Window): void {
  const message: OverlayPreviewReadyMessage = { type: OVERLAY_PREVIEW_READY_MESSAGE_TYPE }
  target.postMessage(message, window.location.origin)
}
