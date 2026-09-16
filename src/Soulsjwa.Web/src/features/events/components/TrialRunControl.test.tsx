import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi, beforeEach } from 'vitest'
import { TrialRunControl } from './TrialRunControl'
import type { TrialRun } from '../../../types'

const baseTrialRun: TrialRun = {
  id: 'trial-1',
  eventId: 'event-1',
  eventGameId: 'game-1',
  userId: 'user-1',
  state: 'NotStarted',
  startedAt: null,
  endedAt: null,
}

const mocks = vi.hoisted(() => ({
  data: null as TrialRun | null,
  isLoading: false,
  enable: vi.fn(),
  disable: vi.fn(),
  start: vi.fn(),
  stop: vi.fn(),
  reset: vi.fn(),
}))

vi.mock('../hooks/useTrialRun', () => ({
  useTrialRun: () => ({ data: mocks.data, isLoading: mocks.isLoading }),
}))
vi.mock('../hooks/useEnableTrialRun', () => ({
  useEnableTrialRun: () => ({ mutate: mocks.enable, isPending: false }),
}))
vi.mock('../hooks/useDisableTrialRun', () => ({
  useDisableTrialRun: () => ({ mutate: mocks.disable, isPending: false }),
}))
vi.mock('../hooks/useStartTrialRun', () => ({
  useStartTrialRun: () => ({ mutate: mocks.start, isPending: false }),
}))
vi.mock('../hooks/useStopTrialRun', () => ({
  useStopTrialRun: () => ({ mutate: mocks.stop, isPending: false }),
}))
vi.mock('../hooks/useResetTrialRun', () => ({
  useResetTrialRun: () => ({ mutate: mocks.reset, isPending: false }),
}))

const renderControl = () =>
  render(
    <TrialRunControl
      eventId="event-1"
      eventGameId="game-1"
      gameName="Elden Ring"
      userId="user-1"
    />,
  )

describe('TrialRunControl', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mocks.data = null
    mocks.isLoading = false
  })

  it('shows an Enable button when no trial run exists', async () => {
    renderControl()
    await userEvent.click(screen.getByRole('button', { name: /enable trial/i }))
    expect(mocks.enable).toHaveBeenCalled()
  })

  it('shows the state and Start/Stop/Reset/Disable controls once enabled', async () => {
    mocks.data = baseTrialRun
    renderControl()

    // The chip shows the human label, not the raw server enum.
    expect(screen.getByText(/Trial: Not started/)).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: /^start$/i }))
    expect(mocks.start).toHaveBeenCalled()
  })

  it('shows Stop instead of Start while Running', async () => {
    mocks.data = { ...baseTrialRun, state: 'Running' }
    renderControl()

    expect(screen.queryByRole('button', { name: /^start$/i })).not.toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: /^stop$/i }))
    expect(mocks.stop).toHaveBeenCalled()
  })

  it('requires typing the game name before disable actually calls the mutation', async () => {
    mocks.data = baseTrialRun
    renderControl()

    await userEvent.click(screen.getByRole('button', { name: /^disable$/i }))
    const confirmButton = screen.getByRole('button', { name: /disable trial mode/i })
    expect(confirmButton).toBeDisabled()

    await userEvent.type(screen.getByLabelText('Game name'), 'Elden Ring')
    expect(confirmButton).toBeEnabled()
    await userEvent.click(confirmButton)
    expect(mocks.disable).toHaveBeenCalled()
  })

  it('renders nothing while loading', () => {
    mocks.isLoading = true
    const { container } = renderControl()
    expect(container).toBeEmptyDOMElement()
  })
})
