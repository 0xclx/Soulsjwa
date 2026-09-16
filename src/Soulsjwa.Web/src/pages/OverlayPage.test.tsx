import { afterEach, describe, expect, it, vi } from 'vitest'
import { act, cleanup, render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { OverlayPage, PREVIEW_WAITING_MESSAGE } from './OverlayPage'
import { PAGE_FADE_MS } from '../features/events/overlay/components/OverlayRenderer'
import type { GameBreakdown, ObjectiveDetail, ScoreboardResponse } from '../types'
import type { OverlayTokenSettings } from '../types/overlay'
import { OVERLAY_DEFAULT_SETTINGS } from '../features/events/overlay/overlayConfig'
import {
  OVERLAY_PREVIEW_MESSAGE_TYPE,
  OVERLAY_PREVIEW_READY_MESSAGE_TYPE,
} from '../features/events/overlay/overlayPreviewProtocol'

const EVENT_ID = 'event-1'
const EVENT_NAME = 'Lordran Relay'
const OVERLAY_TOKEN = 'ot_test'
const PLAYER_ID = 'player-1'
const PLAYER_NAME = 'Chosen Undead'
const GAME_ID = 'game-1'
const GAME_NAME = 'Dark Souls'
const PAST_GAME_ID = 'game-0'
const PAST_GAME_NAME = 'Sekiro'
const COMPLETED_OBJECTIVE_NAME = 'Ring both Bells of Awakening'
const INCOMPLETE_OBJECTIVE_NAME = 'Defeat Capra Demon'
const CATEGORY_NAME = 'Undead Parish'

const scoreboard: ScoreboardResponse = {
  tieBreakMode: 'ByTime',
  entries: [
    {
      userId: PLAYER_ID,
      displayName: PLAYER_NAME,
      twitchLogin: 'chosenundead',
      isLive: true,
      totalScore: 100,
      completedCount: 1,
      isFinished: false,
      lastCompletedAt: '2026-07-03T12:00:00.000Z',
      totalInGameTimeMs: null,
      rank: 1,
      failedCount: 0,
      status: 'Pending',
      games: [
        {
          eventGameId: PAST_GAME_ID,
          gameName: PAST_GAME_NAME,
          score: 30,
          completedCount: 1,
          totalObjectives: 1,
          hasDeathClip: false,
          infos: [],
          failedCount: 0,
          isEnabled: false,
          isTrialActive: false,
          hasTrialRun: false,
          trial: null,
          objectives: [],
        },
        {
          eventGameId: GAME_ID,
          gameName: GAME_NAME,
          score: 100,
          completedCount: 1,
          totalObjectives: 2,
          hasDeathClip: false,
          infos: [],
          failedCount: 0,
          isEnabled: true,
          isTrialActive: false,
          hasTrialRun: false,
          trial: null,
          objectives: [
            {
              objectiveId: 'objective-1',
              name: COMPLETED_OBJECTIVE_NAME,
              score: 100,
              category: CATEGORY_NAME,
              isCompleted: true,
              completedAt: '2026-07-03T12:00:00.000Z',
              isFailed: false,
              failedAt: null,
              status: 'Completed',
              trial: null,
            },
            {
              objectiveId: 'objective-2',
              name: INCOMPLETE_OBJECTIVE_NAME,
              score: 50,
              category: CATEGORY_NAME,
              isCompleted: false,
              completedAt: null,
              isFailed: false,
              failedAt: null,
              status: 'Pending',
              trial: null,
            },
          ],
        },
      ],
    },
  ],
}

const mocks = vi.hoisted(() => ({
  scoreboardData: null as ScoreboardResponse | null,
  savedSettings: null as OverlayTokenSettings | null,
}))

vi.mock('../features/events/hooks/useOverlayScoreboard', () => ({
  useOverlayScoreboard: () => ({
    data: mocks.scoreboardData
      ? { scoreboard: mocks.scoreboardData, settings: mocks.savedSettings }
      : undefined,
    error: null,
  }),
}))

vi.mock('../features/events/hooks/useEvent', () => ({
  useEvent: () => ({ data: { id: EVENT_ID, name: EVENT_NAME } }),
}))

function overlayTree(query = '') {
  return (
    <MemoryRouter initialEntries={[`/events/${EVENT_ID}/overlay?token=${OVERLAY_TOKEN}${query}`]}>
      <Routes>
        <Route path="/events/:id/overlay" element={<OverlayPage />} />
      </Routes>
    </MemoryRouter>
  )
}

function renderOverlay(query = '') {
  mocks.scoreboardData = scoreboard
  mocks.savedSettings = null
  return render(overlayTree(query))
}

describe('<OverlayPage />', () => {
  it('draws the look saved on the token over what the URL asks for', () => {
    mocks.scoreboardData = scoreboard
    mocks.savedSettings = { ...OVERLAY_DEFAULT_SETTINGS, view: 'scores', title: 'Saved title' }

    render(overlayTree('&view=objectives&title=URL%20title'))

    // Scores view: the competitor row with its "Done:" line, not the checklist.
    expect(screen.getByText(`Done: ${COMPLETED_OBJECTIVE_NAME}`)).toBeInTheDocument()
    expect(screen.getByText('Saved title')).toBeInTheDocument()
    expect(screen.queryByText('URL title')).not.toBeInTheDocument()
  })

  it('shows each competitor completed objective names in the scores view', () => {
    renderOverlay('&view=scores')

    expect(screen.getByText(PLAYER_NAME)).toBeInTheDocument()
    expect(screen.getByText(`Done: ${COMPLETED_OBJECTIVE_NAME}`)).toBeInTheDocument()
    expect(screen.queryByText(INCOMPLETE_OBJECTIVE_NAME)).not.toBeInTheDocument()
  })

  it('groups objectives by game and category in the default objectives view', () => {
    renderOverlay()

    // Game and category headers are rendered.
    expect(screen.getByText(GAME_NAME)).toBeInTheDocument()
    expect(screen.getByText(CATEGORY_NAME)).toBeInTheDocument()

    // Both completed and incomplete objectives are listed.
    const completed = screen.getByText(COMPLETED_OBJECTIVE_NAME)
    expect(completed).toBeInTheDocument()
    expect(screen.getByText(INCOMPLETE_OBJECTIVE_NAME)).toBeInTheDocument()

    // Completed objectives are struck through (our own line, sized to the
    // text) and each row shows its score.
    expect(completed.querySelector('[data-testid="objective-strike"]')).not.toBeNull()
    expect(
      screen.getByText(INCOMPLETE_OBJECTIVE_NAME).querySelector('[data-testid="objective-strike"]'),
    ).toBeNull()
    expect(screen.getByText('100')).toBeInTheDocument()
    expect(screen.getByText('50')).toBeInTheDocument()
  })

  it('shows the score against the points on offer, in the title and on every row', () => {
    renderOverlay()
    // Objectives view title: the active game's 100 + 50 are on offer; the past game's 30 is not.
    expect(screen.getByText(`${PLAYER_NAME} · 100 / 150 pts`)).toBeInTheDocument()

    cleanup()
    renderOverlay('&view=scores')
    expect(screen.getByLabelText('of')).toHaveTextContent('/ 150')
  })

  it('shows the competitor’s progress bar in the objectives view, unless progress is hidden', () => {
    renderOverlay()
    const bar = screen.getByRole('progressbar', { name: `${PLAYER_NAME} completion progress` })
    // One of the active game's two objectives is done.
    expect(bar).toHaveAttribute('aria-valuenow', '50')
    expect(screen.getByTestId('objectives-progress')).toHaveTextContent('1 / 2 objectives')

    cleanup()
    renderOverlay('&showProgress=0')
    expect(screen.queryByRole('progressbar')).not.toBeInTheDocument()
  })

  it("shows only the event's active game by default, hiding a past disabled game", () => {
    renderOverlay()

    expect(screen.getByText(GAME_NAME)).toBeInTheDocument()
    expect(screen.queryByText(PAST_GAME_NAME)).not.toBeInTheDocument()
  })

  it('shows a game pinned via ?games= even when it is not the active game', () => {
    renderOverlay(`&view=games&games=${PAST_GAME_ID}`)

    expect(screen.getByText(PAST_GAME_NAME)).toBeInTheDocument()
    expect(screen.queryByText(GAME_NAME)).not.toBeInTheDocument()
  })
})

// `filterEntry` recomputes `lastCompletedAt` from the visible games' own
// objective `completedAt` fields rather than passing the entry-level field
// through, so highlight tests must drive completion via the objective.
function entryWithCompletion(completedAt: string | null): ScoreboardResponse['entries'][number] {
  const baseEntry = scoreboard.entries[0]!
  const [pastGame, activeGame] = baseEntry.games as [GameBreakdown, GameBreakdown]
  const [firstObjective, ...restObjectives] = activeGame.objectives as [
    ObjectiveDetail,
    ...ObjectiveDetail[],
  ]
  return {
    ...baseEntry,
    games: [
      pastGame,
      {
        ...activeGame,
        objectives: [
          { ...firstObjective, isCompleted: completedAt !== null, completedAt },
          ...restObjectives,
        ],
      },
    ],
  }
}

describe('<OverlayPage /> change highlights', () => {
  afterEach(() => {
    vi.useRealTimers()
  })

  it('flashes a newly-completed row and clears it on schedule, surviving an unrelated poll mid-window', async () => {
    vi.useFakeTimers()
    mocks.scoreboardData = { ...scoreboard, entries: [entryWithCompletion(null)] }

    const { container, rerender } = render(overlayTree('&view=scores&highlightSeconds=6'))

    // The first render only establishes the baseline — nothing is highlighted.
    expect(container.querySelector('[data-highlight="true"]')).toBeNull()

    // A completion lands.
    mocks.scoreboardData = {
      ...scoreboard,
      entries: [entryWithCompletion('2026-07-03T12:00:00.000Z')],
    }
    act(() => {
      rerender(overlayTree('&view=scores&highlightSeconds=6'))
    })
    await act(async () => {
      await vi.advanceTimersByTimeAsync(0)
    })
    expect(container.querySelector('[data-highlight="true"]')).not.toBeNull()

    // A later poll with no new completion lands mid-window — this used to
    // cancel the pending un-highlight without rescheduling it, leaving the
    // row flashed forever. It must stay flashed (not clear early) here.
    await act(async () => {
      await vi.advanceTimersByTimeAsync(3000)
    })
    act(() => {
      rerender(overlayTree('&view=scores&highlightSeconds=6'))
    })
    await act(async () => {
      await vi.advanceTimersByTimeAsync(0)
    })
    expect(container.querySelector('[data-highlight="true"]')).not.toBeNull()

    // Once the full highlight window has elapsed, it clears on its own.
    await act(async () => {
      await vi.advanceTimersByTimeAsync(3500)
    })
    expect(container.querySelector('[data-highlight="true"]')).toBeNull()
  })

  it('jumps to the page holding a freshly flashing row, and does not drag back while it fades', async () => {
    vi.useFakeTimers()
    const second = {
      ...entryWithCompletion(null),
      userId: 'player-2',
      displayName: 'Second Player',
      totalScore: 10,
    }
    // Two rows, one per page: the flash will land on the page not on screen.
    mocks.scoreboardData = { ...scoreboard, entries: [entryWithCompletion(null), second] }
    const { rerender } = render(overlayTree('&view=scores&pageSize=1&cycle=0&highlightSeconds=6'))
    expect(screen.getByLabelText('Page indicator')).toHaveTextContent('Page 1 / 2')
    expect(screen.getByText(PLAYER_NAME)).toBeInTheDocument()

    // The second player completes something.
    mocks.scoreboardData = {
      ...scoreboard,
      entries: [
        entryWithCompletion(null),
        {
          ...entryWithCompletion('2026-07-03T12:00:00.000Z'),
          userId: 'player-2',
          displayName: 'Second Player',
          totalScore: 10,
        },
      ],
    }
    act(() => {
      rerender(overlayTree('&view=scores&pageSize=1&cycle=0&highlightSeconds=6'))
    })
    await act(async () => {
      await vi.advanceTimersByTimeAsync(0)
    })
    // The old page is still on screen, fading out — no cut.
    expect(screen.getByTestId('overlay-page')).toHaveAttribute('data-fading-out', 'true')
    expect(screen.getByText(PLAYER_NAME)).toBeInTheDocument()
    await act(async () => {
      await vi.advanceTimersByTimeAsync(PAGE_FADE_MS)
    })
    expect(screen.getByTestId('overlay-page')).not.toHaveAttribute('data-fading-out')
    expect(screen.getByLabelText('Page indicator')).toHaveTextContent('Page 2 / 2')
    expect(screen.getByText('Second Player')).toBeInTheDocument()
    expect(screen.getByText('Second Player').closest('[data-highlight="true"]')).not.toBeNull()

    // A poll with nothing new while the row still flashes does not move the page,
    // and neither does the flash clearing.
    act(() => {
      rerender(overlayTree('&view=scores&pageSize=1&cycle=0&highlightSeconds=6'))
    })
    await act(async () => {
      await vi.advanceTimersByTimeAsync(6500)
    })
    expect(screen.getByLabelText('Page indicator')).toHaveTextContent('Page 2 / 2')
    expect(screen.queryByText('Second Player')!.closest('[data-highlight="true"]')).toBeNull()
  })

  it('restarts the cycle on a flash and holds the page for the highlight duration', async () => {
    vi.useFakeTimers()
    const second = {
      ...entryWithCompletion(null),
      userId: 'player-2',
      displayName: 'Second Player',
      totalScore: 10,
    }
    // Two pages cycling every second, a highlight that lasts six.
    mocks.scoreboardData = { ...scoreboard, entries: [entryWithCompletion(null), second] }
    const query = '&view=scores&pageSize=1&cycle=1&highlightSeconds=6'
    const { rerender } = render(overlayTree(query))
    expect(screen.getByLabelText('Page indicator')).toHaveTextContent('Page 1 / 2')

    // The row already on screen completes something.
    mocks.scoreboardData = {
      ...scoreboard,
      entries: [entryWithCompletion('2026-07-03T12:00:00.000Z'), second],
    }
    act(() => {
      rerender(overlayTree(query))
    })
    await act(async () => {
      await vi.advanceTimersByTimeAsync(0)
    })
    expect(screen.getByLabelText('Page indicator')).toHaveTextContent('Page 1 / 2')

    // Well past the one-second cycle, the flashing page is still held.
    await act(async () => {
      await vi.advanceTimersByTimeAsync(4000)
    })
    expect(screen.getByLabelText('Page indicator')).toHaveTextContent('Page 1 / 2')

    // Once the highlight has run its course, cycling resumes: the tick starts
    // the cross-fade, and the new page is on screen once it has finished.
    await act(async () => {
      await vi.advanceTimersByTimeAsync(2500)
    })
    expect(screen.getByTestId('overlay-page')).toHaveAttribute('data-fading-out', 'true')
    await act(async () => {
      await vi.advanceTimersByTimeAsync(PAGE_FADE_MS)
    })
    expect(screen.getByLabelText('Page indicator')).toHaveTextContent('Page 2 / 2')
  })

  it('animates a fresh completion in the objectives view and jumps to its page', async () => {
    vi.useFakeTimers()
    mocks.scoreboardData = { ...scoreboard, entries: [entryWithCompletion(null)] }
    // One objective per page: the first on page 1, the still-open one on page 2.
    const query = '&view=objectives&pageSize=1&cycle=0&highlightSeconds=6'
    const { rerender } = render(overlayTree(query))
    expect(screen.getByLabelText('Page indicator')).toHaveTextContent('Page 1 / 2')

    // The open objective on page 2 gets completed.
    const base = entryWithCompletion(null)
    mocks.scoreboardData = {
      ...scoreboard,
      entries: [
        {
          ...base,
          games: base.games.map((g) => ({
            ...g,
            objectives: g.objectives.map((o) =>
              o.name === INCOMPLETE_OBJECTIVE_NAME
                ? {
                    ...o,
                    isCompleted: true,
                    completedAt: '2026-07-03T13:00:00.000Z',
                    status: 'Completed' as const,
                  }
                : o,
            ),
          })),
        },
      ],
    }
    act(() => {
      rerender(overlayTree(query))
    })
    await act(async () => {
      await vi.advanceTimersByTimeAsync(0)
    })
    await act(async () => {
      await vi.advanceTimersByTimeAsync(PAGE_FADE_MS)
    })

    expect(screen.getByLabelText('Page indicator')).toHaveTextContent('Page 2 / 2')
    const row = screen.getByText(INCOMPLETE_OBJECTIVE_NAME).closest('[data-completed="true"]')
    expect(row).toHaveAttribute('data-fresh', 'true')
    expect(screen.getByTestId('objective-strike')).toHaveAttribute('data-sweeping', 'true')

    // After the highlight window it settles: the same line stays, no longer sweeping.
    await act(async () => {
      await vi.advanceTimersByTimeAsync(6500)
    })
    expect(row).not.toHaveAttribute('data-fresh')
    expect(screen.getByTestId('objective-strike')).not.toHaveAttribute('data-sweeping')
  })

  it('does not leak a pending highlight timer past unmount', async () => {
    vi.useFakeTimers()
    const consoleError = vi.spyOn(console, 'error').mockImplementation(() => {})
    mocks.scoreboardData = { ...scoreboard, entries: [entryWithCompletion(null)] }

    const { unmount, rerender } = render(overlayTree('&view=scores&highlightSeconds=6'))

    mocks.scoreboardData = {
      ...scoreboard,
      entries: [entryWithCompletion('2026-07-03T12:00:00.000Z')],
    }
    act(() => {
      rerender(overlayTree('&view=scores&highlightSeconds=6'))
    })
    await act(async () => {
      await vi.advanceTimersByTimeAsync(0)
    })

    unmount()
    await act(async () => {
      await vi.advanceTimersByTimeAsync(6000)
    })

    expect(consoleError).not.toHaveBeenCalled()
    consoleError.mockRestore()
  })

  const withTrialOnActiveGame = (state: 'Running' | 'Paused'): ScoreboardResponse => ({
    ...scoreboard,
    entries: scoreboard.entries.map((e) => ({
      ...e,
      games: e.games.map((g) =>
        g.eventGameId === GAME_ID
          ? {
              ...g,
              isTrialActive: state === 'Running',
              trial: {
                trialRunId: 't1',
                state,
                score: 25,
                completedCount: 1,
                failedCount: 0,
                lastCompletedAt: '2026-07-03T12:00:00.000Z',
              },
            }
          : g,
      ),
    })),
  })

  it('badges a recording trial in the objectives view and titles its score', () => {
    // This view is the default and draws the trial's marks, so it must carry
    // its own badge — the scores view's badge is not on screen here.
    mocks.scoreboardData = withTrialOnActiveGame('Running')
    render(overlayTree())

    // The game header carries the badge, and the progress line names its figures as the trial's.
    expect(screen.getAllByText('TRIAL').length).toBeGreaterThanOrEqual(1)
    expect(screen.getByText(/TRIAL 25/)).toBeInTheDocument()
    expect(screen.getByTestId('objectives-progress')).toHaveTextContent('TRIAL 1 / 2 objectives')
  })

  it('does not badge a paused trial in the objectives view', () => {
    // Paused hands the marks back to the official record, so labelling them
    // as a trial would be a lie.
    mocks.scoreboardData = withTrialOnActiveGame('Paused')
    render(overlayTree())

    expect(screen.getByText(GAME_NAME)).toBeInTheDocument()
    expect(screen.queryByText('TRIAL')).not.toBeInTheDocument()
  })
})

describe('<OverlayPage /> preview mode', () => {
  const previewTree = () => (
    <MemoryRouter initialEntries={[`/events/${EVENT_ID}/overlay?preview=1`]}>
      <Routes>
        <Route path="/events/:id/overlay" element={<OverlayPage />} />
      </Routes>
    </MemoryRouter>
  )

  const post = (data: unknown, source: MessageEventSource | null = window) =>
    act(() => {
      window.dispatchEvent(
        new MessageEvent('message', { data, origin: window.location.origin, source }),
      )
    })

  it('polls nothing, announces itself, and waits until the embedding window posts a look', () => {
    mocks.scoreboardData = scoreboard
    const postMessage = vi.spyOn(window, 'postMessage')

    render(previewTree())

    expect(screen.getByText(PREVIEW_WAITING_MESSAGE)).toBeInTheDocument()
    // The live scoreboard the hook would have returned is never drawn here.
    expect(screen.queryByText(PLAYER_NAME)).not.toBeInTheDocument()
    expect(postMessage).toHaveBeenCalledWith(
      { type: OVERLAY_PREVIEW_READY_MESSAGE_TYPE },
      window.location.origin,
    )
    postMessage.mockRestore()
  })

  it('renders what its parent posts and ignores every other sender', () => {
    mocks.scoreboardData = null
    render(previewTree())

    const message = {
      type: OVERLAY_PREVIEW_MESSAGE_TYPE,
      config: { ...OVERLAY_DEFAULT_SETTINGS, view: 'scores', background: null },
      scoreboard,
      eventName: 'Posted event',
    }

    // Not from the embedding window: nothing changes.
    post(message, null)
    expect(screen.getByText(PREVIEW_WAITING_MESSAGE)).toBeInTheDocument()

    post(message)
    expect(screen.queryByText(PREVIEW_WAITING_MESSAGE)).not.toBeInTheDocument()
    expect(screen.getByText('Posted event')).toBeInTheDocument()
    expect(screen.getByText(PLAYER_NAME)).toBeInTheDocument()

    // A later post re-renders in place — no reload, the same frame.
    post({ ...message, config: { ...message.config, title: 'Renamed' } })
    expect(screen.getByText('Renamed')).toBeInTheDocument()
  })
})
