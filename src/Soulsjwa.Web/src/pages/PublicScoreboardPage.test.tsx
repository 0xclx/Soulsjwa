import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { describe, expect, it, vi, beforeEach } from 'vitest'
import { PublicScoreboardPage } from './PublicScoreboardPage'
import type { EventResponse } from '../types'

const mocks = vi.hoisted(() => ({
  data: undefined as EventResponse | undefined,
  isLoading: false,
  isError: false,
}))

vi.mock('../features/events/hooks/useEvent', () => ({
  useEvent: () => ({ data: mocks.data, isLoading: mocks.isLoading, isError: mocks.isError }),
}))

vi.mock('../features/events/components/scoreboard/EventScoreboardView', () => ({
  EventScoreboardView: ({ eventId }: { eventId: string }) => (
    <div data-testid="scoreboard-view">scoreboard for {eventId}</div>
  ),
}))

const renderAt = (path: string) =>
  render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route path="/scoreboard/:eventIdentifier" element={<PublicScoreboardPage />} />
      </Routes>
    </MemoryRouter>,
  )

describe('PublicScoreboardPage', () => {
  beforeEach(() => {
    mocks.data = { id: 'event-1', name: 'Test event' } as unknown as EventResponse
    mocks.isLoading = false
    mocks.isError = false
  })

  it('resolves by event id or alias and renders that event’s scoreboard', () => {
    renderAt('/scoreboard/some-alias')
    expect(screen.getByText('Test event')).toBeInTheDocument()
    expect(screen.getByText('scoreboard for event-1')).toBeInTheDocument()
  })

  it('shows a loading state while resolving the event', () => {
    mocks.isLoading = true
    mocks.data = undefined
    renderAt('/scoreboard/event-1')
    expect(screen.getByText(/loading scoreboard/i)).toBeInTheDocument()
  })

  it('shows an error state when the event cannot be found', () => {
    mocks.isError = true
    mocks.data = undefined
    renderAt('/scoreboard/unknown')
    expect(screen.getByText(/event not found/i)).toBeInTheDocument()
  })

  it('renders no app chrome — just the heading and the scoreboard', () => {
    renderAt('/scoreboard/event-1')
    expect(screen.queryByRole('navigation')).not.toBeInTheDocument()
    expect(screen.queryByRole('contentinfo')).not.toBeInTheDocument()
  })
})
