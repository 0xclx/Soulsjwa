import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { GameCard, type GameCardProps } from './GameCard'
import {
  TRIAL_DORMANT_BLOCKS_OFFICIAL,
  TRIAL_RECORDING_BLOCKS_OFFICIAL,
} from '../scoreboard/trialPresentation'
import type { EventGame, Objective } from '../../../types'

const mocks = vi.hoisted(() => ({
  mutate: vi.fn(),
}))

vi.mock('../hooks/useReorderObjectives', () => ({
  useReorderObjectives: () => ({
    mutate: mocks.mutate,
    isPending: false,
  }),
}))

// jsdom doesn't implement DataTransfer, but useDragReorder reads/writes it.
const makeDataTransfer = () => ({ effectAllowed: '', dropEffect: '', setDragImage: vi.fn() })

const objective = (id: string, name: string, category: string | null): Objective =>
  ({
    id,
    name,
    score: 10,
    category,
    isPredefined: false,
  }) as Objective

const game: EventGame = {
  eventGameId: 'game-1',
  gameName: 'Elden Ring',
  knownGameId: 1,
  knownGameName: 'Elden Ring',
  isCustomGame: false,
  customGameDescription: null,
  isEnabled: false,
  objectives: [
    objective('o1', 'Kill Margit', 'Weeping Peninsula'),
    objective('o2', 'Kill Godrick', 'Limgrave'),
    objective('o3', 'Kill Radahn', 'Limgrave'),
    objective('o4', 'Explore cave', null),
  ],
} as unknown as EventGame

const baseProps: GameCardProps = {
  game,
  eventId: 'event-1',
  connectorSupported: false,
  canManageEvent: true,
  objectivesEditable: true,
  completedObjectiveIds: new Set(),
  completedObjectiveTimes: new Map(),
  failedObjectiveIds: new Set(),
  failedObjectiveTimes: new Map(),
  canToggleCompletion: false,
  canEditCompletionTimes: false,
  toggleDisabled: false,
  onToggleCompletion: vi.fn(),
  onToggleFailure: vi.fn(),
  onEditObjective: vi.fn(),
  onDeleteObjective: vi.fn(),
  onEditCompletionTime: vi.fn(),
  isFormOpen: false,
  onOpenForm: vi.fn(),
  onCloseForm: vi.fn(),
  onRemove: vi.fn(),
  onToggleEnabled: vi.fn(),
  eventIsStarted: false,
}

const renderGameCard = (overrides: Partial<GameCardProps> = {}) => {
  const client = new QueryClient()
  return render(
    <QueryClientProvider client={client}>
      <GameCard {...baseProps} {...overrides} />
    </QueryClientProvider>,
  )
}

describe('GameCard', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('renders objectives grouped by category in order, with an "Uncategorized" group last', () => {
    renderGameCard()

    const headings = screen.getAllByText(/Weeping Peninsula|Limgrave|Uncategorized/)
    expect(headings.map((h) => h.textContent)).toEqual([
      'Weeping Peninsula',
      'Limgrave',
      'Uncategorized',
    ])

    const items = screen.getAllByRole('listitem')
    expect(items.map((li) => li.textContent)).toEqual([
      expect.stringContaining('Kill Margit'),
      expect.stringContaining('Kill Godrick'),
      expect.stringContaining('Kill Radahn'),
      expect.stringContaining('Explore cave'),
    ])
  })

  it('dragging an objective onto another within the same category reorders and calls useReorderObjectives', () => {
    renderGameCard()

    // jsdom reports a zero-size bounding rect for every element, so the
    // dragover handler's before/after midpoint check always resolves to
    // 'after' here — dragging Godrick onto Radahn's row drops it right
    // after Radahn, i.e. swaps the two. The 'before' half of that math is
    // covered precisely by useDragReorder.test.ts, which controls clientY
    // and the row rect directly instead of going through real DOM events.
    const handle = screen.getByLabelText('Reorder Kill Godrick')
    const targetRow = screen.getByText('Kill Radahn').closest('li')!

    fireEvent.dragStart(handle, { dataTransfer: makeDataTransfer() })
    fireEvent.dragOver(targetRow, { dataTransfer: makeDataTransfer() })
    fireEvent.drop(targetRow, { dataTransfer: makeDataTransfer() })

    expect(mocks.mutate).toHaveBeenCalledWith({
      eventGameId: 'game-1',
      objectiveIds: ['o1', 'o3', 'o2', 'o4'],
    })
  })

  it('dragging a category onto another reorders and calls useReorderObjectives', () => {
    renderGameCard()

    const handle = screen.getByLabelText('Reorder Weeping Peninsula category')
    const targetRow = screen.getByText('Limgrave').closest('div')!.parentElement!

    fireEvent.dragStart(handle, { dataTransfer: makeDataTransfer() })
    fireEvent.dragOver(targetRow, { dataTransfer: makeDataTransfer() })
    fireEvent.drop(targetRow, { dataTransfer: makeDataTransfer() })

    expect(mocks.mutate).toHaveBeenCalledWith({
      eventGameId: 'game-1',
      objectiveIds: ['o2', 'o3', 'o1', 'o4'],
    })
  })

  it('shows the caller-supplied reason its official controls are read-only', () => {
    // canToggleCompletion is true here on purpose: baseProps has it false, so
    // without this the checkbox would already be read-only and the test would
    // prove only that copy renders.
    renderGameCard({
      canToggleCompletion: true,
      trialBlockReason: TRIAL_RECORDING_BLOCKS_OFFICIAL,
    })

    expect(screen.getByText(/A trial run is recording for this game/)).toBeInTheDocument()
    expect(screen.getByText(/Trial runs tab/)).toBeInTheDocument()
  })

  it('shows the dormant-trial reason verbatim too, rather than assuming a recording run', () => {
    renderGameCard({
      canToggleCompletion: true,
      trialBlockReason: TRIAL_DORMANT_BLOCKS_OFFICIAL,
    })

    expect(screen.getByText(/not recording/)).toBeInTheDocument()
    expect(screen.queryByText(/is recording for this game/)).not.toBeInTheDocument()
  })

  it('says nothing about trials when trial mode is off', () => {
    renderGameCard({ canToggleCompletion: true })
    expect(screen.queryByText(/trial/i)).not.toBeInTheDocument()
  })

  it('disables the Enable button when the event has not started', () => {
    renderGameCard({ eventIsStarted: false })
    expect(screen.getByLabelText('Enable Elden Ring')).toBeDisabled()
  })

  it('enables the Enable button once the event has started', () => {
    renderGameCard({ eventIsStarted: true })
    expect(screen.getByLabelText('Enable Elden Ring')).toBeEnabled()
  })

  it('disables the Remove button while the event is started', () => {
    renderGameCard({ eventIsStarted: true })
    expect(screen.getByLabelText('Remove Elden Ring')).toBeDisabled()
  })

  it('enables the Remove button while the event is stopped', () => {
    renderGameCard({ eventIsStarted: false })
    expect(screen.getByLabelText('Remove Elden Ring')).toBeEnabled()
  })

  it('shows management controls for a non-creator admin (canManageEvent true)', () => {
    renderGameCard({ canManageEvent: true })
    expect(screen.getByLabelText('Edit Elden Ring')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Add Objective' })).toBeInTheDocument()
  })

  it('hides management controls when canManageEvent is false', () => {
    renderGameCard({ canManageEvent: false })
    expect(screen.queryByLabelText('Edit Elden Ring')).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Add Objective' })).not.toBeInTheDocument()
  })
})
