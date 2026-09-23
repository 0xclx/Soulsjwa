import { render, screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { PreStartOverview } from './PreStartOverview'
import type { EventCompetitor, EventGame, EventResponse, Objective } from '../../../../types'

const objective = (score: number): Objective => ({
  id: `o-${score}-${Math.random()}`,
  name: `Objective ${score}`,
  score,
  isPredefined: false,
})

const game = (overrides: Partial<EventGame> = {}): EventGame => ({
  eventGameId: 'g',
  knownGameId: 1,
  gameName: 'Game',
  knownGameName: 'Game',
  connectorSupported: false,
  isCustomGame: false,
  isEnabled: false,
  objectives: [],
  ...overrides,
})

const competitor = (overrides: Partial<EventCompetitor> = {}): EventCompetitor => ({
  userId: 'u',
  displayName: 'Player',
  joinedAt: '2026-01-01T00:00:00Z',
  isStreamer: false,
  isLive: false,
  moderators: [],
  twitchLogin: 'player',
  profileImageUrl: null,
  ...overrides,
})

const makeEvent = (overrides: Partial<EventResponse> = {}): EventResponse => ({
  id: 'event-1',
  name: 'Lordran Relay',
  urlAlias: null,
  description: '',
  createdById: 'owner',
  isArchived: false,
  isStarted: false,
  isFeatured: false,
  allowTrialRuns: false,
  tieBreakMode: 'ByTime',
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
  competitors: [],
  games: [],
  ...overrides,
})

const gamesSection = () => screen.getByRole('region', { name: 'Games' })
const competitorsSection = () => screen.getByRole('region', { name: 'Competitors' })

describe('PreStartOverview', () => {
  it('lists games in event order with their objective count and total points', () => {
    render(
      <PreStartOverview
        event={makeEvent({
          games: [
            game({
              eventGameId: 'ds1',
              gameName: 'Dark Souls',
              objectives: [objective(10), objective(25)],
            }),
            game({ eventGameId: 'er', gameName: 'Elden Ring', objectives: [objective(100)] }),
          ],
        })}
      />,
    )

    const items = within(gamesSection()).getAllByRole('listitem')
    expect(items).toHaveLength(2)
    expect(items[0]).toHaveTextContent('Dark Souls')
    expect(items[0]).toHaveTextContent('2 objectives')
    expect(items[0]).toHaveTextContent('35 points')
    expect(items[1]).toHaveTextContent('Elden Ring')
    expect(items[1]).toHaveTextContent('1 objective')
    expect(items[1]).toHaveTextContent('100 points')
  })

  it('marks only custom games with a Custom chip', () => {
    render(
      <PreStartOverview
        event={makeEvent({
          games: [
            game({ eventGameId: 'known', gameName: 'Known' }),
            game({ eventGameId: 'custom', gameName: 'Homebrew', isCustomGame: true }),
          ],
        })}
      />,
    )

    const [known, custom] = within(gamesSection()).getAllByRole('listitem')
    expect(within(known!).queryByText('Custom')).toBeNull()
    expect(within(custom!).getByText('Custom')).toBeInTheDocument()
  })

  it('lists competitors sorted by display name', () => {
    render(
      <PreStartOverview
        event={makeEvent({
          competitors: [
            competitor({ userId: 'c', displayName: 'Solaire' }),
            competitor({ userId: 'a', displayName: 'andre' }),
            competitor({ userId: 'b', displayName: 'Lautrec' }),
          ],
        })}
      />,
    )

    const names = within(competitorsSection())
      .getAllByRole('listitem')
      .map((item) => within(item).getByTestId('competitor-name').textContent)
    expect(names).toEqual(['andre', 'Lautrec', 'Solaire'])
  })

  it('shows each competitor with avatar, live status and a Twitch link', () => {
    render(
      <PreStartOverview
        event={makeEvent({
          competitors: [
            competitor({
              userId: 'live',
              displayName: 'Siegmeyer',
              twitchLogin: 'onion_knight',
              profileImageUrl: 'https://example.test/onion.png',
              isLive: true,
            }),
            competitor({ userId: 'off', displayName: 'Patches', twitchLogin: 'patches' }),
          ],
        })}
      />,
    )

    const [patches, siegmeyer] = within(competitorsSection()).getAllByRole('listitem')
    expect(within(siegmeyer!).getByRole('img', { name: 'Siegmeyer' })).toHaveAttribute(
      'src',
      'https://example.test/onion.png',
    )
    expect(within(siegmeyer!).getByRole('img', { name: 'Live' })).toBeInTheDocument()
    expect(within(patches!).getByRole('img', { name: 'Offline' })).toBeInTheDocument()
    expect(
      within(siegmeyer!).getByRole('link', { name: 'Watch Siegmeyer on Twitch' }),
    ).toHaveAttribute('href', 'https://twitch.tv/onion_knight')
  })

  it('says so when there are no games or no competitors', () => {
    render(<PreStartOverview event={makeEvent()} />)

    expect(within(gamesSection()).getByText('No games added yet')).toBeInTheDocument()
    expect(within(competitorsSection()).getByText('No competitors yet')).toBeInTheDocument()
  })
})
