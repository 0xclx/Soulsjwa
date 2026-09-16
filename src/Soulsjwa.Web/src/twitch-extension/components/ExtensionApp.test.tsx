import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { act, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import type {
  TwitchExtensionCompetitorDetail,
  TwitchExtensionConfiguration,
  TwitchExtensionScoreboard,
} from '../../types/twitchExtension'
import { ExtensionApp, NO_EVENT_TITLE, WAITING_FOR_TWITCH_LABEL } from './ExtensionApp'
import { ALL_GAMES_LABEL, PICK_GAME_LABEL } from './ScopeSwitcher'
import { YOU_TAG } from './CompetitorRow'
import { SHOW_OBJECTIVES_LABEL } from './CompetitorDetail'
import { FOLLOW_FEATURED_LABEL, SAVE_LABEL, SAVED_LABEL, SIGN_IN_NOTICE } from './SettingsForm'

const CHANNEL_ID = '1234'
const TOKEN = 'twitch-jwt'
const ER = 'game-er'
const SEK = 'game-sek'
const STREAMER_ID = 'user-kai'

type Listener = (target: string, contentType: string, message: string) => void

/** A stand-in for the helper Twitch injects, driven from the tests. */
function installTwitchHelper(role: 'viewer' | 'broadcaster') {
  const listeners: Listener[] = []
  let authorize: ((auth: Twitch.ExtensionAuth) => void) | null = null
  let context:
    | ((
        ctx: Twitch.ExtensionContext,
        changed: ReadonlyArray<keyof Twitch.ExtensionContext>,
      ) => void)
    | null = null
  window.Twitch = {
    ext: {
      onAuthorized: (cb) => {
        authorize = cb
      },
      onContext: (cb) => {
        context = cb
      },
      onVisibilityChanged: () => {},
      listen: (_target, cb) => {
        listeners.push(cb)
      },
      unlisten: () => {},
    },
  }
  return {
    authorize: () =>
      act(() =>
        authorize?.({
          token: TOKEN,
          channelId: CHANNEL_ID,
          clientId: 'cid',
          userId: role === 'broadcaster' ? 'U1' : 'A1',
        }),
      ),
    setTheme: (theme: Twitch.ExtensionTheme) => act(() => context?.({ theme }, ['theme'])),
    push: (message: object) =>
      act(() =>
        listeners.forEach((cb) => cb('global', 'application/json', JSON.stringify(message))),
      ),
  }
}

const board: TwitchExtensionScoreboard = {
  channelId: CHANNEL_ID,
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
    id: 'event-1',
    name: 'Autumn Ashes Cup',
    urlAlias: 'autumn',
    isStarted: true,
    isFeatured: true,
    tieBreakMode: 'SharedPlace',
    activeEventGameId: ER,
    source: 'Featured',
  },
  channelCompetitorUserId: STREAMER_ID,
  games: [
    { eventGameId: ER, name: 'Elden Ring', isEnabled: true, sortOrder: 0, totalObjectives: 3 },
    { eventGameId: SEK, name: 'Sekiro', isEnabled: false, sortOrder: 1, totalObjectives: 2 },
  ],
  entries: [
    {
      userId: 'user-mira',
      displayName: 'Mira_Rune',
      twitchLogin: 'mira_rune',
      profileImageUrl: null,
      isLive: true,
      rank: 1,
      totalScore: 40,
      completedCount: 3,
      failedCount: 0,
      isFinished: false,
      status: 'Pending',
      lastCompletedAt: '2026-09-01T10:00:00Z',
      totalInGameTimeMs: null,
      games: [
        {
          eventGameId: ER,
          score: 10,
          completedCount: 1,
          failedCount: 0,
          lastCompletedAt: '2026-09-01T09:00:00Z',
          isTrialActive: false,
          trial: null,
        },
        {
          eventGameId: SEK,
          score: 30,
          completedCount: 2,
          failedCount: 0,
          lastCompletedAt: '2026-09-01T10:00:00Z',
          isTrialActive: false,
          trial: null,
        },
      ],
    },
    {
      userId: STREAMER_ID,
      displayName: 'Ashen_Kai',
      twitchLogin: 'ashen_kai',
      profileImageUrl: null,
      isLive: true,
      rank: 2,
      totalScore: 25,
      completedCount: 2,
      failedCount: 0,
      isFinished: false,
      status: 'Pending',
      lastCompletedAt: '2026-09-01T09:30:00Z',
      totalInGameTimeMs: 3_600_000,
      games: [
        {
          eventGameId: ER,
          score: 25,
          completedCount: 2,
          failedCount: 0,
          lastCompletedAt: '2026-09-01T09:30:00Z',
          isTrialActive: false,
          trial: null,
        },
        {
          eventGameId: SEK,
          score: 0,
          completedCount: 0,
          failedCount: 0,
          lastCompletedAt: null,
          isTrialActive: false,
          trial: null,
        },
      ],
    },
  ],
}

const detail: TwitchExtensionCompetitorDetail = {
  userId: STREAMER_ID,
  displayName: 'Ashen_Kai',
  games: [
    {
      eventGameId: ER,
      gameName: 'Elden Ring',
      isEnabled: true,
      isTrialActive: false,
      objectives: [
        {
          objectiveId: 'o1',
          name: 'Defeat Margit',
          score: 10,
          category: 'Limgrave',
          isCompleted: true,
          completedAt: '2026-09-01T09:00:00Z',
          isFailed: false,
          failedAt: null,
          status: 'Completed',
          trial: null,
        },
        {
          objectiveId: 'o2',
          name: 'Defeat Godrick',
          score: 15,
          category: 'Stormveil',
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
}

const configuration: TwitchExtensionConfiguration = {
  channelId: CHANNEL_ID,
  linkedUser: { id: STREAMER_ID, displayName: 'Ashen_Kai', twitchLogin: 'ashen_kai' },
  policy: board.policy,
  settings: board.settings,
  resolvedEvent: board.event,
  events: [
    {
      id: 'event-1',
      name: 'Autumn Ashes Cup',
      isStarted: true,
      isFeatured: true,
      isCompetitor: true,
      games: [{ eventGameId: ER, name: 'Elden Ring', isEnabled: true }],
    },
    {
      id: 'event-2',
      name: 'Winter Bonfire Relay',
      isStarted: false,
      isFeatured: false,
      isCompetitor: false,
      games: [],
    },
  ],
}

const json = (body: unknown, init: ResponseInit = {}) =>
  new Response(JSON.stringify(body), {
    status: 200,
    headers: { 'Content-Type': 'application/json', ETag: '"v1"' },
    ...init,
  })

let fetchMock: ReturnType<typeof vi.fn>

beforeEach(() => {
  fetchMock = vi.fn()
  vi.stubGlobal('fetch', fetchMock)
})

afterEach(() => {
  vi.unstubAllGlobals()
  delete window.Twitch
  delete document.documentElement.dataset.theme
})

describe('ExtensionApp panel', () => {
  it('waits for Twitch, then renders the board with the token Twitch handed over', async () => {
    const twitch = installTwitchHelper('viewer')
    fetchMock.mockResolvedValue(json(board))
    render(<ExtensionApp view="panel" />)

    expect(screen.getByText(WAITING_FOR_TWITCH_LABEL)).toBeInTheDocument()
    await twitch.authorize()

    expect(await screen.findByText('Autumn Ashes Cup')).toBeInTheDocument()
    expect(screen.getByText('Mira_Rune')).toBeInTheDocument()
    expect(screen.getByText(YOU_TAG)).toBeInTheDocument()
    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit]
    expect(url).toBe('/api/v1/twitch-extension/scoreboard')
    expect((init.headers as Record<string, string>).Authorization).toBe(`Bearer ${TOKEN}`)
    expect(screen.getByRole('tab', { name: ALL_GAMES_LABEL })).toHaveAttribute(
      'aria-selected',
      'true',
    )
  })

  it('switches to the active game and re-ranks within it', async () => {
    const twitch = installTwitchHelper('viewer')
    fetchMock.mockResolvedValue(json(board))
    render(<ExtensionApp view="panel" />)
    await twitch.authorize()
    await screen.findByText('Autumn Ashes Cup')

    await userEvent.click(screen.getByRole('tab', { name: /Now: Elden Ring/ }))

    const rows = screen.getAllByRole('listitem')
    expect(rows[0]).toHaveTextContent('Ashen_Kai')
    expect(rows[0]).toHaveTextContent('25')
    expect(rows[1]).toHaveTextContent('Mira_Rune')

    await userEvent.click(screen.getByRole('tab', { name: PICK_GAME_LABEL }))
    await userEvent.click(screen.getByRole('button', { name: /Sekiro/ }))
    expect(screen.getAllByRole('listitem')[0]).toHaveTextContent('Mira_Rune')
  })

  it('expands a row and fetches objectives on demand', async () => {
    const twitch = installTwitchHelper('viewer')
    fetchMock.mockImplementation((url: string) =>
      Promise.resolve(url.includes('/competitors/') ? json(detail) : json(board)),
    )
    render(<ExtensionApp view="panel" />)
    await twitch.authorize()
    await screen.findByText('Autumn Ashes Cup')

    await userEvent.click(screen.getByRole('button', { name: /Ashen_Kai/ }))
    expect(screen.getByText(/IGT 1:00:00/)).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: new RegExp(SHOW_OBJECTIVES_LABEL) }))

    expect(await screen.findByText('Defeat Margit')).toBeInTheDocument()
    expect(screen.getByText('Stormveil')).toBeInTheDocument()
    expect(
      fetchMock.mock.calls.some(([url]) => (url as string).endsWith(`/competitors/${STREAMER_ID}`)),
    ).toBe(true)
  })

  it('refetches when Twitch pushes a change for the shown event', async () => {
    const twitch = installTwitchHelper('viewer')
    fetchMock.mockResolvedValue(json(board))
    render(<ExtensionApp view="panel" />)
    await twitch.authorize()
    await screen.findByText('Autumn Ashes Cup')
    const before = fetchMock.mock.calls.length

    await twitch.push({ type: 'scoreboard', eventId: 'someone-else' })
    expect(fetchMock.mock.calls.length).toBe(before)

    await twitch.push({ type: 'scoreboard', eventId: 'event-1' })
    await waitFor(() => expect(fetchMock.mock.calls.length).toBe(before + 1))
    const [, init] = fetchMock.mock.calls[before] as [string, RequestInit]
    expect((init.headers as Record<string, string>)['If-None-Match']).toBe('"v1"')
  })

  it('shows the empty state when the channel has nothing to show', async () => {
    const twitch = installTwitchHelper('viewer')
    fetchMock.mockResolvedValue(json({ ...board, event: null, games: [], entries: [] }))
    render(<ExtensionApp view="panel" />)
    await twitch.authorize()

    expect(await screen.findByText(NO_EVENT_TITLE)).toBeInTheDocument()
  })

  it('mirrors the Twitch theme onto the document', async () => {
    const twitch = installTwitchHelper('viewer')
    fetchMock.mockResolvedValue(json(board))
    render(<ExtensionApp view="panel" />)
    await twitch.authorize()
    await screen.findByText('Autumn Ashes Cup')

    await twitch.setTheme('light')

    expect(document.documentElement.dataset.theme).toBe('light')
  })
})

describe('ExtensionApp config', () => {
  it('loads the settings and saves a new event', async () => {
    const twitch = installTwitchHelper('broadcaster')
    fetchMock.mockImplementation((_url: string, init?: RequestInit) =>
      Promise.resolve(
        init?.method === 'PUT'
          ? json({ ...configuration, settings: { ...configuration.settings, eventId: 'event-2' } })
          : json(configuration),
      ),
    )
    render(<ExtensionApp view="config" />)
    await twitch.authorize()

    expect(await screen.findByLabelText(new RegExp(FOLLOW_FEATURED_LABEL))).toBeChecked()
    await userEvent.click(screen.getByLabelText(/Winter Bonfire Relay/))
    await userEvent.click(screen.getByRole('button', { name: SAVE_LABEL }))

    expect(await screen.findByText(SAVED_LABEL)).toBeInTheDocument()
    const put = fetchMock.mock.calls.find(
      ([, init]) => (init as RequestInit)?.method === 'PUT',
    ) as [string, RequestInit]
    expect(put[0]).toBe('/api/v1/twitch-extension/configuration')
    expect(JSON.parse(put[1].body as string)).toMatchObject({
      eventId: 'event-2',
      defaultScope: 'AllGames',
      pinnedEventGameId: null,
    })
  })

  it('explains what to do when the broadcaster has no linked account', async () => {
    const twitch = installTwitchHelper('broadcaster')
    fetchMock.mockResolvedValue(json({ ...configuration, linkedUser: null }))
    render(<ExtensionApp view="config" />)
    await twitch.authorize()

    expect(await screen.findByText(new RegExp(SIGN_IN_NOTICE.slice(0, 30)))).toBeInTheDocument()
    expect(screen.getByRole('button', { name: SAVE_LABEL })).toBeDisabled()
  })
})
