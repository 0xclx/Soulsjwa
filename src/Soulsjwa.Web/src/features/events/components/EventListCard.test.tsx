import { describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { EventListCard } from './EventListCard'
import type { EventListItem } from '../../../types'

const makeEvent = (over: Partial<EventListItem> = {}): EventListItem => ({
  id: 'e1',
  name: 'Summer Marathon',
  urlAlias: null,
  description: 'A long description',
  createdById: 'owner',
  isArchived: false,
  isStarted: false,
  isFeatured: false,
  allowTrialRuns: true,
  tieBreakMode: 'ByTime',
  createdAt: '2024-01-01T00:00:00Z',
  updatedAt: '2024-01-01T00:00:00Z',
  competitorCount: 0,
  gameCount: 0,
  ...over,
})

const renderCard = (props: Partial<React.ComponentProps<typeof EventListCard>> = {}) =>
  render(
    <MemoryRouter>
      <EventListCard
        event={makeEvent()}
        isAdmin={false}
        onUnarchive={() => {}}
        unarchivePending={false}
        onDuplicate={() => {}}
        duplicatePending={false}
        {...props}
      />
    </MemoryRouter>,
  )

describe('<EventListCard />', () => {
  it('renders the event name and a status chip', () => {
    renderCard({ event: makeEvent({ isStarted: true }) })
    expect(screen.getByRole('heading', { name: 'Summer Marathon' })).toBeInTheDocument()
    expect(screen.getByText('Live')).toBeInTheDocument()
  })

  it('links to the event detail page', () => {
    renderCard()
    expect(screen.getByRole('link')).toHaveAttribute('href', '/events/e1')
  })

  it('prefers the URL alias for the event detail link', () => {
    renderCard({ event: makeEvent({ urlAlias: 'summer-marathon' }) })
    expect(screen.getByRole('link')).toHaveAttribute('href', '/events/summer-marathon')
  })

  it('shows an admin unarchive action only for archived events and does not navigate on click', async () => {
    const onUnarchive = vi.fn()
    renderCard({ event: makeEvent({ isArchived: true }), isAdmin: true, onUnarchive })
    const button = screen.getByRole('button', { name: 'Unarchive' })
    await userEvent.click(button)
    expect(onUnarchive).toHaveBeenCalledWith('e1')
  })

  it('hides the unarchive action for non-admins', () => {
    renderCard({ event: makeEvent({ isArchived: true }), isAdmin: false })
    expect(screen.queryByRole('button', { name: 'Unarchive' })).not.toBeInTheDocument()
  })

  it('shows an admin duplicate action for both archived and active events, without navigating on click', async () => {
    const onDuplicate = vi.fn()
    renderCard({ event: makeEvent({ isArchived: true }), isAdmin: true, onDuplicate })
    await userEvent.click(screen.getByRole('button', { name: 'Duplicate' }))
    expect(onDuplicate).toHaveBeenCalledWith('e1')
  })

  it('hides the duplicate action for non-admins', () => {
    renderCard({ isAdmin: false })
    expect(screen.queryByRole('button', { name: 'Duplicate' })).not.toBeInTheDocument()
  })
})
