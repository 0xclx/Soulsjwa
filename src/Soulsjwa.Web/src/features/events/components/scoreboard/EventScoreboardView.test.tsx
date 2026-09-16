import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { describe, expect, it, vi, beforeEach } from 'vitest'
import { EventScoreboardView } from './EventScoreboardView'
import type { EventResponse, ScoreboardEntry, ScoreboardResponse, User } from '../../../../types'

const mocks = vi.hoisted(() => ({
  data: undefined as ScoreboardResponse | undefined,
  isLoading: false,
  isError: false,
}))

vi.mock('../../hooks/useScoreboard', () => ({
  useScoreboard: () => ({ data: mocks.data, isLoading: mocks.isLoading, isError: mocks.isError }),
}))

const event: EventResponse = {
  id: 'event-1',
  name: 'Test event',
  description: '',
  isStarted: true,
  isArchived: false,
  isFeatured: false,
  allowTrialRuns: true,
  urlAlias: null,
  createdById: 'creator-1',
  games: [],
  competitors: [],
} as unknown as EventResponse

const scoreboardEntry: ScoreboardEntry = {
  userId: 'user-1',
  displayName: 'Competitor One',
  twitchLogin: 'competitor1',
  profileImageUrl: '',
  isLive: false,
  totalScore: 10,
  completedCount: 2,
  isFinished: false,
  lastCompletedAt: null,
  totalInGameTimeMs: null,
  rank: 1,
  games: [],
  failedCount: 0,
  status: 'Pending',
}

const scoreboardWithEntries: ScoreboardResponse = {
  entries: [scoreboardEntry],
  tieBreakMode: 'ByTime',
}

const renderView = (currentUser: User | undefined) =>
  render(
    <MemoryRouter>
      <EventScoreboardView eventId="event-1" event={event} currentUser={currentUser} />
    </MemoryRouter>,
  )

/** Stub `window.matchMedia` so MUI's `useMediaQuery(theme.breakpoints.up('md'))` resolves to `matches`. */
const stubViewportWidth = (matches: boolean) => {
  window.matchMedia = (query: string) =>
    ({
      matches,
      media: query,
      onchange: null,
      addListener: () => {},
      removeListener: () => {},
      addEventListener: () => {},
      removeEventListener: () => {},
      dispatchEvent: () => false,
    }) as MediaQueryList
}

describe('EventScoreboardView', () => {
  beforeEach(() => {
    mocks.data = scoreboardWithEntries
    mocks.isLoading = false
    mocks.isError = false
    stubViewportWidth(false)
  })

  it('renders an anonymous visitor the same entries as an authenticated one', () => {
    const { unmount } = renderView(undefined)
    expect(screen.getAllByText('Competitor One').length).toBeGreaterThan(0)
    unmount()

    renderView({ id: 'user-2', role: 'User' } as User)
    expect(screen.getAllByText('Competitor One').length).toBeGreaterThan(0)
  })

  it('shows an empty state when there are no entries', () => {
    mocks.data = { entries: [], tieBreakMode: 'ByTime' }
    renderView(undefined)
    expect(screen.getByText(/no scores yet/i)).toBeInTheDocument()
  })

  it('shows an error state when the fetch fails', () => {
    mocks.isError = true
    mocks.data = undefined
    renderView(undefined)
    expect(screen.getByText(/failed to load scoreboard/i)).toBeInTheDocument()
  })

  it('renders only the table at a wide viewport, never the cards', () => {
    stubViewportWidth(true)
    const { container } = renderView(undefined)
    expect(container.querySelector('table')).not.toBeNull()
    expect(screen.getAllByText('Competitor One')).toHaveLength(1)
  })

  it('renders only the cards at a narrow viewport, never the table', () => {
    stubViewportWidth(false)
    const { container } = renderView(undefined)
    expect(container.querySelector('table')).toBeNull()
    expect(screen.getAllByText('Competitor One')).toHaveLength(1)
  })
})
