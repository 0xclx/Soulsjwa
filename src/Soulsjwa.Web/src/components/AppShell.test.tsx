import { describe, it, expect, beforeEach, vi } from 'vitest'
import { cleanup, render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { ThemeModeProvider } from '../theme/ThemeModeProvider'
import { AppShell } from './AppShell'
import { tokenStore, emitAuthRefreshFailed } from '../lib/axios'
import { SCOREBOARD_CACHE_NAME } from '../lib/serviceWorkerCacheNames'
import type { EventResponse, EventRules } from '../types'

const mocks = vi.hoisted(() => ({
  featuredEvent: undefined as EventResponse | undefined,
  featuredRules: undefined as EventRules | undefined,
}))

vi.mock('../features/events/hooks/useFeaturedEvent', () => ({
  useFeaturedEvent: () => ({ data: mocks.featuredEvent }),
}))

vi.mock('../features/events/hooks/useEventRules', () => ({
  useEventRules: () => ({ data: mocks.featuredRules }),
}))

// Build a JWT-shaped string with the given exp offset (seconds from now)
function makeJwt(expSecondsFromNow: number): string {
  const exp = Math.floor(Date.now() / 1000) + expSecondsFromNow
  const header = btoa(JSON.stringify({ alg: 'HS256', typ: 'JWT' }))
  const payload = btoa(JSON.stringify({ sub: 'u', exp, iat: 0 }))
  return `${header}.${payload}.sig`
}

const renderLayout = (initialPath = '/') => {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <ThemeModeProvider>
        <MemoryRouter initialEntries={[initialPath]}>
          <Routes>
            <Route element={<AppShell />}>
              <Route path="/" element={<div>home content</div>} />
              <Route path="/events" element={<div>events content</div>} />
              <Route path="/events/:id" element={<div>event content</div>} />
              <Route path="/profile" element={<div>profile content</div>} />
            </Route>
          </Routes>
        </MemoryRouter>
      </ThemeModeProvider>
    </QueryClientProvider>,
  )
}

const FEATURED_EVENT: EventResponse = {
  id: 'event-1',
  name: 'Featured Event',
  urlAlias: 'featured-event',
  description: '',
  createdById: 'user-1',
  isArchived: false,
  isStarted: true,
  isFeatured: true,
  allowTrialRuns: true,
  tieBreakMode: 'ByTime',
  createdAt: '2026-01-01T00:00:00Z',
  updatedAt: '2026-01-01T00:00:00Z',
  competitors: [],
  games: [],
}

describe('<AppShell />', () => {
  beforeEach(() => {
    window.localStorage.clear()
    tokenStore.clearAccessToken()
    mocks.featuredEvent = undefined
    mocks.featuredRules = undefined
  })

  it('renders the brand, primary navigation, theme toggle, and outlet', () => {
    renderLayout('/')

    expect(screen.getByRole('link', { name: 'Soulsjwa' })).toBeInTheDocument()
    expect(screen.getByRole('navigation', { name: /primary/i })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /skip to main content/i })).toHaveAttribute(
      'href',
      '#main-content',
    )
    expect(screen.getByRole('main')).toHaveAttribute('id', 'main-content')
    expect(screen.getByRole('link', { name: 'Home' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Events' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /switch to/i })).toBeInTheDocument()
    expect(screen.getByText('home content')).toBeInTheDocument()
  })

  it('hides the profile link when the user is not authenticated', () => {
    renderLayout('/')
    expect(screen.queryByRole('link', { name: 'Profile' })).not.toBeInTheDocument()
  })

  it('hides the My Events link for an anonymous visitor', () => {
    renderLayout('/')
    expect(screen.queryByRole('link', { name: 'My Events' })).not.toBeInTheDocument()
  })

  it('shows the My Events link for an authenticated visitor', () => {
    tokenStore.setAccessToken(makeJwt(60))
    renderLayout('/')
    expect(screen.getByRole('link', { name: 'My Events' })).toHaveAttribute('href', '/my-events')
  })

  it('offers the Twitch sign-in consent notice to anonymous visitors only', () => {
    renderLayout('/')
    expect(
      screen.getByRole('button', { name: /what we store when you sign in/i }),
    ).toBeInTheDocument()

    cleanup()
    tokenStore.setAccessToken(makeJwt(60))
    renderLayout('/')
    expect(
      screen.queryByRole('button', { name: /what we store when you sign in/i }),
    ).not.toBeInTheDocument()
  })

  it('marks the active route with aria-current="page"', () => {
    renderLayout('/events')
    const eventsLink = screen.getByRole('link', { name: 'Events' })
    expect(eventsLink).toHaveAttribute('aria-current', 'page')
    const homeLink = screen.getByRole('link', { name: 'Home' })
    expect(homeLink).not.toHaveAttribute('aria-current')
  })

  it('marks a global destination active for its nested routes', () => {
    renderLayout('/events/event-id')
    expect(screen.getByRole('link', { name: 'Events' })).toHaveAttribute('aria-current', 'page')
  })

  it('omits the Rules nav entry when no event is featured', () => {
    renderLayout('/')
    expect(screen.queryByRole('link', { name: 'Rules' })).not.toBeInTheDocument()
  })

  it('omits the Rules nav entry when the featured event has no rules content', () => {
    mocks.featuredEvent = FEATURED_EVENT
    mocks.featuredRules = { content: null, updatedAt: null }
    renderLayout('/')
    expect(screen.queryByRole('link', { name: 'Rules' })).not.toBeInTheDocument()
  })

  it('shows a Rules nav entry linking to the featured event when it has published rules', () => {
    mocks.featuredEvent = FEATURED_EVENT
    mocks.featuredRules = { content: 'No item duping.', updatedAt: '2026-01-01T00:00:00Z' }
    renderLayout('/')
    expect(screen.getByRole('link', { name: 'Rules' })).toHaveAttribute('href', '/rules')
  })

  it('clears the scoreboard cache when the session ends via a refresh failure', () => {
    const deleteMock = vi.fn().mockResolvedValue(true)
    vi.stubGlobal('caches', { delete: deleteMock })

    renderLayout('/')
    emitAuthRefreshFailed()

    expect(deleteMock).toHaveBeenCalledWith(SCOREBOARD_CACHE_NAME)

    vi.unstubAllGlobals()
  })
})
