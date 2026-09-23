import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter } from 'react-router-dom'
import { describe, expect, it, vi, beforeEach } from 'vitest'
import { EventScoreboardView } from './EventScoreboardView'
import { eventsApi } from '../../api/eventsApi'
import { stubViewportWidth } from '../../../../test/viewport'
import type {
  EventGame,
  EventResponse,
  GameBreakdown,
  ScoreboardEntry,
  ScoreboardResponse,
  User,
} from '../../../../types'

vi.mock('../../api/eventsApi', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../api/eventsApi')>()
  return {
    ...actual,
    eventsApi: { ...actual.eventsApi, get: vi.fn(), getScoreboard: vi.fn() },
  }
})

const makeEvent = (overrides: Partial<EventResponse> = {}): EventResponse => ({
  id: 'event-1',
  name: 'Test event',
  urlAlias: null,
  description: '',
  createdById: 'creator-1',
  isArchived: false,
  isStarted: true,
  isFeatured: false,
  allowTrialRuns: true,
  tieBreakMode: 'ByTime',
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
  games: [],
  competitors: [],
  ...overrides,
})

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

const renderView = (
  currentUser: User | undefined,
  event: EventResponse = makeEvent(),
  url = '/scoreboard',
) => {
  vi.mocked(eventsApi.get).mockResolvedValue(event)
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <MemoryRouter initialEntries={[url]}>
        <EventScoreboardView eventId={event.id} event={event} currentUser={currentUser} />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('EventScoreboardView', () => {
  beforeEach(() => {
    vi.mocked(eventsApi.get).mockReset()
    vi.mocked(eventsApi.getScoreboard).mockReset()
    vi.mocked(eventsApi.getScoreboard).mockResolvedValue(scoreboardWithEntries)
    stubViewportWidth(false)
  })

  it('renders an anonymous visitor the same entries as an authenticated one', async () => {
    const { unmount } = renderView(undefined)
    expect((await screen.findAllByText('Competitor One')).length).toBeGreaterThan(0)
    unmount()

    renderView({ id: 'user-2', role: 'User' } as User)
    expect((await screen.findAllByText('Competitor One')).length).toBeGreaterThan(0)
  })

  it('shows the pre-start overview, and makes no scoreboard request, before the event starts', async () => {
    const { container } = renderView(undefined, makeEvent({ isStarted: false }))

    expect(screen.getByRole('region', { name: 'Games' })).toBeInTheDocument()
    expect(screen.getByRole('region', { name: 'Competitors' })).toBeInTheDocument()
    expect(container.querySelector('table')).toBeNull()
    // Let the live-event fetch settle so a stray scoreboard request would show.
    await vi.waitFor(() => expect(eventsApi.get).toHaveBeenCalled())
    expect(eventsApi.getScoreboard).not.toHaveBeenCalled()
  })

  it('says there are no competitors yet for a started event without entries', async () => {
    vi.mocked(eventsApi.getScoreboard).mockResolvedValue({ entries: [], tieBreakMode: 'ByTime' })
    renderView(undefined)
    expect(await screen.findByText('No competitors yet')).toBeInTheDocument()
    expect(screen.queryByText(/no scores yet/i)).toBeNull()
  })

  it('shows an error state when the fetch fails', async () => {
    vi.mocked(eventsApi.getScoreboard).mockRejectedValue(new Error('boom'))
    renderView(undefined)
    expect(await screen.findByText(/failed to load scoreboard/i)).toBeInTheDocument()
  })

  it('renders only the table at a wide viewport, never the cards', async () => {
    stubViewportWidth(true)
    const { container } = renderView(undefined)
    expect(await screen.findAllByText('Competitor One')).toHaveLength(1)
    expect(container.querySelector('table')).not.toBeNull()
  })

  it('renders only the cards at a narrow viewport, never the table', async () => {
    stubViewportWidth(false)
    const { container } = renderView(undefined)
    expect(await screen.findAllByText('Competitor One')).toHaveLength(1)
    expect(container.querySelector('table')).toBeNull()
  })

  describe('view switch', () => {
    const eventGame = (eventGameId: string, gameName: string, isEnabled: boolean): EventGame => ({
      eventGameId,
      knownGameId: null,
      gameName,
      knownGameName: null,
      connectorSupported: false,
      isCustomGame: true,
      isEnabled,
      objectives: Array.from({ length: 12 }, (_, i) => ({
        id: `${eventGameId}-${i}`,
        name: `Objective ${i}`,
        score: 10,
        isPredefined: false,
      })),
    })

    const breakdown = (overrides: Partial<GameBreakdown>): GameBreakdown => ({
      eventGameId: 'er',
      gameName: 'Elden Ring',
      score: 0,
      completedCount: 0,
      totalObjectives: 12,
      objectives: [],
      infos: [],
      hasDeathClip: false,
      failedCount: 0,
      isEnabled: true,
      isTrialActive: false,
      hasTrialRun: false,
      trial: null,
      rank: 1,
      ...overrides,
    })

    const withGame = (enabled: boolean) =>
      makeEvent({
        games: [eventGame('ds', 'Dark Souls', false), eventGame('er', 'Elden Ring', enabled)],
      })

    beforeEach(() => {
      stubViewportWidth(true)
      vi.mocked(eventsApi.getScoreboard).mockResolvedValue({
        tieBreakMode: 'ByTime',
        entries: [
          {
            ...scoreboardEntry,
            games: [breakdown({ completedCount: 10, failedCount: 2 })],
          },
          {
            ...scoreboardEntry,
            userId: 'user-2',
            displayName: 'Competitor Two',
            rank: 2,
            games: [breakdown({ completedCount: 3, rank: 2 })],
          },
        ],
      })
    })

    const switchGroup = () => screen.getByRole('group', { name: 'Scoreboard view' })

    it('defaults to the current game view when a game is enabled', async () => {
      renderView(undefined, withGame(true))

      expect(
        await screen.findByRole('table', { name: 'Current game standings' }),
      ).toBeInTheDocument()
      expect(within(switchGroup()).getByRole('button', { name: 'Current game' })).toHaveAttribute(
        'aria-pressed',
        'true',
      )
    })

    it('heads the current game view with the game, its objective count and who is done', async () => {
      renderView(undefined, withGame(true))

      expect(await screen.findByText('Now playing: Elden Ring')).toBeInTheDocument()
      expect(screen.getByText('12 objectives')).toBeInTheDocument()
      expect(screen.getByText('1 of 2 done')).toBeInTheDocument()
    })

    it('opens the whole event view from ?view=event', async () => {
      renderView(undefined, withGame(true), '/scoreboard?view=event')

      expect(
        await screen.findByRole('table', { name: 'Whole event standings' }),
      ).toBeInTheDocument()
      expect(screen.queryByRole('table', { name: 'Current game standings' })).toBeNull()
      expect(within(switchGroup()).getByRole('button', { name: 'Whole event' })).toHaveAttribute(
        'aria-pressed',
        'true',
      )
    })

    it('switches views from the toggle', async () => {
      renderView(undefined, withGame(true))
      await screen.findByRole('table', { name: 'Current game standings' })

      await userEvent.click(within(switchGroup()).getByRole('button', { name: 'Whole event' }))

      expect(screen.getByRole('table', { name: 'Whole event standings' })).toBeInTheDocument()
      expect(screen.queryByRole('table', { name: 'Current game standings' })).toBeNull()
    })

    it('opens the objectives dialog from a standings row and returns focus on Escape', async () => {
      renderView(undefined, withGame(true))
      const trigger = await screen.findByRole('button', {
        name: 'Competitor Two: Elden Ring objectives',
      })

      trigger.focus()
      await userEvent.keyboard('{Enter}')
      expect(
        await screen.findByRole('dialog', { name: 'Competitor Two · Elden Ring' }),
      ).toBeInTheDocument()

      await userEvent.keyboard('{Escape}')
      await vi.waitFor(() => expect(screen.queryByRole('dialog')).toBeNull())
      expect(trigger).toHaveFocus()
    })

    it('opens the objectives dialog for the competitor and game of a whole event cell', async () => {
      renderView(undefined, withGame(true), '/scoreboard?view=event')

      await userEvent.click(
        await screen.findByRole('button', { name: 'Competitor One: Elden Ring objectives' }),
      )

      expect(
        await screen.findByRole('dialog', { name: 'Competitor One · Elden Ring' }),
      ).toBeInTheDocument()
    })

    it('shows current game cards on a narrow screen, and a tap opens the dialog', async () => {
      stubViewportWidth(false)
      renderView(undefined, withGame(true))

      const list = await screen.findByRole('list', { name: 'Current game standings' })
      expect(screen.queryByRole('table')).toBeNull()

      await userEvent.click(within(list).getByText('Competitor Two'))

      expect(
        await screen.findByRole('dialog', { name: 'Competitor Two · Elden Ring' }),
      ).toBeInTheDocument()
    })

    // Ported from the removed desktop row tests: which marks the dialog
    // draws is decided by showsTrial with the enabled game as the scope.
    describe('trial marks in the dialog', () => {
      const OFFICIAL_AT = '2026-01-01T09:00:00Z'
      const objective = {
        objectiveId: 'obj',
        name: 'Practised only',
        score: 10,
        category: null,
        isCompleted: true,
        completedAt: OFFICIAL_AT,
        isFailed: false,
        failedAt: null,
        status: 'Completed' as const,
        trial: {
          isCompleted: false,
          completedAt: null,
          isFailed: false,
          failedAt: null,
          status: 'Pending' as const,
        },
      }
      const pausedTrial = {
        trialRunId: 't',
        state: 'Paused' as const,
        score: 12,
        completedCount: 0,
        failedCount: 0,
        lastCompletedAt: null,
      }

      it('shows a paused trial on a game that is not the enabled one as that run', async () => {
        vi.mocked(eventsApi.getScoreboard).mockResolvedValue({
          tieBreakMode: 'ByTime',
          entries: [
            {
              ...scoreboardEntry,
              games: [
                breakdown({ eventGameId: 'er' }),
                breakdown({
                  eventGameId: 'ds',
                  gameName: 'Dark Souls',
                  isEnabled: false,
                  trial: pausedTrial,
                  objectives: [objective],
                }),
              ],
            },
          ],
        })
        renderView(undefined, withGame(true), '/scoreboard?view=event')

        await userEvent.click(
          await screen.findByRole('button', { name: 'Competitor One: Dark Souls objectives' }),
        )

        const dialog = await screen.findByRole('dialog', { name: 'Competitor One · Dark Souls' })
        expect(within(dialog).getByText(/Showing this trial run/)).toBeInTheDocument()
        expect(
          within(dialog).queryByText(new Date(OFFICIAL_AT).toLocaleString()),
        ).not.toBeInTheDocument()
      })

      it('hands a paused trial on the enabled game back to the official record', async () => {
        vi.mocked(eventsApi.getScoreboard).mockResolvedValue({
          tieBreakMode: 'ByTime',
          entries: [
            {
              ...scoreboardEntry,
              games: [breakdown({ trial: pausedTrial, objectives: [objective] })],
            },
          ],
        })
        renderView(undefined, withGame(true))

        await userEvent.click(
          await screen.findByRole('button', { name: 'Competitor One: Elden Ring objectives' }),
        )

        const dialog = await screen.findByRole('dialog', { name: 'Competitor One · Elden Ring' })
        expect(within(dialog).getByText('Trial 12')).toBeInTheDocument()
        expect(within(dialog).queryByText(/Showing this trial run/)).toBeNull()
        expect(within(dialog).getByText(new Date(OFFICIAL_AT).toLocaleString())).toBeInTheDocument()
      })
    })

    it('shows no switch and the whole event view when no game is enabled, even with ?view=game', async () => {
      renderView(undefined, withGame(false), '/scoreboard?view=game')

      expect(
        await screen.findByRole('table', { name: 'Whole event standings' }),
      ).toBeInTheDocument()
      expect(screen.queryByRole('group', { name: 'Scoreboard view' })).toBeNull()
      expect(screen.queryByText(/Now playing/)).toBeNull()
    })
  })

  describe('competitor search', () => {
    const eventGame: EventGame = {
      eventGameId: 'er',
      knownGameId: null,
      gameName: 'Elden Ring',
      knownGameName: null,
      connectorSupported: false,
      isCustomGame: true,
      isEnabled: true,
      objectives: [],
    }
    const game = (rank: number, completedCount: number): GameBreakdown => ({
      eventGameId: 'er',
      gameName: 'Elden Ring',
      score: completedCount * 10,
      completedCount,
      totalObjectives: 5,
      objectives: [],
      infos: [],
      hasDeathClip: false,
      failedCount: 0,
      isEnabled: true,
      isTrialActive: false,
      hasTrialRun: false,
      trial: null,
      rank,
    })
    const competitor = (
      userId: string,
      displayName: string,
      twitchLogin: string,
      rank: number,
    ): ScoreboardEntry => ({
      ...scoreboardEntry,
      userId,
      displayName,
      twitchLogin,
      rank,
      games: [game(rank, 5 - rank)],
    })

    beforeEach(() => {
      stubViewportWidth(true)
      vi.mocked(eventsApi.getScoreboard).mockResolvedValue({
        tieBreakMode: 'ByTime',
        entries: [
          competitor('a', 'Solaire', 'sun_bro', 1),
          competitor('b', 'Siegmeyer', 'onion_knight', 2),
          competitor('c', 'Patches', 'trusty_patches', 3),
        ],
      })
    })

    const search = () => screen.getByRole('searchbox', { name: 'Search competitors' })
    const standings = () => screen.getByRole('table', { name: 'Current game standings' })
    const rankOf = (name: string) =>
      within(standings())
        .getAllByRole('row')
        .find((row) => within(row).queryByText(name))
        ?.querySelector('td')?.textContent

    it('filters by display name and keeps the server rank on the rows left', async () => {
      renderView(undefined, makeEvent({ games: [eventGame] }))
      await screen.findByRole('table', { name: 'Current game standings' })

      await userEvent.type(search(), 'SIEG')

      expect(within(standings()).queryByText('Solaire')).toBeNull()
      expect(within(standings()).queryByText('Patches')).toBeNull()
      expect(rankOf('Siegmeyer')).toBe('2')
    })

    it('matches the Twitch login', async () => {
      renderView(undefined, makeEvent({ games: [eventGame] }))
      await screen.findByRole('table', { name: 'Current game standings' })

      await userEvent.type(search(), 'trusty')

      expect(within(standings()).getByText('Patches')).toBeInTheDocument()
      expect(within(standings()).queryByText('Solaire')).toBeNull()
    })

    it('announces the number of matches politely', async () => {
      renderView(undefined, makeEvent({ games: [eventGame] }))
      await screen.findByRole('table', { name: 'Current game standings' })

      await userEvent.type(search(), 's')

      const status = screen.getByRole('status')
      expect(status).toHaveAttribute('aria-live', 'polite')
      // Solaire, Siegmeyer and trusty_patches all contain an "s".
      expect(status).toHaveTextContent('3 of 3 competitors match')

      await userEvent.type(search(), 'ol')
      expect(status).toHaveTextContent('1 of 3 competitors match')
    })

    it('says when nothing matches, and the clear button brings every row back', async () => {
      renderView(undefined, makeEvent({ games: [eventGame] }))
      await screen.findByRole('table', { name: 'Current game standings' })

      await userEvent.type(search(), 'gwyn')

      expect(screen.getByText("No competitor matches 'gwyn'")).toBeInTheDocument()
      expect(screen.queryByRole('table', { name: 'Current game standings' })).toBeNull()

      await userEvent.click(screen.getByRole('button', { name: 'Clear search' }))

      expect(search()).toHaveValue('')
      expect(within(standings()).getAllByRole('row')).toHaveLength(4)
    })

    it('keeps the search when switching views, and filters the whole event view too', async () => {
      renderView(undefined, makeEvent({ games: [eventGame] }))
      await screen.findByRole('table', { name: 'Current game standings' })
      await userEvent.type(search(), 'patch')

      await userEvent.click(screen.getByRole('button', { name: 'Whole event' }))

      expect(search()).toHaveValue('patch')
      const matrix = screen.getByRole('table', { name: 'Whole event standings' })
      expect(within(matrix).getByText('Patches')).toBeInTheDocument()
      expect(within(matrix).queryByText('Solaire')).toBeNull()
      // Hiding the leaders renumbers nothing: Patches is still #3 in the event.
      expect(within(matrix).getAllByRole('row')[1]?.lastElementChild?.textContent).toBe('3')
    })
  })
})
