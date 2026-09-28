import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { CompetitorGameDialog } from './CompetitorGameDialog'
import type {
  CompetitorInfo,
  GameBreakdown,
  ObjectiveDetail,
  ScoreboardEntry,
  TrialProgress,
} from '../../../../types'

// The editor fetches and mutates on its own; here only its gating matters.
vi.mock('../CompetitorInfosEditor', () => ({
  CompetitorInfosEditor: ({ canEdit }: { canEdit: boolean }) => (
    <div data-testid="infos-editor" data-can-edit={String(canEdit)} />
  ),
}))

const COMPLETED_AT = '2026-05-18T12:34:56Z'

const objective = (overrides: Partial<ObjectiveDetail> = {}): ObjectiveDetail => ({
  objectiveId: 'o',
  name: 'Objective',
  score: 10,
  category: null,
  isCompleted: false,
  completedAt: null,
  isFailed: false,
  failedAt: null,
  status: 'Pending',
  trial: null,
  ...overrides,
})

const makeGame = (overrides: Partial<GameBreakdown> = {}): GameBreakdown => ({
  eventGameId: 'er',
  gameName: 'Elden Ring',
  score: 25,
  completedCount: 1,
  totalObjectives: 3,
  objectives: [
    objective({
      objectiveId: 'margit',
      name: 'Margit',
      score: 25,
      category: 'Limgrave',
      isCompleted: true,
      completedAt: COMPLETED_AT,
      status: 'Completed',
    }),
    objective({
      objectiveId: 'rennala',
      name: 'Rennala',
      score: 40,
      category: 'Liurnia',
      isFailed: true,
      failedAt: COMPLETED_AT,
      status: 'Failed',
    }),
    objective({ objectiveId: 'godrick', name: 'Godrick', score: 30, category: 'Limgrave' }),
  ],
  infos: [],
  hasDeathClip: false,
  failedCount: 1,
  isEnabled: true,
  isTrialActive: false,
  hasTrialRun: false,
  trial: null,
  rank: 1,
  ...overrides,
})

const entry: ScoreboardEntry = {
  userId: 'u1',
  displayName: 'Player One',
  twitchLogin: 'player1',
  isLive: false,
  totalScore: 25,
  completedCount: 1,
  isFinished: false,
  lastCompletedAt: null,
  totalInGameTimeMs: null,
  rank: 1,
  games: [],
  failedCount: 1,
  status: 'Pending',
}

const renderDialog = ({
  game = makeGame(),
  canEdit = false,
  showTrial = false,
  onClose = vi.fn(),
}: {
  game?: GameBreakdown
  canEdit?: boolean
  showTrial?: boolean
  onClose?: () => void
} = {}) => {
  render(
    <CompetitorGameDialog
      open
      onClose={onClose}
      entry={entry}
      game={game}
      eventId="event-1"
      canEdit={canEdit}
      showTrial={showTrial}
    />,
  )
  return { onClose, dialog: screen.getByRole('dialog', { name: 'Player One · Elden Ring' }) }
}

const rowOf = (name: string) => screen.getByText(name).closest('tr') as HTMLElement

describe('CompetitorGameDialog', () => {
  it('is titled with the player and the game', () => {
    const { dialog } = renderDialog()
    expect(dialog).toBeInTheDocument()
  })

  it('lists objectives grouped by category, in first-seen order', () => {
    const { dialog } = renderDialog()

    const text = within(dialog)
      .getAllByRole('row')
      .map((row) => row.textContent ?? '')
    const index = (needle: string) => text.findIndex((t) => t.includes(needle))
    expect(index('Limgrave')).toBeLessThan(index('Margit'))
    expect(index('Margit')).toBeLessThan(index('Godrick'))
    expect(index('Godrick')).toBeLessThan(index('Liurnia'))
    expect(index('Liurnia')).toBeLessThan(index('Rennala'))
  })

  it('marks each objective completed, failed or pending, with points and time', () => {
    renderDialog()

    expect(within(rowOf('Margit')).getByTestId('CheckCircleIcon')).toBeInTheDocument()
    expect(within(rowOf('Rennala')).getByTestId('CancelIcon')).toBeInTheDocument()
    expect(within(rowOf('Godrick')).getByTestId('RadioButtonUncheckedIcon')).toBeInTheDocument()
    expect(rowOf('Margit')).toHaveTextContent('25 pts')
    expect(rowOf('Margit')).toHaveTextContent(new Date(COMPLETED_AT).toLocaleString())
  })

  it('summarises the game progress', () => {
    const { dialog } = renderDialog()
    expect(within(dialog).getByText('1/3 objectives · 1 failed · 25 pts')).toBeInTheDocument()
  })

  it('hides the infos editor from a viewer who may not edit when there are no infos', () => {
    renderDialog({ canEdit: false })
    expect(screen.queryByTestId('infos-editor')).toBeNull()
  })

  it('shows the infos editor, editable, to a viewer who may edit', () => {
    renderDialog({ canEdit: true })
    expect(screen.getByTestId('infos-editor')).toHaveAttribute('data-can-edit', 'true')
  })

  it('shows existing infos read-only to a viewer who may not edit', () => {
    const info = { id: 'i1', type: 'Link', url: 'https://x.test' } as unknown as CompetitorInfo
    renderDialog({ canEdit: false, game: makeGame({ infos: [info] }) })
    expect(screen.getByTestId('infos-editor')).toHaveAttribute('data-can-edit', 'false')
  })

  it('shows the trial marks, labelled, when asked to', () => {
    const trial: TrialProgress = {
      trialRunId: 't',
      state: 'Running',
      score: 30,
      completedCount: 1,
      failedCount: 0,
      lastCompletedAt: null,
    }
    renderDialog({
      showTrial: true,
      game: makeGame({
        isTrialActive: true,
        trial,
        objectives: [
          objective({
            objectiveId: 'godrick',
            name: 'Godrick',
            trial: {
              isCompleted: true,
              completedAt: COMPLETED_AT,
              isFailed: false,
              failedAt: null,
              status: 'Completed',
            },
          }),
        ],
      }),
    })

    expect(screen.getByText(/Showing this trial run’s marks/)).toBeInTheDocument()
    expect(within(rowOf('Godrick')).getByTestId('CheckCircleIcon')).toBeInTheDocument()
    expect(screen.getByText('Trial 30')).toBeInTheDocument()
  })

  it('closes from the close button', async () => {
    const { onClose } = renderDialog()
    await userEvent.click(screen.getByRole('button', { name: 'Close' }))
    expect(onClose).toHaveBeenCalled()
  })

  it('closes on Escape', async () => {
    const { onClose } = renderDialog()
    await userEvent.keyboard('{Escape}')
    expect(onClose).toHaveBeenCalled()
  })

  it('closes on a backdrop click', async () => {
    const { onClose } = renderDialog()
    const backdrop = document.querySelector('.MuiBackdrop-root')
    expect(backdrop).not.toBeNull()
    await userEvent.click(backdrop as Element)
    expect(onClose).toHaveBeenCalled()
  })
})
