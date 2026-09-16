import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { BulkCreateObjectiveDialog } from './BulkCreateObjectiveDialog'
import type { EventGame } from '../../../types'

const mocks = vi.hoisted(() => ({
  mutateAsync: vi.fn(),
}))

vi.mock('../hooks/useCreateObjective', () => ({
  useCreateObjective: () => ({
    mutateAsync: mocks.mutateAsync,
    isPending: false,
  }),
}))

const game: EventGame = {
  eventGameId: 'game-1',
  gameName: 'Elden Ring',
  knownGameId: null,
  knownGameName: null,
  isCustomGame: true,
  customGameDescription: null,
  isEnabled: true,
  objectives: [],
} as unknown as EventGame

describe('BulkCreateObjectiveDialog', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mocks.mutateAsync.mockResolvedValue(undefined)
  })

  const onClose = vi.fn()

  it('starts with a single blank row and adds/removes rows', async () => {
    const user = userEvent.setup()
    render(<BulkCreateObjectiveDialog open eventId="event-1" game={game} onClose={onClose} />)

    expect(screen.getAllByLabelText(/Objective name/)).toHaveLength(1)
    expect(screen.getByLabelText('Remove objective row')).toBeDisabled()

    await user.click(screen.getByRole('button', { name: 'Add another objective' }))
    expect(screen.getAllByLabelText(/Objective name/)).toHaveLength(2)

    const removeButtons = screen.getAllByLabelText('Remove objective row')
    expect(removeButtons.length).toBeGreaterThan(0)
    await user.click(removeButtons[0]!)
    expect(screen.getAllByLabelText(/Objective name/)).toHaveLength(1)
  })

  it('disables submit when a row is invalid', async () => {
    render(<BulkCreateObjectiveDialog open eventId="event-1" game={game} onClose={onClose} />)

    const submit = screen.getByRole('button', { name: 'Create objectives' })
    expect(submit).toBeDisabled()
  })

  it('submits N valid rows resulting in N mutateAsync calls', async () => {
    const user = userEvent.setup()
    render(<BulkCreateObjectiveDialog open eventId="event-1" game={game} onClose={onClose} />)

    const names = screen.getAllByLabelText(/Objective name/)
    await user.type(names[0]!, 'Defeat boss 1')

    await user.click(screen.getByRole('button', { name: 'Add another objective' }))
    const names2 = screen.getAllByLabelText(/Objective name/)
    await user.type(names2[1]!, 'Defeat boss 2')

    const submit = screen.getByRole('button', { name: 'Create objectives' })
    expect(submit).toBeEnabled()
    await user.click(submit)

    expect(mocks.mutateAsync).toHaveBeenCalledTimes(2)
    expect(mocks.mutateAsync).toHaveBeenNthCalledWith(1, {
      eventId: 'event-1',
      eventGameId: 'game-1',
      payload: { name: 'Defeat boss 1', score: 0, category: undefined },
    })
    expect(mocks.mutateAsync).toHaveBeenNthCalledWith(2, {
      eventId: 'event-1',
      eventGameId: 'game-1',
      payload: { name: 'Defeat boss 2', score: 0, category: undefined },
    })
  })

  it('keeps failed rows visible and shows Retry failed on partial failure', async () => {
    const user = userEvent.setup()
    mocks.mutateAsync.mockImplementationOnce(() => Promise.resolve())
    mocks.mutateAsync.mockImplementationOnce(() => Promise.reject(new Error('boom')))

    render(<BulkCreateObjectiveDialog open eventId="event-1" game={game} onClose={onClose} />)

    const names = screen.getAllByLabelText(/Objective name/)
    await user.type(names[0]!, 'Succeeds')

    await user.click(screen.getByRole('button', { name: 'Add another objective' }))
    const names2 = screen.getAllByLabelText(/Objective name/)
    await user.type(names2[1]!, 'Fails')

    const submit = screen.getByRole('button', { name: 'Create objectives' })
    await user.click(submit)

    expect(await screen.findByRole('button', { name: /Retry failed \(1\)/ })).toBeInTheDocument()
    expect(screen.queryByDisplayValue('Succeeds')).not.toBeInTheDocument()
    expect(screen.getByDisplayValue('Fails')).toBeInTheDocument()
    expect(screen.getByText('Failed to create objective.')).toBeInTheDocument()
    expect(onClose).not.toHaveBeenCalled()
  })
})
