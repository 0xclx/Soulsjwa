import { describe, expect, it } from 'vitest'
import type { TwitchExtensionEntry, TwitchExtensionScoreboard } from '../../types/twitchExtension'
import {
  ACTIVE_SCOPE,
  ALL_SCOPE,
  buildBoardView,
  initialScope,
  sameScope,
  scopedGame,
} from './scope'

const ER = 'game-er'
const DS3 = 'game-ds3'
const STREAMER = 'user-streamer'

const entry = (
  overrides: Partial<TwitchExtensionEntry> & Pick<TwitchExtensionEntry, 'userId'>,
): TwitchExtensionEntry => ({
  displayName: overrides.userId,
  twitchLogin: overrides.userId,
  profileImageUrl: null,
  isLive: false,
  rank: 1,
  totalScore: 0,
  completedCount: 0,
  failedCount: 0,
  isFinished: false,
  status: 'Pending',
  lastCompletedAt: null,
  totalInGameTimeMs: null,
  games: [],
  ...overrides,
})

const board = (overrides: Partial<TwitchExtensionScoreboard> = {}): TwitchExtensionScoreboard => ({
  channelId: '1',
  policy: {
    allowChannelEventChoice: true,
    allowViewerScopeSwitch: true,
    defaultScope: 'AllGames',
    defaultHighlightChannelCompetitor: true,
    defaultShowTrialProgress: true,
  },
  settings: {
    eventId: null,
    defaultScope: 'AllGames',
    pinnedEventGameId: null,
    highlightChannelCompetitor: true,
    showTrialProgress: true,
  },
  event: {
    id: 'event',
    name: 'Autumn Ashes Cup',
    urlAlias: 'autumn',
    isStarted: true,
    isFeatured: true,
    tieBreakMode: 'SharedPlace',
    activeEventGameId: ER,
    source: 'Featured',
  },
  channelCompetitorUserId: STREAMER,
  games: [
    { eventGameId: ER, name: 'Elden Ring', isEnabled: true, sortOrder: 0, totalObjectives: 10 },
    {
      eventGameId: DS3,
      name: 'Dark Souls III',
      isEnabled: false,
      sortOrder: 1,
      totalObjectives: 4,
    },
  ],
  entries: [
    entry({
      userId: 'mira',
      rank: 1,
      totalScore: 30,
      completedCount: 5,
      lastCompletedAt: '2026-09-01T10:00:00Z',
      games: [
        {
          eventGameId: ER,
          score: 10,
          completedCount: 2,
          failedCount: 0,
          lastCompletedAt: '2026-09-01T10:00:00Z',
          isTrialActive: false,
          trial: null,
        },
        {
          eventGameId: DS3,
          score: 20,
          completedCount: 3,
          failedCount: 0,
          lastCompletedAt: '2026-09-01T09:00:00Z',
          isTrialActive: false,
          trial: null,
        },
      ],
    }),
    entry({
      userId: STREAMER,
      rank: 2,
      totalScore: 25,
      completedCount: 4,
      lastCompletedAt: '2026-09-01T09:30:00Z',
      games: [
        {
          eventGameId: ER,
          score: 25,
          completedCount: 4,
          failedCount: 1,
          lastCompletedAt: '2026-09-01T09:30:00Z',
          isTrialActive: false,
          trial: null,
        },
        {
          eventGameId: DS3,
          score: 0,
          completedCount: 0,
          failedCount: 0,
          lastCompletedAt: null,
          isTrialActive: true,
          trial: {
            trialRunId: 't',
            state: 'Running',
            score: 7,
            completedCount: 1,
            failedCount: 0,
            lastCompletedAt: '2026-09-01T11:00:00Z',
          },
        },
      ],
    }),
    entry({
      userId: 'gus',
      rank: 3,
      totalScore: 10,
      completedCount: 2,
      games: [
        {
          eventGameId: ER,
          score: 10,
          completedCount: 2,
          failedCount: 0,
          lastCompletedAt: '2026-09-01T08:00:00Z',
          isTrialActive: false,
          trial: null,
        },
        {
          eventGameId: DS3,
          score: 0,
          completedCount: 0,
          failedCount: 0,
          lastCompletedAt: null,
          isTrialActive: false,
          trial: null,
        },
      ],
    }),
  ],
  ...overrides,
})

describe('buildBoardView', () => {
  it('all games keeps the server ranking and sums trial figures across games', () => {
    const view = buildBoardView(board(), ALL_SCOPE)

    expect(view.game).toBeNull()
    expect(view.rows.map((r) => [r.userId, r.rank, r.score])).toEqual([
      ['mira', 1, 30],
      [STREAMER, 2, 25],
      ['gus', 3, 10],
    ])
    expect(view.rows[0]?.totalObjectives).toBe(14)
    const streamer = view.rows[1]!
    expect(streamer.isChannelCompetitor).toBe(true)
    expect(streamer.trialScore).toBe(7)
    expect(streamer.trialLastCompletedAt).toBe('2026-09-01T11:00:00Z')
    expect(view.rows[0]?.trialScore).toBeNull()
  })

  it('the active game narrows every figure and re-ranks within it', () => {
    const view = buildBoardView(board(), ACTIVE_SCOPE)

    expect(view.game?.eventGameId).toBe(ER)
    // Tied rows are ordered by their earlier newest completion, as the server orders ties.
    expect(
      view.rows.map((r) => [r.userId, r.rank, r.score, r.completedCount, r.failedCount]),
    ).toEqual([
      [STREAMER, 1, 25, 4, 1],
      ['gus', 2, 10, 2, 0],
      ['mira', 2, 10, 2, 0],
    ])
    expect(view.rows[0]?.totalObjectives).toBe(10)
    expect(view.rows[0]?.trialScore).toBeNull()
    expect(view.rows[2]?.lastCompletedAt).toBe('2026-09-01T10:00:00Z')
  })

  it('ties share a place under SharedPlace and split by time under ByTime', () => {
    const shared = buildBoardView(board(), ACTIVE_SCOPE)
    const byTime = buildBoardView(
      board({ event: { ...board().event!, tieBreakMode: 'ByTime' } }),
      ACTIVE_SCOPE,
    )

    expect(shared.rows.map((r) => r.rank)).toEqual([1, 2, 2])
    // gus reached 10 at 08:00, mira at 10:00 — first to the score wins.
    expect(byTime.rows.map((r) => [r.userId, r.rank])).toEqual([
      [STREAMER, 1],
      ['gus', 2],
      ['mira', 3],
    ])
  })

  it('a picked game reports that game alone, its trial included, and marks a finished run', () => {
    const view = buildBoardView(board(), { kind: 'game', eventGameId: DS3 })

    expect(view.game?.name).toBe('Dark Souls III')
    const mira = view.rows.find((r) => r.userId === 'mira')!
    expect(mira.score).toBe(20)
    expect(mira.totalObjectives).toBe(4)
    expect(mira.isFinished).toBe(false)
    const streamer = view.rows.find((r) => r.userId === STREAMER)!
    expect(streamer.score).toBe(0)
    expect(streamer.trialScore).toBe(7)
  })

  it('a finished game counts failures towards completion', () => {
    const view = buildBoardView(
      board({
        games: [
          {
            eventGameId: ER,
            name: 'Elden Ring',
            isEnabled: true,
            sortOrder: 0,
            totalObjectives: 5,
          },
        ],
      }),
      ACTIVE_SCOPE,
    )

    expect(view.rows.find((r) => r.userId === STREAMER)?.isFinished).toBe(true)
  })

  it('hides trial figures and the channel highlight when the settings say so', () => {
    const b = board()
    const view = buildBoardView(
      {
        ...b,
        settings: { ...b.settings, showTrialProgress: false, highlightChannelCompetitor: false },
      },
      ALL_SCOPE,
    )

    expect(view.rows.every((r) => r.trialScore === null)).toBe(true)
    expect(view.rows.every((r) => !r.isChannelCompetitor)).toBe(true)
  })

  it('a scope naming no game yields no rows', () => {
    const noActive = board({ event: { ...board().event!, activeEventGameId: null } })

    expect(buildBoardView(noActive, ACTIVE_SCOPE).rows).toEqual([])
    expect(buildBoardView(board(), { kind: 'game', eventGameId: 'nope' }).rows).toEqual([])
  })
})

describe('initialScope', () => {
  const games = board().games

  it('follows the saved default', () => {
    expect(initialScope({ ...board().settings, defaultScope: 'AllGames' }, games)).toEqual(
      ALL_SCOPE,
    )
    expect(initialScope({ ...board().settings, defaultScope: 'ActiveGame' }, games)).toEqual(
      ACTIVE_SCOPE,
    )
    expect(
      initialScope(
        { ...board().settings, defaultScope: 'PinnedGame', pinnedEventGameId: DS3 },
        games,
      ),
    ).toEqual({ kind: 'game', eventGameId: DS3 })
  })

  it('a pinned game the event no longer has falls back to the active game', () => {
    expect(
      initialScope(
        { ...board().settings, defaultScope: 'PinnedGame', pinnedEventGameId: 'gone' },
        games,
      ),
    ).toEqual(ACTIVE_SCOPE)
  })
})

describe('scope helpers', () => {
  it('sameScope compares game ids only for game scopes', () => {
    expect(sameScope(ALL_SCOPE, { kind: 'all' })).toBe(true)
    expect(sameScope({ kind: 'game', eventGameId: ER }, { kind: 'game', eventGameId: ER })).toBe(
      true,
    )
    expect(sameScope({ kind: 'game', eventGameId: ER }, { kind: 'game', eventGameId: DS3 })).toBe(
      false,
    )
    expect(sameScope(ACTIVE_SCOPE, ALL_SCOPE)).toBe(false)
  })

  it('scopedGame resolves the active game from the event', () => {
    expect(scopedGame(ACTIVE_SCOPE, board())?.eventGameId).toBe(ER)
    expect(scopedGame(ALL_SCOPE, board())).toBeNull()
  })
})
