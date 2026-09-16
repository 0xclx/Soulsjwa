import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { ScoreboardResponse } from '../../../types'
import { OVERLAY_DEFAULT_SETTINGS, OVERLAY_PREVIEW_PARAM } from '../overlay/overlayConfig'
import {
  OVERLAY_PREVIEW_MESSAGE_TYPE,
  OVERLAY_PREVIEW_READY_MESSAGE_TYPE,
  type OverlayPreviewMessage,
} from '../overlay/overlayPreviewProtocol'
import { SAMPLE_COMPETITORS } from '../overlay/overlaySampleData'
import { OverlayPreview, SAMPLE_DATA_LABEL, SIMULATE_COMPLETION_LABEL } from './OverlayPreview'

const EVENT_ID = 'event-1'
const EVENT_NAME = 'Lordran Relay'
const LIVE_PLAYER = 'Chosen Streamer'

const liveScoreboard: ScoreboardResponse = {
  tieBreakMode: 'ByTime',
  entries: [
    {
      userId: 'u1',
      displayName: LIVE_PLAYER,
      twitchLogin: 'chosen',
      isLive: true,
      totalScore: 0,
      completedCount: 0,
      isFinished: false,
      lastCompletedAt: null,
      totalInGameTimeMs: null,
      rank: 1,
      failedCount: 0,
      status: 'Pending',
      games: [],
    },
  ],
}

const mocks = vi.hoisted(() => ({ scoreboard: null as ScoreboardResponse | null }))

vi.mock('../hooks/useScoreboard', () => ({
  useScoreboard: () => ({ data: mocks.scoreboard }),
}))

const games = [{ eventGameId: 'g1', gameName: 'Dark Souls', isEnabled: true, objectives: [] }]

const frame = () => screen.getByTitle('Overlay preview') as HTMLIFrameElement

/** The frame saying it listens, as the real route does on mount. */
const frameReady = () =>
  act(() => {
    window.dispatchEvent(
      new MessageEvent('message', {
        data: { type: OVERLAY_PREVIEW_READY_MESSAGE_TYPE },
        origin: window.location.origin,
        source: frame().contentWindow,
      }),
    )
  })

const lastPosted = (postMessage: ReturnType<typeof vi.fn>): OverlayPreviewMessage =>
  postMessage.mock.calls.at(-1)![0] as OverlayPreviewMessage

describe('<OverlayPreview />', () => {
  beforeEach(() => {
    mocks.scoreboard = liveScoreboard
  })

  it('embeds the overlay route in preview mode, never with a token', () => {
    render(
      <OverlayPreview
        eventId={EVENT_ID}
        eventName={EVENT_NAME}
        games={games}
        settings={OVERLAY_DEFAULT_SETTINGS}
      />,
    )

    const src = frame().getAttribute('src')!
    expect(src).toBe(`/events/${EVENT_ID}/overlay?${OVERLAY_PREVIEW_PARAM}=1`)
    expect(src).not.toContain('token')
  })

  it('posts the look and live data once the frame is ready, and again on every change', () => {
    const { rerender } = render(
      <OverlayPreview
        eventId={EVENT_ID}
        eventName={EVENT_NAME}
        games={games}
        settings={OVERLAY_DEFAULT_SETTINGS}
      />,
    )
    const postMessage = vi.fn()
    vi.spyOn(frame().contentWindow!, 'postMessage').mockImplementation(postMessage)

    // Nothing is posted into a frame that has not said it is listening.
    expect(postMessage).not.toHaveBeenCalled()

    frameReady()
    expect(postMessage).toHaveBeenCalledTimes(1)
    const first = lastPosted(postMessage)
    expect(first.type).toBe(OVERLAY_PREVIEW_MESSAGE_TYPE)
    expect(first.eventName).toBe(EVENT_NAME)
    expect(first.config.view).toBe('objectives')
    // The backdrop is drawn behind the frame; the page itself stays transparent as in OBS.
    expect(first.config.background).toBeNull()
    expect(first.scoreboard?.entries[0]?.displayName).toBe(LIVE_PLAYER)
    expect(postMessage.mock.calls.at(-1)![1]).toBe(window.location.origin)

    rerender(
      <OverlayPreview
        eventId={EVENT_ID}
        eventName={EVENT_NAME}
        games={games}
        settings={{ ...OVERLAY_DEFAULT_SETTINGS, view: 'scores', panelOpacity: 40 }}
      />,
    )
    expect(postMessage).toHaveBeenCalledTimes(2)
    expect(lastPosted(postMessage).config.view).toBe('scores')
    expect(lastPosted(postMessage).config.panelOpacity).toBe(40)

    // A frame that reloads announces again and is fed again, unchanged look and all.
    frameReady()
    expect(postMessage).toHaveBeenCalledTimes(3)
    expect(lastPosted(postMessage).config.view).toBe('scores')
  })

  it('in the objectives view, simulates a completion for the competitor on screen', async () => {
    const leader = {
      ...liveScoreboard.entries[0]!,
      userId: 'u2',
      displayName: 'Leader',
      totalScore: 50,
    }
    mocks.scoreboard = { ...liveScoreboard, entries: [liveScoreboard.entries[0]!, leader] }
    render(
      <OverlayPreview
        eventId={EVENT_ID}
        eventName={EVENT_NAME}
        games={games}
        settings={{ ...OVERLAY_DEFAULT_SETTINGS, view: 'objectives' }}
      />,
    )
    const postMessage = vi.fn()
    vi.spyOn(frame().contentWindow!, 'postMessage').mockImplementation(postMessage)
    frameReady()

    await userEvent.click(screen.getByRole('button', { name: SIMULATE_COMPLETION_LABEL }))
    await userEvent.click(screen.getByRole('button', { name: SIMULATE_COMPLETION_LABEL }))

    // The leader is who the objectives view shows, so only they complete anything — twice.
    const entries = lastPosted(postMessage).scoreboard!.entries
    expect(entries.find((e) => e.userId === 'u2')!.lastCompletedAt).not.toBeNull()
    expect(entries.find((e) => e.userId === 'u1')!.lastCompletedAt).toBeNull()
  })

  it('in the scores view, simulates a completion for the next competitor in turn', async () => {
    const other = { ...liveScoreboard.entries[0]!, userId: 'u2', displayName: 'Other Player' }
    mocks.scoreboard = { ...liveScoreboard, entries: [liveScoreboard.entries[0]!, other] }
    render(
      <OverlayPreview
        eventId={EVENT_ID}
        eventName={EVENT_NAME}
        games={games}
        settings={{ ...OVERLAY_DEFAULT_SETTINGS, view: 'scores' }}
      />,
    )
    const postMessage = vi.fn()
    vi.spyOn(frame().contentWindow!, 'postMessage').mockImplementation(postMessage)
    frameReady()

    await userEvent.click(screen.getByRole('button', { name: SIMULATE_COMPLETION_LABEL }))
    let entries = lastPosted(postMessage).scoreboard!.entries
    expect(entries[0]!.lastCompletedAt).not.toBeNull()
    expect(entries[1]!.lastCompletedAt).toBeNull()

    // The next press moves on to the next competitor, so every page gets its turn.
    await userEvent.click(screen.getByRole('button', { name: SIMULATE_COMPLETION_LABEL }))
    entries = lastPosted(postMessage).scoreboard!.entries
    expect(entries[1]!.lastCompletedAt).not.toBeNull()
  })

  it('swaps in the fictional scoreboard when sample data is switched on', async () => {
    render(
      <OverlayPreview
        eventId={EVENT_ID}
        eventName={EVENT_NAME}
        games={games}
        settings={OVERLAY_DEFAULT_SETTINGS}
      />,
    )
    const postMessage = vi.fn()
    vi.spyOn(frame().contentWindow!, 'postMessage').mockImplementation(postMessage)
    frameReady()

    await userEvent.click(screen.getByLabelText(SAMPLE_DATA_LABEL))

    const names = lastPosted(postMessage).scoreboard!.entries.map((e) => e.displayName)
    expect(names).toHaveLength(SAMPLE_COMPETITORS.length)
    expect(names).not.toContain(LIVE_PLAYER)
    expect(SAMPLE_COMPETITORS.map((c) => c.displayName)).toEqual(expect.arrayContaining(names))
  })
})
