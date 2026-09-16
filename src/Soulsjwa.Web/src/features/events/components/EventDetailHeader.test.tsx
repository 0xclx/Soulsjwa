import { MemoryRouter } from 'react-router-dom'
import { cleanup, render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { EventDetailHeader } from './EventDetailHeader'
import type { EventResponse } from '../../../types'

const makeEvent = (over: Partial<EventResponse> = {}): EventResponse => ({
  id: 'e1',
  name: 'Event',
  urlAlias: null,
  description: '',
  createdById: 'owner',
  isArchived: false,
  isStarted: false,
  isFeatured: false,
  allowTrialRuns: true,
  tieBreakMode: 'ByTime',
  createdAt: '2024-01-01T00:00:00Z',
  updatedAt: '2024-01-01T00:00:00Z',
  competitors: [],
  games: [],
  ...over,
})

const baseProps = {
  isEditing: false,
  startStopPending: false,
  archivePending: false,
  unarchivePending: false,
  featurePending: false,
  duplicatePending: false,
  onStartStop: vi.fn(),
  onEdit: vi.fn(),
  onArchive: vi.fn(),
  onUnarchive: vi.fn(),
  onFeatureToggle: vi.fn(),
  onDuplicate: vi.fn(),
}

const renderHeader = (event: EventResponse, canManage: boolean, isAdmin: boolean) =>
  render(
    <MemoryRouter>
      <EventDetailHeader event={event} canManage={canManage} isAdmin={isAdmin} {...baseProps} />
    </MemoryRouter>,
  )

// Renders standalone (unmounting whatever renderHeader left behind first) so
// successive calls within one test never accumulate duplicate elements.
const renderHeaderAlone = (event: EventResponse, canManage: boolean, isAdmin: boolean) => {
  cleanup()
  return renderHeader(event, canManage, isAdmin)
}

describe('EventDetailHeader', () => {
  it('shows start/stop, edit, and archive for a non-creator admin', () => {
    renderHeader(makeEvent({ createdById: 'owner' }), true, true)

    expect(screen.getByRole('button', { name: /start event/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /edit/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /archive/i })).toBeInTheDocument()
  })

  it('shows unarchive for a non-creator admin on an archived event', () => {
    renderHeader(makeEvent({ createdById: 'owner', isArchived: true }), true, true)

    expect(screen.getByRole('button', { name: /unarchive/i })).toBeInTheDocument()
  })

  it('hides management actions for a plain competitor', () => {
    renderHeader(makeEvent({ createdById: 'owner' }), false, false)

    expect(screen.queryByRole('button', { name: /start event/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /^edit$/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /archive/i })).not.toBeInTheDocument()
  })

  it('gates feature toggle on isAdmin alone, not canManage', () => {
    renderHeader(makeEvent({ createdById: 'creator' }), true, false)
    expect(screen.queryByRole('button', { name: /feature event/i })).not.toBeInTheDocument()

    renderHeader(makeEvent({ createdById: 'creator' }), true, true)
    expect(screen.getByRole('button', { name: /feature event/i })).toBeInTheDocument()
  })

  it('gates duplicate on isAdmin alone, not canManage, and shows it on an archived event too', () => {
    renderHeaderAlone(makeEvent({ createdById: 'creator' }), true, false)
    expect(screen.queryByRole('button', { name: /duplicate/i })).not.toBeInTheDocument()

    renderHeaderAlone(makeEvent({ createdById: 'creator' }), true, true)
    expect(screen.getByRole('button', { name: /duplicate/i })).toBeInTheDocument()

    renderHeaderAlone(makeEvent({ createdById: 'creator', isArchived: true }), true, true)
    expect(screen.getByRole('button', { name: /duplicate/i })).toBeInTheDocument()
  })
})
