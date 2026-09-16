import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { TwitchExtensionConfiguration } from '../../../types/twitchExtension'
import {
  FOLLOWING_FEATURED_LABEL,
  SAVE_LABEL,
  SAVED_LABEL,
  SHOW_THIS_EVENT_LABEL,
  TWITCH_EXTENSION_HEADING,
  TwitchExtensionSection,
} from './TwitchExtensionSection'

const EVENT_ID = 'event-1'
const OTHER_EVENT_ID = 'event-2'
const CLIENT_ID = 'abc123'

const configuration: TwitchExtensionConfiguration = {
  channelId: '42',
  linkedUser: { id: 'user-1', displayName: 'Ashen_Kai', twitchLogin: 'ashen_kai' },
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
  resolvedEvent: {
    id: OTHER_EVENT_ID,
    name: 'Winter Bonfire Relay',
    urlAlias: null,
    isStarted: true,
    isFeatured: true,
    tieBreakMode: 'SharedPlace',
    activeEventGameId: null,
    source: 'Featured',
  },
  events: [],
}

const status = vi.fn()
const mine = vi.fn()
const mutateAsync = vi.fn()

vi.mock('../hooks/useTwitchExtensionStatus', () => ({
  useTwitchExtensionStatus: () => status(),
}))
vi.mock('../hooks/useMyTwitchExtensionConfiguration', () => ({
  useMyTwitchExtensionConfiguration: () => mine(),
}))
vi.mock('../hooks/useUpdateMyTwitchExtensionConfiguration', () => ({
  useUpdateMyTwitchExtensionConfiguration: () => ({
    mutateAsync,
    isPending: false,
    isError: false,
  }),
}))

beforeEach(() => {
  status.mockReset()
  mine.mockReset()
  mutateAsync.mockReset()
  status.mockReturnValue({ data: { configured: true, clientId: CLIENT_ID } })
  mine.mockReturnValue({ data: configuration, isLoading: false })
  mutateAsync.mockResolvedValue(configuration)
})

const renderSection = () =>
  render(
    <TwitchExtensionSection
      eventId={EVENT_ID}
      eventName="Autumn Ashes Cup"
      games={[{ eventGameId: 'g1', gameName: 'Elden Ring', isEnabled: true }]}
    />,
  )

describe('TwitchExtensionSection', () => {
  it('renders nothing when the server backs no extension', () => {
    status.mockReturnValue({ data: { configured: false, clientId: null } })

    const { container } = renderSection()

    expect(container).toBeEmptyDOMElement()
  })

  it('links to the Twitch install page and reports what the channel shows', () => {
    renderSection()

    expect(screen.getByRole('heading', { name: TWITCH_EXTENSION_HEADING })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /Install on Twitch/ })).toHaveAttribute(
      'href',
      `https://dashboard.twitch.tv/extensions/${CLIENT_ID}`,
    )
    expect(
      screen.getByText(`${FOLLOWING_FEATURED_LABEL} (Winter Bonfire Relay)`),
    ).toBeInTheDocument()
  })

  it('shows this event on the channel and saves the settings row', async () => {
    renderSection()

    await userEvent.click(screen.getByRole('button', { name: SHOW_THIS_EVENT_LABEL }))
    await userEvent.click(screen.getByRole('button', { name: SAVE_LABEL }))

    expect(mutateAsync).toHaveBeenCalledWith({
      eventId: EVENT_ID,
      defaultScope: 'AllGames',
      pinnedEventGameId: null,
      highlightChannelCompetitor: true,
      showTrialProgress: true,
    })
    expect(await screen.findByText(SAVED_LABEL)).toBeInTheDocument()
  })
})
