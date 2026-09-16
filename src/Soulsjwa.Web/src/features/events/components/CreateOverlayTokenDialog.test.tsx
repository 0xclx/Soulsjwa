import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { OverlayTokenWithSecret } from '../../../types'
import { OVERLAY_DEFAULT_SETTINGS } from '../overlay/overlayConfig'
import {
  CREATE_LABEL,
  CreateOverlayTokenDialog,
  LOOK_SAVED_MESSAGE,
  NAME_REQUIRED_MESSAGE,
} from './CreateOverlayTokenDialog'

const mocks = vi.hoisted(() => ({
  mutateAsync: vi.fn(),
}))

vi.mock('../hooks/useCreateOverlayToken', () => ({
  useCreateOverlayToken: () => ({ mutateAsync: mocks.mutateAsync, isPending: false }),
}))

vi.mock('../hooks/useScoreboard', () => ({
  useScoreboard: () => ({ data: undefined }),
}))

const RAW_TOKEN = 'ot_rawsecret'
const created: OverlayTokenWithSecret = {
  id: 'tok-1',
  name: 'Main scene',
  token: RAW_TOKEN,
  tokenPrefix: 'rawsecre',
  createdById: 'me',
  createdAt: '2026-09-01T00:00:00.000Z',
  lastUsedAt: null,
  expiresAt: null,
  settings: OVERLAY_DEFAULT_SETTINGS,
}

const competitors = [{ userId: 'me', displayName: 'Me' }]
const games = [
  {
    eventGameId: 'g1',
    gameName: 'Dark Souls',
    isEnabled: true,
    hasActiveTrial: false,
    objectives: [],
  },
]

const renderDialog = (currentUserId?: string) =>
  render(
    <CreateOverlayTokenDialog
      open
      eventId="event-1"
      eventUrlIdentifier="lordran-relay"
      eventName="Lordran Relay"
      competitors={competitors}
      games={games}
      currentUserId={currentUserId}
      onClose={vi.fn()}
    />,
  )

describe('<CreateOverlayTokenDialog />', () => {
  beforeEach(() => {
    mocks.mutateAsync.mockReset().mockResolvedValue(created)
  })

  it('mints the token together with the designed look, pinned to the competitor minting it', async () => {
    renderDialog('me')

    await userEvent.type(screen.getByLabelText(/token name/i), '  Main scene ')
    await userEvent.click(screen.getByLabelText('Progress'))
    await userEvent.click(screen.getByRole('button', { name: CREATE_LABEL }))

    expect(mocks.mutateAsync).toHaveBeenCalledWith({
      name: 'Main scene',
      settings: { ...OVERLAY_DEFAULT_SETTINGS, playerIds: ['me'], showProgress: false },
    })
  })

  it('reveals a URL carrying only the token, since the look lives on the token', async () => {
    renderDialog()

    await userEvent.type(screen.getByLabelText(/token name/i), 'Main scene')
    await userEvent.click(screen.getByRole('button', { name: CREATE_LABEL }))

    const url = (await screen.findByLabelText('Overlay URL')) as HTMLInputElement
    expect(url.value).toBe(
      `${window.location.origin}/events/lordran-relay/overlay?token=${RAW_TOKEN}`,
    )
    expect(screen.getByText(LOOK_SAVED_MESSAGE)).toBeInTheDocument()
  })

  it('does not mint without a name', async () => {
    renderDialog()

    await userEvent.click(screen.getByRole('button', { name: CREATE_LABEL }))

    expect(screen.getByText(NAME_REQUIRED_MESSAGE)).toBeInTheDocument()
    expect(mocks.mutateAsync).not.toHaveBeenCalled()
  })
})
