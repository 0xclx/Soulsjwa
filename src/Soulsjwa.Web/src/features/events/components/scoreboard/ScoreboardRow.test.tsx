import userEvent from '@testing-library/user-event'
import { render, screen, within } from '@testing-library/react'
import { Table, TableBody } from '@mui/material'
import { describe, expect, it } from 'vitest'
import { ScoreboardRow } from './ScoreboardRow'
import type {
  GameBreakdown,
  ObjectiveDetail,
  ScoreboardEntry,
  TrialProgress,
} from '../../../../types'

const makeTrial = (overrides: Partial<TrialProgress> = {}): TrialProgress => ({
  trialRunId: 't1',
  state: 'Running',
  score: 0,
  completedCount: 0,
  failedCount: 0,
  lastCompletedAt: null,
  ...overrides,
})

const makeGame = (overrides: Partial<GameBreakdown>): GameBreakdown => ({
  eventGameId: 'g',
  gameName: 'Game',
  score: 0,
  completedCount: 0,
  totalObjectives: 0,
  objectives: [],
  infos: [],
  hasDeathClip: false,
  failedCount: 0,
  isEnabled: false,
  isTrialActive: false,
  hasTrialRun: false,
  trial: null,
  ...overrides,
})

const makeObjective = (overrides: Partial<ObjectiveDetail> = {}): ObjectiveDetail => ({
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

const makeEntry = (games: GameBreakdown[]): ScoreboardEntry => ({
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
  games,
  failedCount: 0,
  status: 'Pending',
})

const renderRow = (games: GameBreakdown[]) =>
  render(
    <Table>
      <TableBody>
        <ScoreboardRow
          entry={makeEntry(games)}
          rank={1}
          eventId="event-1"
          event={undefined}
          currentUser={undefined}
        />
      </TableBody>
    </Table>,
  )

describe('ScoreboardRow', () => {
  it('renders exactly one game section — the active game — even when the competitor has played several', async () => {
    const games = [
      makeGame({ eventGameId: 'past-1', gameName: 'Dark Souls' }),
      makeGame({ eventGameId: 'active', gameName: 'Elden Ring', isEnabled: true }),
      makeGame({ eventGameId: 'past-2', gameName: 'Sekiro' }),
    ]
    renderRow(games)

    await userEvent.click(screen.getByText('Player One'))

    expect(screen.getAllByText('Elden Ring')).toHaveLength(1)
    expect(screen.queryByText('Dark Souls')).not.toBeInTheDocument()
    expect(screen.queryByText('Sekiro')).not.toBeInTheDocument()
  })

  it('renders no game section when the event has no active game yet', async () => {
    const games = [makeGame({ eventGameId: 'past-1', gameName: 'Dark Souls' })]
    const { container } = renderRow(games)

    await userEvent.click(screen.getByText('Player One'))

    expect(within(container).queryByText('Dark Souls')).not.toBeInTheDocument()
  })

  it('shows a Trial badge when and only when a started trial is reported', () => {
    const withTrial = [
      makeGame({ eventGameId: 'active', isEnabled: true, isTrialActive: true, trial: makeTrial() }),
    ]
    const { unmount } = renderRow(withTrial)
    expect(screen.getByText('Trial')).toBeInTheDocument()
    unmount()

    const withoutTrial = [
      makeGame({ eventGameId: 'active', isEnabled: true, isTrialActive: false }),
    ]
    renderRow(withoutTrial)
    expect(screen.queryByText('Trial')).not.toBeInTheDocument()
  })

  it('keeps showing the badge and score for a paused trial', () => {
    renderRow([
      makeGame({
        eventGameId: 'active',
        isEnabled: true,
        isTrialActive: false,
        trial: makeTrial({ state: 'Paused', score: 7 }),
      }),
    ])

    expect(screen.getByText('Trial')).toBeInTheDocument()
    expect(screen.getByText('Trial 7')).toBeInTheDocument()
  })

  it('shows the trial figures beside the official ones without altering them', () => {
    renderRow([
      makeGame({
        eventGameId: 'active',
        isEnabled: true,
        isTrialActive: true,
        trial: makeTrial({ score: 45, completedCount: 3, failedCount: 2 }),
      }),
    ])

    // makeEntry reports 10 official points and 1 official completion. Bound to
    // the screen-reader labels, not bare digits — asserting '3' and '2' alone
    // passed even with the completed and failed figures swapped.
    expect(screen.getByText('10')).toBeInTheDocument()
    expect(screen.getByText('Trial 45')).toBeInTheDocument()
    expect(screen.getByText('Completed in trial:').parentElement).toHaveTextContent('3')
    expect(screen.getByText('Failed in trial:').parentElement).toHaveTextContent('2')
  })

  it('surfaces a trial on a game that is not the active one, naming that game', async () => {
    renderRow([
      makeGame({ eventGameId: 'active', gameName: 'Elden Ring', isEnabled: true }),
      makeGame({
        eventGameId: 'trialed',
        gameName: 'Sekiro',
        isEnabled: false,
        isTrialActive: true,
        trial: makeTrial({ score: 12 }),
      }),
    ])

    expect(screen.getByText('Trial · Sekiro')).toBeInTheDocument()
    expect(screen.getByText('Trial 12')).toBeInTheDocument()

    // ...and its breakdown is reachable, which activeGame alone would skip.
    await userEvent.click(screen.getByText('Player One'))
    expect(screen.getByText('Sekiro')).toBeInTheDocument()
    expect(screen.getByText('Elden Ring')).toBeInTheDocument()
  })

  it('expands a paused trial on a non-active game to that run, not the official record', async () => {
    // The game is on screen only because of the trial, so its official marks
    // would be ticks with no points behind them. Matches what the overlay now
    // draws for the same data.
    renderRow([
      makeGame({ eventGameId: 'active', gameName: 'Elden Ring', isEnabled: true }),
      makeGame({
        eventGameId: 'trialed',
        gameName: 'Sekiro',
        isEnabled: false,
        isTrialActive: false,
        trial: makeTrial({ state: 'Paused', score: 12 }),
        totalObjectives: 1,
        objectives: [
          makeObjective({
            objectiveId: 'obj',
            name: 'Practised only',
            isCompleted: true,
            completedAt: '2026-01-01T09:00:00Z',
            trial: {
              isCompleted: false,
              completedAt: null,
              isFailed: false,
              failedAt: null,
              status: 'Pending',
            },
          }),
        ],
      }),
    ])

    await userEvent.click(screen.getByText('Player One'))

    // The trial has not completed it, so it must read as not done despite the
    // official completion, and the list must say which run it is showing.
    expect(screen.getByText(/Showing this trial run/)).toBeInTheDocument()
    expect(screen.queryByText('01/01/2026, 09:00:00')).not.toBeInTheDocument()
  })

  it('hands a paused trial on the active game back to the official record', async () => {
    renderRow([
      makeGame({
        eventGameId: 'active',
        gameName: 'Elden Ring',
        isEnabled: true,
        isTrialActive: false,
        trial: makeTrial({ state: 'Paused', score: 12 }),
        totalObjectives: 1,
        objectives: [makeObjective({ objectiveId: 'obj', name: 'Really done', isCompleted: true })],
      }),
    ])

    await userEvent.click(screen.getByText('Player One'))

    // Its score stays on screen (on the row and again in the breakdown), but
    // the marks are official again — this is the game the row's own figures
    // report on, so there is nothing to warn about.
    expect(screen.getAllByText('Trial 12').length).toBeGreaterThan(0)
    expect(screen.queryByText(/Showing this trial run/)).not.toBeInTheDocument()
  })

  it('labels the last-completed column when it is the trial that is showing', async () => {
    renderRow([
      makeGame({
        eventGameId: 'active',
        gameName: 'Elden Ring',
        isEnabled: true,
        isTrialActive: true,
        completedCount: 1,
        totalObjectives: 2,
        trial: makeTrial({ lastCompletedAt: '2026-02-02T08:00:00Z' }),
      }),
    ])

    await userEvent.click(screen.getByText('Player One'))

    // Bound to the screen-reader label, not the date text: the counts in the
    // cell beside it stay official, so an unmarked trial timestamp reads as
    // the date of those official completions.
    expect(screen.getByText('Last completed in trial:')).toBeInTheDocument()
  })
})
