import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { EventGameMatrix } from './EventGameMatrix'
import { stubViewportWidth } from '../../../../test/viewport'
import type { EventGame, GameBreakdown, ScoreboardEntry, TrialProgress } from '../../../../types'

const eventGame = (eventGameId: string, gameName: string, isEnabled = false): EventGame => ({
  eventGameId,
  knownGameId: null,
  gameName,
  knownGameName: null,
  connectorSupported: false,
  isCustomGame: true,
  isEnabled,
  objectives: [],
})

const GAMES = [
  eventGame('ds', 'Dark Souls'),
  eventGame('er', 'Elden Ring', true),
  eventGame('sk', 'Sekiro'),
]

const breakdown = (eventGameId: string, overrides: Partial<GameBreakdown> = {}): GameBreakdown => ({
  eventGameId,
  gameName: GAMES.find((g) => g.eventGameId === eventGameId)?.gameName ?? eventGameId,
  score: 0,
  completedCount: 0,
  totalObjectives: 10,
  objectives: [],
  infos: [],
  hasDeathClip: false,
  failedCount: 0,
  isEnabled: eventGameId === 'er',
  isTrialActive: false,
  hasTrialRun: false,
  trial: null,
  rank: 1,
  ...overrides,
})

const makeTrial = (overrides: Partial<TrialProgress> = {}): TrialProgress => ({
  trialRunId: 't',
  state: 'Running',
  score: 0,
  completedCount: 0,
  failedCount: 0,
  lastCompletedAt: null,
  ...overrides,
})

const makeEntry = (
  userId: string,
  displayName: string,
  rank: number,
  games: Partial<Record<string, Partial<GameBreakdown>>> = {},
  overrides: Partial<ScoreboardEntry> = {},
): ScoreboardEntry => ({
  userId,
  displayName,
  twitchLogin: userId,
  isLive: false,
  totalScore: 0,
  completedCount: 0,
  isFinished: false,
  lastCompletedAt: null,
  totalInGameTimeMs: null,
  rank,
  games: GAMES.map((g) => breakdown(g.eventGameId, games[g.eventGameId])),
  failedCount: 0,
  status: 'Pending',
  ...overrides,
})

const renderMatrix = (entries: ScoreboardEntry[], onSelect = vi.fn(), query = '') => {
  render(<EventGameMatrix entries={entries} games={GAMES} query={query} onSelect={onSelect} />)
  return { onSelect }
}

const table = () => screen.getByRole('table', { name: 'Whole event standings' })
const headerCells = () =>
  within(within(table()).getAllByRole('row')[0]!).getAllByRole('columnheader')
const bodyRows = () => within(table()).getAllByRole('row').slice(1)
const cell = (player: string, game: string) =>
  screen.getByRole('button', { name: `${player}: ${game} objectives` })

describe('EventGameMatrix', () => {
  beforeEach(() => stubViewportWidth(true))

  it('has a column per game in event order, then the event total and rank', () => {
    renderMatrix([makeEntry('a', 'Solaire', 1)])

    expect(headerCells().map((h) => h.textContent)).toEqual([
      'Player',
      'Dark Souls',
      'Elden RingNow playing',
      'Sekiro',
      'Total',
      'Rank',
    ])
  })

  it('sorts rows by event rank', () => {
    renderMatrix([
      makeEntry('c', 'Third', 3),
      makeEntry('a', 'First', 1),
      makeEntry('b', 'Second', 2),
    ])

    expect(bodyRows().map((row) => within(row).getAllByRole('cell')[0]?.textContent)).toEqual([
      expect.stringContaining('First'),
      expect.stringContaining('Second'),
      expect.stringContaining('Third'),
    ])
  })

  it('shows score, progress and the per-game rank in each cell', () => {
    renderMatrix([
      makeEntry('a', 'Solaire', 2, {
        ds: { score: 120, completedCount: 4, totalObjectives: 10, rank: 2 },
      }),
      makeEntry('b', 'Siegmeyer', 1, { ds: { score: 300, completedCount: 9, rank: 1 } }),
    ])

    const solaireDs = cell('Solaire', 'Dark Souls')
    expect(solaireDs).toHaveTextContent('120')
    expect(solaireDs).toHaveTextContent('4/10')
    expect(solaireDs).toHaveTextContent('#2')
  })

  it('shows — instead of a rank for a game nobody has a result in yet', () => {
    renderMatrix([
      makeEntry('a', 'Solaire', 1, { ds: { completedCount: 1 } }),
      makeEntry('b', 'Siegmeyer', 2, { sk: { failedCount: 0, completedCount: 0, rank: 1 } }),
    ])

    expect(cell('Solaire', 'Dark Souls')).toHaveTextContent('#1')
    expect(cell('Solaire', 'Sekiro')).toHaveTextContent('—')
    expect(cell('Solaire', 'Sekiro')).not.toHaveTextContent('#')
    expect(cell('Siegmeyer', 'Sekiro')).not.toHaveTextContent('#')
  })

  it('counts a failure as a result for the rank', () => {
    renderMatrix([makeEntry('a', 'Solaire', 1, { sk: { failedCount: 1, rank: 1 } })])

    expect(cell('Solaire', 'Sekiro')).toHaveTextContent('#1')
  })

  it('shows the event total and rank', () => {
    renderMatrix([makeEntry('a', 'Solaire', 4, {}, { totalScore: 910 })])

    const cells = within(bodyRows()[0]!).getAllByRole('cell')
    expect(cells.at(-2)?.textContent).toBe('910')
    expect(cells.at(-1)?.textContent).toBe('4')
  })

  it('shows the amber trial score in a cell with a started trial, paused included', () => {
    renderMatrix([
      makeEntry('a', 'Solaire', 1, {
        sk: { trial: makeTrial({ score: 12 }), isTrialActive: true },
        ds: { trial: makeTrial({ score: 5, state: 'Paused' }) },
      }),
    ])

    expect(within(cell('Solaire', 'Sekiro')).getByText('Trial 12')).toBeInTheDocument()
    expect(within(cell('Solaire', 'Dark Souls')).getByText('Trial 5')).toBeInTheDocument()
    expect(within(cell('Solaire', 'Elden Ring')).queryByText(/Trial/)).toBeNull()
  })

  it('opens the right competitor and game from a cell, by click or keyboard', async () => {
    const { onSelect } = renderMatrix([
      makeEntry('a', 'Solaire', 1),
      makeEntry('b', 'Siegmeyer', 2),
    ])

    await userEvent.click(cell('Siegmeyer', 'Sekiro'))
    expect(onSelect).toHaveBeenLastCalledWith('b', 'sk')

    cell('Solaire', 'Dark Souls').focus()
    await userEvent.keyboard('{Enter}')
    expect(onSelect).toHaveBeenLastCalledWith('a', 'ds')

    cell('Solaire', 'Elden Ring').focus()
    await userEvent.keyboard(' ')
    expect(onSelect).toHaveBeenLastCalledWith('a', 'er')
    expect(onSelect).toHaveBeenCalledTimes(3)
  })

  it('keeps the event-wide Finished chip on the player', () => {
    renderMatrix([makeEntry('a', 'Solaire', 1, {}, { isFinished: true })])

    expect(within(bodyRows()[0]!).getByText('Finished')).toBeInTheDocument()
  })

  it('filters rows by the search query but still ranks games from every competitor', () => {
    renderMatrix(
      [
        makeEntry('a', 'Solaire', 1, { sk: { completedCount: 3, rank: 1 } }),
        makeEntry('b', 'Siegmeyer', 2, { sk: { rank: 2 } }),
      ],
      vi.fn(),
      'sieg',
    )

    expect(screen.queryByText('Solaire')).toBeNull()
    // Solaire, hidden, is the only one with a Sekiro result — the rank still shows.
    expect(cell('Siegmeyer', 'Sekiro')).toHaveTextContent('#2')
  })

  describe('narrow screens', () => {
    beforeEach(() => stubViewportWidth(false))

    const cards = () =>
      within(screen.getByRole('list', { name: 'Whole event standings' })).getAllByRole('listitem', {
        name: /./,
      })

    it('renders a card per competitor in event-rank order instead of a table', () => {
      renderMatrix([makeEntry('b', 'Second', 2), makeEntry('a', 'First', 1)])

      expect(screen.queryByRole('table')).toBeNull()
      expect(cards().map((card) => card.getAttribute('aria-label'))).toEqual(['First', 'Second'])
    })

    it('shows the event total and rank on each card', () => {
      renderMatrix([makeEntry('a', 'Solaire', 4, {}, { totalScore: 910 })])

      expect(within(cards()[0]!).getByText('910 · #4')).toBeInTheDocument()
    })

    it('has one line per game in event order with score, progress and per-game rank', () => {
      renderMatrix([
        makeEntry('a', 'Solaire', 1, { ds: { score: 120, completedCount: 4, rank: 2 } }),
        makeEntry('b', 'Siegmeyer', 2, { ds: { completedCount: 9, rank: 1 } }),
      ])

      const lines = within(cards()[0]!).getAllByRole('button', { name: /^Solaire: / })
      expect(lines.map((line) => line.getAttribute('aria-label'))).toEqual([
        'Solaire: Dark Souls objectives',
        'Solaire: Elden Ring objectives',
        'Solaire: Sekiro objectives',
      ])
      expect(lines[0]).toHaveTextContent('120')
      expect(lines[0]).toHaveTextContent('4/10')
      expect(lines[0]).toHaveTextContent('#2')
      expect(lines[2]).toHaveTextContent('—')
    })

    it('opens the objectives from a game line, by tap or keyboard', async () => {
      const { onSelect } = renderMatrix([makeEntry('a', 'Solaire', 1)])

      await userEvent.click(cell('Solaire', 'Sekiro'))
      expect(onSelect).toHaveBeenLastCalledWith('a', 'sk')

      cell('Solaire', 'Dark Souls').focus()
      await userEvent.keyboard('{Enter}')
      expect(onSelect).toHaveBeenLastCalledWith('a', 'ds')
    })

    it('shows the amber trial score on a game line with a started trial', () => {
      renderMatrix([makeEntry('a', 'Solaire', 1, { sk: { trial: makeTrial({ score: 12 }) } })])

      expect(within(cell('Solaire', 'Sekiro')).getByText('Trial 12')).toBeInTheDocument()
    })

    it('filters cards by the search query', () => {
      renderMatrix([makeEntry('a', 'Solaire', 1), makeEntry('b', 'Siegmeyer', 2)], vi.fn(), 'sieg')

      expect(cards().map((card) => card.getAttribute('aria-label'))).toEqual(['Siegmeyer'])
    })
  })
})
