import { render, screen } from '@testing-library/react'
import { describe, expect, it, vi, beforeEach } from 'vitest'
import { RulesPage } from './RulesPage'
import type { EventResponse, EventRules, User } from '../types'

const FEATURED_EVENT = { id: 'event-1', name: 'Featured Event', createdById: 'owner-1' } as Pick<
  EventResponse,
  'id' | 'name' | 'createdById'
> as EventResponse

const mocks = vi.hoisted(() => ({
  featuredEvent: undefined as EventResponse | undefined,
  featuredLoading: false,
  featuredError: false,
  currentUser: undefined as User | undefined,
  rules: { content: null, updatedAt: null } as EventRules,
}))

vi.mock('../features/events/hooks/useFeaturedEvent', () => ({
  useFeaturedEvent: () => ({
    data: mocks.featuredEvent,
    isLoading: mocks.featuredLoading,
    isError: mocks.featuredError,
  }),
}))

vi.mock('../features/users/hooks/useCurrentUser', () => ({
  useCurrentUser: () => ({ data: mocks.currentUser }),
}))

vi.mock('../features/events/hooks/useEventRules', () => ({
  useEventRules: () => ({ data: mocks.rules, isLoading: false, isError: false }),
}))

vi.mock('../features/events/hooks/useUpdateEventRules', () => ({
  useUpdateEventRules: () => ({ mutate: vi.fn(), isPending: false }),
}))

describe('RulesPage', () => {
  beforeEach(() => {
    mocks.featuredEvent = undefined
    mocks.featuredLoading = false
    mocks.featuredError = false
    mocks.currentUser = undefined
    mocks.rules = { content: null, updatedAt: null }
  })

  it('shows an empty state when there is no featured event', () => {
    render(<RulesPage />)
    expect(screen.getByText('No rules to show')).toBeInTheDocument()
  })

  it('renders just the rules panel for the featured event, with no event chrome', () => {
    mocks.featuredEvent = FEATURED_EVENT
    mocks.rules = {
      content: '# Ground rules\n\nNo item duping.',
      updatedAt: '2026-01-01T00:00:00Z',
    }
    render(<RulesPage />)
    expect(screen.getByRole('heading', { name: 'Ground rules' })).toBeInTheDocument()
    expect(screen.queryByRole('tablist')).not.toBeInTheDocument()
  })
})
