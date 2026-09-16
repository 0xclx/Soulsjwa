import { beforeEach, describe, expect, it, vi } from 'vitest'
import { fireEvent, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { MyEventsPage } from './MyEventsPage'
import { useMyEvents } from '../features/myEvents/hooks/useMyEvents'
import { useMyEventObjectives } from '../features/myEvents/hooks/useMyEventObjectives'
import { useToggleMyEventObjective } from '../features/myEvents/hooks/useToggleMyEventObjective'
import { useToggleMyEventObjectiveFailure } from '../features/myEvents/hooks/useToggleMyEventObjectiveFailure'
import { useMyTrialRuns } from '../features/myEvents/hooks/useMyTrialRuns'
import { useMyTrialRunObjectives } from '../features/myEvents/hooks/useMyTrialRunObjectives'
import { useToggleTrialObjective } from '../features/myEvents/hooks/useToggleTrialObjective'
import { useToggleTrialObjectiveFailure } from '../features/myEvents/hooks/useToggleTrialObjectiveFailure'
import { useFailRemainingMyEventObjectives } from '../features/myEvents/hooks/useFailRemainingMyEventObjectives'
import { useFailRemainingTrialObjectives } from '../features/myEvents/hooks/useFailRemainingTrialObjectives'
import { FAIL_REMAINING_LABEL } from '../features/events/components/FailRemainingObjectivesButton'
import type { MyEventGame, MyTrialRun } from '../types'

vi.mock('../features/myEvents/hooks/useMyEvents')
vi.mock('../features/myEvents/hooks/useMyEventObjectives')
vi.mock('../features/myEvents/hooks/useToggleMyEventObjective')
vi.mock('../features/myEvents/hooks/useToggleMyEventObjectiveFailure')
vi.mock('../features/myEvents/hooks/useMyTrialRuns')
vi.mock('../features/myEvents/hooks/useMyTrialRunObjectives')
vi.mock('../features/myEvents/hooks/useToggleTrialObjective')
vi.mock('../features/myEvents/hooks/useToggleTrialObjectiveFailure')
vi.mock('../features/myEvents/hooks/useFailRemainingMyEventObjectives')
vi.mock('../features/myEvents/hooks/useFailRemainingTrialObjectives')
vi.mock('../features/events/components/TrialRunControl', () => ({
  TrialRunControl: () => <div>trial controls</div>,
}))

const mutate = vi.fn()
const mutateFailure = vi.fn()
const mutateAsyncFailRemaining = vi.fn(() => Promise.resolve())

const competitorEvent = {
  eventId: 'event-1',
  eventName: 'Lordran Race',
  urlAlias: 'lordran-race',
  status: 'live' as const,
  score: 1250,
  rank: 3,
  totalCompetitors: 12,
  incompleteObjectives: 5,
  totalObjectives: 20,
  lastActivity: '2026-07-21T12:00:00Z',
  lastActivityType: 'objective_completed' as const,
  failedObjectives: 0,
  completionStatus: 'Pending' as const,
}

const trialRun: MyTrialRun = {
  trialRunId: 'trial-1',
  eventId: 'event-1',
  eventName: 'Lordran Race',
  urlAlias: 'lordran-race',
  eventGameId: 'game-9',
  gameName: 'Sekiro',
  isGameEnabled: false,
  competitorId: 'competitor-1',
  competitorName: 'Chosen Undead',
  isOwnTrial: true,
  state: 'Running',
  startedAt: '2026-07-21T10:00:00Z',
  score: 45,
  completedCount: 2,
  failedCount: 0,
  totalObjectives: 6,
  lastCompletedAt: '2026-07-21T12:00:00Z',
}

const game = (overrides: Partial<MyEventGame> = {}): MyEventGame => ({
  gameId: 'game-1',
  gameName: 'Dark Souls',
  isTrialActive: false,
  hasTrialRun: false,
  objectives: [
    {
      objectiveId: 'objective-1',
      name: 'Ring both Bells of Awakening',
      completed: false,
      completedAt: null,
      score: 100,
      failed: false,
      failedAt: null,
    },
  ],
  ...overrides,
})

const renderPage = (path = '/my-events') =>
  render(
    <MemoryRouter initialEntries={[path]}>
      <MyEventsPage />
    </MemoryRouter>,
  )

const mockEvents = (overrides: Record<string, unknown> = {}) =>
  vi.mocked(useMyEvents).mockReturnValue({
    data: {
      competitor: [competitorEvent],
      delegated: [],
      owned: [],
      quickCompleteEnabled: false,
      ...overrides,
    },
    isLoading: false,
    isError: false,
  } as unknown as ReturnType<typeof useMyEvents>)

describe('<MyEventsPage />', () => {
  beforeEach(() => {
    mutate.mockReset()
    mutateFailure.mockReset()
    mutateAsyncFailRemaining.mockReset()
    vi.mocked(useFailRemainingMyEventObjectives).mockReturnValue({
      mutateAsync: mutateAsyncFailRemaining,
      isPending: false,
      isError: false,
    } as unknown as ReturnType<typeof useFailRemainingMyEventObjectives>)
    vi.mocked(useFailRemainingTrialObjectives).mockReturnValue({
      mutateAsync: mutateAsyncFailRemaining,
      isPending: false,
      isError: false,
    } as unknown as ReturnType<typeof useFailRemainingTrialObjectives>)
    vi.mocked(useMyEventObjectives).mockReturnValue({
      data: { competitorId: 'competitor-1', competitorName: 'Chosen Undead', games: [game()] },
      isLoading: false,
      isError: false,
    } as ReturnType<typeof useMyEventObjectives>)
    vi.mocked(useToggleMyEventObjective).mockReturnValue({
      mutate,
      isPending: false,
      isError: false,
    } as unknown as ReturnType<typeof useToggleMyEventObjective>)
    vi.mocked(useToggleMyEventObjectiveFailure).mockReturnValue({
      mutate: mutateFailure,
      isPending: false,
      isError: false,
    } as unknown as ReturnType<typeof useToggleMyEventObjectiveFailure>)
    vi.mocked(useMyTrialRuns).mockReturnValue({
      data: [trialRun],
      isLoading: false,
      isError: false,
    } as unknown as ReturnType<typeof useMyTrialRuns>)
    vi.mocked(useMyTrialRunObjectives).mockReturnValue({
      data: {
        competitorId: 'competitor-1',
        competitorName: 'Chosen Undead',
        games: [game({ gameId: 'game-9', gameName: 'Sekiro' })],
      },
      isLoading: false,
      isError: false,
    } as ReturnType<typeof useMyTrialRunObjectives>)
    vi.mocked(useToggleTrialObjective).mockReturnValue({
      mutate,
      isPending: false,
      isError: false,
    } as unknown as ReturnType<typeof useToggleTrialObjective>)
    vi.mocked(useToggleTrialObjectiveFailure).mockReturnValue({
      mutate: mutateFailure,
      isPending: false,
      isError: false,
    } as unknown as ReturnType<typeof useToggleTrialObjectiveFailure>)
  })

  it('renders a tab per role with counts, and links each to its own path', () => {
    mockEvents({
      owned: [
        {
          eventId: 'event-2',
          eventName: 'Owner Event',
          urlAlias: null,
          status: 'upcoming',
          competitorCount: 2,
          lastActivity: null,
          lastActivityType: null,
        },
      ],
    })

    renderPage()

    expect(screen.getByRole('tab', { name: /Competing \(1\)/ })).toHaveAttribute(
      'href',
      '/my-events',
    )
    expect(screen.getByRole('tab', { name: /Delegated/ })).toHaveAttribute(
      'href',
      '/my-events/delegated',
    )
    expect(screen.getByRole('tab', { name: /Owned \(1\)/ })).toHaveAttribute(
      'href',
      '/my-events/owned',
    )
    expect(screen.getByRole('tab', { name: /Trial runs/ })).toHaveAttribute(
      'href',
      '/my-events/trial',
    )
  })

  it('selects the tab from the URL rather than resetting to the first', () => {
    mockEvents()

    renderPage('/my-events/trial')

    expect(screen.getByRole('tab', { name: /Trial runs/ })).toHaveAttribute('aria-current', 'page')
    // The trial run's own figures, which appear on no other tab.
    expect(screen.getByText('45 pts')).toBeInTheDocument()
  })

  it('marks the trial objective list so a tick is never ambiguous', () => {
    mockEvents()

    renderPage('/my-events/trial')

    // The panel's alert and amber score scroll away on a long list, so the
    // cue has to sit on the list itself and on every control.
    expect(screen.getByText('Trial run')).toBeInTheDocument()
    expect(
      screen.getByRole('checkbox', { name: 'Complete Ring both Bells of Awakening in trial run' }),
    ).toBeInTheDocument()
  })

  it('leaves the official objective list unmarked', () => {
    mockEvents({ quickCompleteEnabled: true })

    renderPage()

    expect(screen.queryByText('Trial run')).not.toBeInTheDocument()
    // No "in trial run" suffix — this control writes to the real record.
    expect(
      screen.getByRole('checkbox', { name: 'Complete Ring both Bells of Awakening' }),
    ).toBeInTheDocument()
  })

  it('shows the selected event objectives without an expand step', () => {
    mockEvents()

    renderPage()

    expect(useMyEventObjectives).toHaveBeenLastCalledWith('event-1', undefined, true, false)
    expect(
      screen.getByRole('checkbox', { name: 'Complete Ring both Bells of Awakening' }),
    ).toBeDisabled()
    expect(screen.getByRole('link', { name: 'Go to event' })).toHaveAttribute(
      'href',
      '/events/lordran-race/games',
    )
  })

  const withObjectivesFor = (...games: ReturnType<typeof game>[]) => {
    mockEvents({ quickCompleteEnabled: true })
    vi.mocked(useMyEventObjectives).mockReturnValue({
      data: {
        competitorId: 'competitor-1',
        competitorName: 'Chosen Undead',
        games,
      },
      isLoading: false,
      isError: false,
    } as ReturnType<typeof useMyEventObjectives>)
  }

  it('makes a game read-only in the regular tab while its trial is recording', () => {
    withObjectivesFor(game({ hasTrialRun: true, isTrialActive: true }))

    renderPage()

    expect(
      screen.getByRole('checkbox', { name: 'Complete Ring both Bells of Awakening' }),
    ).toBeDisabled()
    expect(screen.getByText(/use the Trial runs tab/i)).toBeInTheDocument()
  })

  it('makes a game read-only when trial mode is on but the run is dormant', () => {
    // The rule the server enforces is "a slot exists", not "a run records":
    // a dormant slot refuses the official write, so leaving the checkbox live
    // here would only produce a 409 the user cannot act on.
    withObjectivesFor(game({ hasTrialRun: true, isTrialActive: false }))

    renderPage()

    expect(
      screen.getByRole('checkbox', { name: 'Complete Ring both Bells of Awakening' }),
    ).toBeDisabled()
    expect(screen.getByText(/not recording/i)).toBeInTheDocument()
    expect(screen.queryByText(/use the Trial runs tab/i)).not.toBeInTheDocument()
  })

  it('leaves a game editable in the regular tab when no trial is recording', () => {
    mockEvents({ quickCompleteEnabled: true })

    renderPage()

    expect(
      screen.getByRole('checkbox', { name: 'Complete Ring both Bells of Awakening' }),
    ).toBeEnabled()
  })

  it('offers to fail the rest of a game on the regular tab, only after confirming', async () => {
    mockEvents({ quickCompleteEnabled: true })

    renderPage()

    fireEvent.click(screen.getByRole('button', { name: FAIL_REMAINING_LABEL }))
    expect(mutateAsyncFailRemaining).not.toHaveBeenCalled()
    expect(
      await screen.findByRole('dialog', { name: 'Fail all remaining objectives in Dark Souls?' }),
    ).toHaveAccessibleDescription(/1 objective still pending for you/)

    fireEvent.click(screen.getByRole('button', { name: 'Fail 1 objective' }))

    expect(mutateAsyncFailRemaining).toHaveBeenCalledWith({ eventGameId: 'game-1' })
  })

  it('withholds the bulk fail while a trial blocks the regular tab', () => {
    mockEvents({ quickCompleteEnabled: true })
    vi.mocked(useMyEventObjectives).mockReturnValue({
      data: {
        competitorId: 'competitor-1',
        competitorName: 'Chosen Undead',
        games: [game({ hasTrialRun: true, isTrialActive: true })],
      },
      isLoading: false,
      isError: false,
    } as ReturnType<typeof useMyEventObjectives>)

    renderPage()

    expect(screen.queryByRole('button', { name: FAIL_REMAINING_LABEL })).not.toBeInTheDocument()
  })

  it('offers the bulk fail on the trial tab against the run', async () => {
    mockEvents()

    renderPage('/my-events/trial')

    fireEvent.click(screen.getByRole('button', { name: FAIL_REMAINING_LABEL }))
    expect(await screen.findByRole('dialog')).toHaveAccessibleDescription(/on the trial run/)

    fireEvent.click(screen.getByRole('button', { name: 'Fail 1 objective' }))

    expect(mutateAsyncFailRemaining).toHaveBeenCalledOnce()
  })

  it('shows the no-events action', () => {
    vi.mocked(useMyEvents).mockReturnValue({
      data: { competitor: [], delegated: [], owned: [], quickCompleteEnabled: false },
      isLoading: false,
      isError: false,
    } as unknown as ReturnType<typeof useMyEvents>)

    renderPage()

    expect(screen.getByText("You're not part of any events yet.")).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Browse events' })).toHaveAttribute('href', '/events')
    expect(screen.queryByRole('tab')).not.toBeInTheDocument()
  })
})
