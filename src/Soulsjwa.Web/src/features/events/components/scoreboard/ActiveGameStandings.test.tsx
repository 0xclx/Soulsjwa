import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ActiveGameStandings } from './ActiveGameStandings'
import { stubViewportWidth } from '../../../../test/viewport'
import type { GameBreakdown, ScoreboardEntry, TrialProgress } from '../../../../types'

const ACTIVE = 'active'

const makeTrial = (overrides: Partial<TrialProgress> = {}): TrialProgress => ({
  trialRunId: 't1',
  state: 'Running',
  score: 0,
  completedCount: 0,
  failedCount: 0,
  lastCompletedAt: null,
  ...overrides,
})

const makeGame = (overrides: Partial<GameBreakdown> = {}): GameBreakdown => ({
  eventGameId: ACTIVE,
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

const makeEntry = (overrides: Partial<ScoreboardEntry> = {}): ScoreboardEntry => ({
  userId: 'u1',
  displayName: 'Player One',
  twitchLogin: 'player1',
  isLive: false,
  totalScore: 10,
  completedCount: 1,
  isFinished: false,
  lastCompletedAt: null,
  totalInGameTimeMs: null,
  rank: 1,
  games: [makeGame()],
  failedCount: 0,
  status: 'Pending',
  ...overrides,
})

const renderStandings = (entries: ScoreboardEntry[], onSelect = vi.fn(), query = '') => {
  const view = render(
    <ActiveGameStandings
      entries={entries}
      eventGameId={ACTIVE}
      query={query}
      onSelect={onSelect}
    />,
  )
  return { ...view, onSelect }
}

const table = () => screen.getByRole('table', { name: 'Current game standings' })
const bodyRows = () => within(table()).getAllByRole('row').slice(1)
const rowFor = (name: string) => {
  const row = bodyRows().find((r) => within(r).queryByText(name))
  if (!row) throw new Error(`no row for ${name}`)
  return row
}

describe('ActiveGameStandings', () => {
  beforeEach(() => stubViewportWidth(true))

  it('orders rows by the server-provided game rank, not the event rank', () => {
    renderStandings([
      makeEntry({
        userId: 'a',
        displayName: 'Event Leader',
        rank: 1,
        games: [makeGame({ rank: 3 })],
      }),
      makeEntry({
        userId: 'b',
        displayName: 'Game Leader',
        rank: 3,
        games: [makeGame({ rank: 1 })],
      }),
      makeEntry({ userId: 'c', displayName: 'Middle', rank: 2, games: [makeGame({ rank: 2 })] }),
    ])

    const rows = bodyRows()
    expect(within(rows[0]!).getByText('Game Leader')).toBeInTheDocument()
    expect(within(rows[1]!).getByText('Middle')).toBeInTheDocument()
    expect(within(rows[2]!).getByText('Event Leader')).toBeInTheDocument()
    expect(rows.map((row) => within(row).getAllByRole('cell')[0]?.textContent)).toEqual([
      '1',
      '2',
      '3',
    ])
  })

  it('shows game progress as a named progress bar', () => {
    renderStandings([makeEntry({ games: [makeGame({ completedCount: 7, totalObjectives: 12 })] })])

    const bar = screen.getByRole('progressbar', { name: '7 of 12 objectives' })
    expect(bar).toHaveAttribute('aria-valuenow', String(Math.round((7 / 12) * 100)))
  })

  it('shows the game score, and the event total with the event rank', () => {
    renderStandings([
      makeEntry({ totalScore: 910, rank: 2, games: [makeGame({ score: 120, rank: 1 })] }),
    ])

    const cells = within(rowFor('Player One')).getAllByRole('cell')
    expect(cells.some((cell) => cell.textContent === '120')).toBe(true)
    expect(cells.some((cell) => cell.textContent === '910 · #2')).toBe(true)
  })

  it('shows the failed count only when there are failures', () => {
    renderStandings([
      makeEntry({ userId: 'a', displayName: 'Clean', games: [makeGame({ failedCount: 0 })] }),
      makeEntry({
        userId: 'b',
        displayName: 'Dented',
        games: [makeGame({ failedCount: 2, rank: 2 })],
      }),
    ])

    expect(within(rowFor('Dented')).getByLabelText('2 failed')).toBeInTheDocument()
    expect(within(rowFor('Clean')).queryByLabelText(/failed/)).toBeNull()
  })

  it('marks a competitor Done when every objective of the game is completed or failed', () => {
    renderStandings([
      makeEntry({
        userId: 'a',
        displayName: 'Finisher',
        games: [makeGame({ completedCount: 10, failedCount: 2, totalObjectives: 12 })],
      }),
      makeEntry({
        userId: 'b',
        displayName: 'Still Going',
        // Finished the event as a whole, but not this game: no chip at all.
        isFinished: true,
        games: [makeGame({ completedCount: 10, failedCount: 1, totalObjectives: 12, rank: 2 })],
      }),
    ])

    expect(within(rowFor('Finisher')).getByText('Done')).toBeInTheDocument()
    expect(within(rowFor('Still Going')).queryByText('Done')).toBeNull()
    expect(screen.queryByText('Finished')).toBeNull()
  })

  it('shows live status, avatar and Twitch link for each competitor', () => {
    renderStandings([
      makeEntry({ isLive: true, twitchLogin: 'p1', profileImageUrl: 'https://example.test/a.png' }),
    ])

    const row = rowFor('Player One')
    expect(within(row).getByRole('img', { name: 'Live' })).toBeInTheDocument()
    expect(within(row).getByRole('img', { name: 'Player One' })).toHaveAttribute(
      'src',
      'https://example.test/a.png',
    )
    expect(within(row).getByRole('link', { name: 'Watch Player One on Twitch' })).toHaveAttribute(
      'href',
      'https://twitch.tv/p1',
    )
  })

  it('skips competitors without a breakdown for the game', () => {
    renderStandings([
      makeEntry({ userId: 'a', displayName: 'Playing' }),
      makeEntry({
        userId: 'b',
        displayName: 'Elsewhere',
        games: [makeGame({ eventGameId: 'other' })],
      }),
    ])

    expect(screen.getByText('Playing')).toBeInTheDocument()
    expect(screen.queryByText('Elsewhere')).toBeNull()
  })

  describe('trials', () => {
    it('shows a Trial badge when and only when the game has a started trial', () => {
      const { unmount } = renderStandings([
        makeEntry({ games: [makeGame({ isTrialActive: true, trial: makeTrial() })] }),
      ])
      expect(screen.getByText('Trial')).toBeInTheDocument()
      unmount()

      renderStandings([makeEntry()])
      expect(screen.queryByText('Trial')).toBeNull()
    })

    it('keeps showing the badge and score for a paused trial', () => {
      renderStandings([
        makeEntry({
          games: [
            makeGame({ isTrialActive: false, trial: makeTrial({ state: 'Paused', score: 7 }) }),
          ],
        }),
      ])

      expect(screen.getByText('Trial')).toBeInTheDocument()
      expect(screen.getByText('Trial 7')).toBeInTheDocument()
    })

    it('shows the current game trial figures beside the official ones without altering them', () => {
      renderStandings([
        makeEntry({
          games: [
            makeGame({
              score: 10,
              completedCount: 1,
              isTrialActive: true,
              trial: makeTrial({ score: 45, completedCount: 3, failedCount: 2 }),
            }),
          ],
        }),
      ])

      // Bound to the screen-reader labels, not bare digits, so swapped
      // completed/failed figures would fail.
      expect(screen.getByRole('progressbar', { name: '1 of 12 objectives' })).toBeInTheDocument()
      expect(screen.getByText('Trial 45')).toBeInTheDocument()
      expect(screen.getByText('Completed in trial:').parentElement).toHaveTextContent('3')
      expect(screen.getByText('Failed in trial:').parentElement).toHaveTextContent('2')
    })

    it('shows only a badge naming the game for a trial on another game', () => {
      renderStandings([
        makeEntry({
          games: [
            makeGame(),
            makeGame({
              eventGameId: 'trialed',
              gameName: 'Sekiro',
              isEnabled: false,
              isTrialActive: true,
              trial: makeTrial({ score: 12, completedCount: 4 }),
            }),
          ],
        }),
      ])

      expect(screen.getByText('Trial · Sekiro')).toBeInTheDocument()
      expect(screen.queryByText('Trial 12')).toBeNull()
      expect(screen.queryByText('Completed in trial:')).toBeNull()
    })
  })

  describe('opening the objectives', () => {
    const openButton = () =>
      screen.getByRole('button', { name: 'Player One: Elden Ring objectives' })

    it('selects the competitor and game when the row is clicked', async () => {
      const { onSelect } = renderStandings([makeEntry()])

      await userEvent.click(rowFor('Player One').querySelectorAll('td')[2] as HTMLElement)

      expect(onSelect).toHaveBeenCalledTimes(1)
      expect(onSelect).toHaveBeenCalledWith('u1', ACTIVE)
    })

    it.each(['{Enter}', ' '])('selects from the keyboard with %j', async (key) => {
      const { onSelect } = renderStandings([makeEntry()])

      openButton().focus()
      await userEvent.keyboard(key)

      expect(onSelect).toHaveBeenCalledTimes(1)
      expect(onSelect).toHaveBeenCalledWith('u1', ACTIVE)
    })

    it('does not select when the Twitch link is used', async () => {
      const { onSelect } = renderStandings([makeEntry()])

      await userEvent.click(screen.getByRole('link', { name: 'Watch Player One on Twitch' }))

      expect(onSelect).not.toHaveBeenCalled()
    })
  })

  it('filters rows by the search query without renumbering them', () => {
    renderStandings(
      [
        makeEntry({ userId: 'a', displayName: 'Solaire', games: [makeGame({ rank: 1 })] }),
        makeEntry({ userId: 'b', displayName: 'Siegmeyer', games: [makeGame({ rank: 2 })] }),
      ],
      vi.fn(),
      'SIEG',
    )

    expect(bodyRows()).toHaveLength(1)
    expect(within(rowFor('Siegmeyer')).getAllByRole('cell')[0]?.textContent).toBe('2')
  })

  describe('narrow screens', () => {
    beforeEach(() => stubViewportWidth(false))

    const cards = () => screen.getAllByRole('listitem')
    const cardFor = (name: string) => {
      const card = cards().find((c) => within(c).queryByText(name))
      if (!card) throw new Error(`no card for ${name}`)
      return card
    }

    it('renders a card per competitor in game-rank order instead of a table', () => {
      renderStandings([
        makeEntry({ userId: 'a', displayName: 'Event Leader', games: [makeGame({ rank: 2 })] }),
        makeEntry({
          userId: 'b',
          displayName: 'Game Leader',
          rank: 2,
          games: [makeGame({ rank: 1 })],
        }),
      ])

      expect(screen.queryByRole('table')).toBeNull()
      expect(screen.getByRole('list', { name: 'Current game standings' })).toBeInTheDocument()
      expect(cards().map((c) => within(c).getByTestId('standings-rank').textContent)).toEqual([
        '1',
        '2',
      ])
      expect(within(cards()[0]!).getByText('Game Leader')).toBeInTheDocument()
    })

    it('shows rank, name, progress, game score and event total on each card', () => {
      renderStandings([
        makeEntry({
          totalScore: 910,
          rank: 2,
          games: [makeGame({ score: 120, completedCount: 7, totalObjectives: 12, rank: 1 })],
        }),
      ])

      const card = cardFor('Player One')
      expect(
        within(card).getByRole('progressbar', { name: '7 of 12 objectives' }),
      ).toBeInTheDocument()
      expect(within(card).getByTestId('standings-score')).toHaveTextContent('120')
      expect(within(card).getByTestId('standings-event')).toHaveTextContent('910 · #2')
    })

    it('opens the objectives by tapping the card or from the keyboard', async () => {
      const { onSelect } = renderStandings([makeEntry()])

      await userEvent.click(within(cardFor('Player One')).getByTestId('standings-score'))
      expect(onSelect).toHaveBeenLastCalledWith('u1', ACTIVE)

      screen.getByRole('button', { name: 'Player One: Elden Ring objectives' }).focus()
      await userEvent.keyboard('{Enter}')
      await userEvent.keyboard(' ')
      expect(onSelect).toHaveBeenCalledTimes(3)
    })

    it('follows the desktop trial rules: amber figures for this game, a named badge for another', () => {
      renderStandings([
        makeEntry({
          userId: 'a',
          displayName: 'Here',
          games: [makeGame({ isTrialActive: true, trial: makeTrial({ score: 45 }) })],
        }),
        makeEntry({
          userId: 'b',
          displayName: 'Elsewhere',
          games: [
            makeGame({ rank: 2 }),
            makeGame({
              eventGameId: 'trialed',
              gameName: 'Sekiro',
              isEnabled: false,
              trial: makeTrial({ score: 12 }),
            }),
          ],
        }),
      ])

      expect(within(cardFor('Here')).getByText('Trial')).toBeInTheDocument()
      expect(within(cardFor('Here')).getByText('Trial 45')).toBeInTheDocument()
      expect(within(cardFor('Elsewhere')).getByText('Trial · Sekiro')).toBeInTheDocument()
      expect(within(cardFor('Elsewhere')).queryByText('Trial 12')).toBeNull()
    })

    it('marks a competitor Done', () => {
      renderStandings([
        makeEntry({
          games: [makeGame({ completedCount: 11, failedCount: 1, totalObjectives: 12 })],
        }),
      ])

      expect(within(cardFor('Player One')).getByText('Done')).toBeInTheDocument()
    })

    it('filters cards by the search query', () => {
      renderStandings(
        [
          makeEntry({ userId: 'a', displayName: 'Solaire' }),
          makeEntry({ userId: 'b', displayName: 'Siegmeyer', games: [makeGame({ rank: 2 })] }),
        ],
        vi.fn(),
        'sol',
      )

      expect(cards()).toHaveLength(1)
      expect(within(cards()[0]!).getByText('Solaire')).toBeInTheDocument()
    })
  })
})
