import { describe, expect, it, vi } from 'vitest'
import { OVERLAY_DEFAULT_SETTINGS } from './overlayConfig'
import {
  OVERLAY_PREVIEW_MESSAGE_TYPE,
  OVERLAY_PREVIEW_READY_MESSAGE_TYPE,
  isOverlayPreviewMessage,
  isOverlayPreviewReadyMessage,
  postOverlayPreview,
  postOverlayPreviewReady,
  type OverlayPreviewPayload,
} from './overlayPreviewProtocol'

const payload: OverlayPreviewPayload = {
  config: { ...OVERLAY_DEFAULT_SETTINGS, background: null },
  scoreboard: null,
  eventName: 'Lordran Relay',
}

const messageFrom = (origin: string, data: unknown) => new MessageEvent('message', { origin, data })

describe('overlay preview protocol', () => {
  it('posts the look and the ready signal to the same origin only', () => {
    const target = { postMessage: vi.fn() } as unknown as Window

    postOverlayPreview(target, payload)
    postOverlayPreviewReady(target)

    expect(target.postMessage).toHaveBeenNthCalledWith(
      1,
      { type: OVERLAY_PREVIEW_MESSAGE_TYPE, ...payload },
      window.location.origin,
    )
    expect(target.postMessage).toHaveBeenNthCalledWith(
      2,
      { type: OVERLAY_PREVIEW_READY_MESSAGE_TYPE },
      window.location.origin,
    )
  })

  it('recognises its own messages and nothing else', () => {
    const own = window.location.origin
    expect(
      isOverlayPreviewMessage(messageFrom(own, { type: OVERLAY_PREVIEW_MESSAGE_TYPE, ...payload })),
    ).toBe(true)
    expect(
      isOverlayPreviewReadyMessage(messageFrom(own, { type: OVERLAY_PREVIEW_READY_MESSAGE_TYPE })),
    ).toBe(true)

    // Another origin, another type, or no object at all: never a preview message.
    expect(
      isOverlayPreviewMessage(
        messageFrom('https://evil.example', { type: OVERLAY_PREVIEW_MESSAGE_TYPE, ...payload }),
      ),
    ).toBe(false)
    expect(isOverlayPreviewMessage(messageFrom(own, { type: 'something-else' }))).toBe(false)
    expect(isOverlayPreviewMessage(messageFrom(own, 'a string'))).toBe(false)
    expect(isOverlayPreviewMessage(messageFrom(own, null))).toBe(false)
    expect(
      isOverlayPreviewReadyMessage(messageFrom(own, { type: OVERLAY_PREVIEW_MESSAGE_TYPE })),
    ).toBe(false)
  })
})
