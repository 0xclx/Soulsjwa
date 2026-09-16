import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { OverlayToken } from '../../../types'
import { OVERLAY_DEFAULT_SETTINGS } from '../overlay/overlayConfig'
import {
  EditOverlayTokenDialog,
  FOLLOWS_WITHIN_REFRESH_MESSAGE,
  SAVE_LABEL,
  URL_DRIVEN_MESSAGE,
} from './EditOverlayTokenDialog'

const mocks = vi.hoisted(() => ({
  mutateAsync: vi.fn(),
}))

vi.mock('../hooks/useUpdateOverlayTokenSettings', () => ({
  useUpdateOverlayTokenSettings: () => ({ mutateAsync: mocks.mutateAsync, isPending: false }),
}))

vi.mock('../hooks/useScoreboard', () => ({
  useScoreboard: () => ({ data: undefined }),
}))

const token = (settings: OverlayToken['settings']): OverlayToken => ({
  id: 'tok-1',
  name: 'Main scene',
  tokenPrefix: 'abcdefgh',
  createdById: 'u1',
  createdAt: '2026-09-01T00:00:00.000Z',
  lastUsedAt: null,
  expiresAt: null,
  settings,
})

const games = [
  {
    eventGameId: 'g1',
    gameName: 'Dark Souls',
    isEnabled: true,
    hasActiveTrial: false,
    objectives: [],
  },
]

describe('<EditOverlayTokenDialog />', () => {
  beforeEach(() => {
    mocks.mutateAsync.mockReset().mockResolvedValue(undefined)
  })

  it('starts from the saved look and saves what is on screen against the token', async () => {
    const onClose = vi.fn()
    render(
      <EditOverlayTokenDialog
        token={token({ ...OVERLAY_DEFAULT_SETTINGS, view: 'scores', title: 'Finals' })}
        eventId="event-1"
        eventName="Lordran Relay"
        games={games}
        onClose={onClose}
      />,
    )

    expect(screen.getByText(FOLLOWS_WITHIN_REFRESH_MESSAGE)).toBeInTheDocument()
    expect(screen.getByLabelText(/title override/i)).toHaveValue('Finals')

    await userEvent.click(screen.getByLabelText('Highlight'))
    await userEvent.click(screen.getByRole('button', { name: SAVE_LABEL }))

    expect(mocks.mutateAsync).toHaveBeenCalledWith({
      tokenId: 'tok-1',
      settings: { ...OVERLAY_DEFAULT_SETTINGS, view: 'scores', title: 'Finals', highlight: false },
    })
    expect(onClose).toHaveBeenCalled()
  })

  it('explains that a token with no saved look is still driven by its URL, and starts from the defaults', () => {
    render(
      <EditOverlayTokenDialog
        token={token(null)}
        eventId="event-1"
        eventName="Lordran Relay"
        games={games}
        onClose={vi.fn()}
      />,
    )

    expect(screen.getByText(URL_DRIVEN_MESSAGE)).toBeInTheDocument()
    expect(screen.getByLabelText('Page size')).toHaveValue(OVERLAY_DEFAULT_SETTINGS.pageSize)
  })

  it('stays open and reports a failed save', async () => {
    mocks.mutateAsync.mockRejectedValue(new Error('nope'))
    const onClose = vi.fn()
    render(
      <EditOverlayTokenDialog
        token={token(OVERLAY_DEFAULT_SETTINGS)}
        eventId="event-1"
        eventName="Lordran Relay"
        games={games}
        onClose={onClose}
      />,
    )

    await userEvent.click(screen.getByRole('button', { name: SAVE_LABEL }))

    expect(await screen.findByText('nope')).toBeInTheDocument()
    expect(onClose).not.toHaveBeenCalled()
  })
})
