import { render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { EventOverviewPage } from './EventOverviewPage'
import { useEventScores } from '../features/events/hooks/useEventScores'
import type { EventResponse } from '../types'

const event = { id: 'event-1', isStarted: true } as EventResponse

vi.mock('../features/events/hooks/useEventRoute', () => ({
  useEventRoute: () => ({
    eventId: 'event-1',
    eventUrlIdentifier: 'lordran',
    event,
    currentUser: undefined,
  }),
}))

vi.mock('../features/events/hooks/useEventScores', () => ({ useEventScores: vi.fn() }))

vi.mock('../features/events/components/EventOverviewSection', () => ({
  EventOverviewSection: ({ eventUrlIdentifier }: { eventUrlIdentifier: string }) => (
    <div data-testid="overview">{eventUrlIdentifier}</div>
  ),
}))

describe('EventOverviewPage', () => {
  it('renders the overview without fetching /scores', () => {
    render(<EventOverviewPage />)

    expect(screen.getByTestId('overview')).toHaveTextContent('lordran')
    expect(useEventScores).not.toHaveBeenCalled()
  })
})
