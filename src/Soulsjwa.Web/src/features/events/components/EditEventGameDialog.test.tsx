import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { EditEventGameDialog } from './EditEventGameDialog'
import type { EventGame } from '../../../types'

const mocks = vi.hoisted(() => ({
  mutate: vi.fn(),
}))

vi.mock('../hooks/useEditEventGame', () => ({
  useEditEventGame: () => ({
    mutate: mocks.mutate,
    isPending: false,
  }),
}))

const customGame: EventGame = {
  eventGameId: 'game-1',
  gameName: 'My Custom Run',
  knownGameId: null,
  knownGameName: null,
  isCustomGame: true,
  customGameDescription: 'A description',
  isEnabled: true,
  objectives: [],
} as unknown as EventGame

const predefinedGame: EventGame = {
  eventGameId: 'game-2',
  gameName: 'Elden Ring',
  knownGameId: 1,
  knownGameName: 'Elden Ring',
  isCustomGame: false,
  customGameDescription: null,
  isEnabled: true,
  objectives: [],
} as unknown as EventGame

const predefinedGameRenamed: EventGame = {
  ...predefinedGame,
  eventGameId: 'game-3',
  gameName: 'Elden Ring (Randomizer)',
} as unknown as EventGame

describe('EditEventGameDialog', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  const onClose = vi.fn()

  it('prefills name/description for a custom game', () => {
    render(<EditEventGameDialog open eventId="event-1" game={customGame} onClose={onClose} />)
    expect(screen.getByLabelText('Name')).toHaveValue('My Custom Run')
    expect(screen.getByLabelText('Description')).toHaveValue('A description')
  })

  it('prefills an empty name for a predefined game using the catalog name', () => {
    render(<EditEventGameDialog open eventId="event-1" game={predefinedGame} onClose={onClose} />)
    expect(screen.getByLabelText('Name')).toHaveValue('')
    expect(
      screen.getByText(/Leave blank to use the catalog name \("Elden Ring"\)/),
    ).toBeInTheDocument()
  })

  it('prefills the overridden name for a renamed predefined game', () => {
    render(
      <EditEventGameDialog open eventId="event-1" game={predefinedGameRenamed} onClose={onClose} />,
    )
    expect(screen.getByLabelText('Name')).toHaveValue('Elden Ring (Randomizer)')
  })

  it('blocks submit with an empty name for a custom game', async () => {
    const user = userEvent.setup()
    render(<EditEventGameDialog open eventId="event-1" game={customGame} onClose={onClose} />)

    await user.clear(screen.getByLabelText('Name'))
    await user.click(screen.getByRole('button', { name: 'Save' }))

    expect(screen.getByText('Name is required for a custom game.')).toBeInTheDocument()
    expect(mocks.mutate).not.toHaveBeenCalled()
  })

  it('allows an empty name for a predefined game and submits trimmed values', async () => {
    const user = userEvent.setup()
    render(<EditEventGameDialog open eventId="event-1" game={predefinedGame} onClose={onClose} />)

    await user.type(screen.getByLabelText('Description'), '  new desc  ')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    expect(mocks.mutate).toHaveBeenCalledWith(
      { eventGameId: 'game-2', name: '', description: 'new desc' },
      expect.anything(),
    )
  })

  it('submits trimmed name and description for a custom game', async () => {
    const user = userEvent.setup()
    render(<EditEventGameDialog open eventId="event-1" game={customGame} onClose={onClose} />)

    await user.clear(screen.getByLabelText('Name'))
    await user.type(screen.getByLabelText('Name'), '  Renamed Run  ')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    expect(mocks.mutate).toHaveBeenCalledWith(
      { eventGameId: 'game-1', name: 'Renamed Run', description: 'A description' },
      expect.anything(),
    )
  })
})
