import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { describe, expect, it, vi } from 'vitest'
import { EventOverviewSection } from './EventOverviewSection'
import type { EventResponse } from '../../../types'

const viewProps = vi.hoisted(() => ({ last: undefined as Record<string, unknown> | undefined }))

vi.mock('./scoreboard/EventScoreboardView', () => ({
  EventScoreboardView: (props: Record<string, unknown>) => {
    viewProps.last = props
    return <div data-testid="scoreboard-view" />
  },
}))

const event = { id: 'event-1', name: 'Lordran Relay' } as EventResponse

describe('EventOverviewSection', () => {
  it('renders the full scoreboard view, not a compact preview', () => {
    render(
      <MemoryRouter>
        <EventOverviewSection
          eventId="event-1"
          eventUrlIdentifier="lordran"
          event={event}
          currentUser={undefined}
        />
      </MemoryRouter>,
    )

    expect(screen.getByTestId('scoreboard-view')).toBeInTheDocument()
    expect(viewProps.last).toMatchObject({ eventId: 'event-1', event, currentUser: undefined })
  })

  it('links to the full scoreboard tab by the event URL identifier', () => {
    render(
      <MemoryRouter>
        <EventOverviewSection
          eventId="event-1"
          eventUrlIdentifier="lordran"
          event={event}
          currentUser={undefined}
        />
      </MemoryRouter>,
    )

    expect(screen.getByRole('link', { name: 'Full scoreboard' })).toHaveAttribute(
      'href',
      '/events/lordran/scoreboard',
    )
  })
})
