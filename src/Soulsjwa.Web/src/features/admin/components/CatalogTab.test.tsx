import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { CatalogTab } from './CatalogTab'

const mocks = vi.hoisted(() => ({
  createGame: vi.fn(),
  updateGame: vi.fn(),
  createObjective: vi.fn(),
}))

vi.mock('../hooks/useAdminGames', () => ({
  useAdminGames: () => ({
    data: [
      {
        id: 1,
        name: 'Dark Souls',
        description: 'Original',
        connectorSupported: false,
      },
    ],
    isLoading: false,
    isError: false,
  }),
}))

vi.mock('../hooks/usePredefinedObjectiveCatalog', () => ({
  usePredefinedObjectiveCatalog: () => ({
    data: [{ id: 'objective-1', gameId: 1, name: 'Ring the bell', score: 10 }],
    isLoading: false,
    isError: false,
  }),
}))

vi.mock('../hooks/useCreateGame', () => ({
  useCreateGame: () => ({
    mutate: mocks.createGame,
    isPending: false,
    isError: false,
    error: null,
  }),
}))

vi.mock('../hooks/useUpdateGame', () => ({
  useUpdateGame: () => ({
    mutate: mocks.updateGame,
    isPending: false,
    isError: false,
    error: null,
  }),
}))

vi.mock('../hooks/useCreatePredefinedObjective', () => ({
  useCreatePredefinedObjective: () => ({
    mutate: mocks.createObjective,
    isPending: false,
    isError: false,
    error: null,
  }),
}))

describe('CatalogTab', () => {
  beforeEach(() => vi.clearAllMocks())

  it('browses games and their predefined objectives', () => {
    render(<CatalogTab />)

    expect(screen.getAllByText('Dark Souls')).toHaveLength(2)
    expect(screen.getByText('Ring the bell')).toBeInTheDocument()
    expect(screen.getByText('10')).toBeInTheDocument()
  })

  it('creates a predefined objective for the selected game', async () => {
    const user = userEvent.setup()
    render(<CatalogTab />)

    await user.click(screen.getByRole('button', { name: 'Create objective' }))
    const dialog = await screen.findByRole('dialog')
    await screen.findByText('Create predefined objective')
    await user.type(within(dialog).getByLabelText('Name', { exact: false }), 'Defeat the boss')
    const score = within(dialog).getByRole('spinbutton')
    await user.clear(score)
    await user.type(score, '50')
    await user.click(screen.getByRole('button', { name: 'Create' }))

    expect(mocks.createObjective).toHaveBeenCalledWith(
      { gameId: 1, name: 'Defeat the boss', score: 50 },
      expect.objectContaining({ onSuccess: expect.any(Function) }),
    )
  })
})
