import { describe, it, expect, beforeEach, vi } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter, Routes, Route } from 'react-router-dom'
import { AuthCallbackPage } from './AuthCallbackPage'

const mocks = vi.hoisted(() => ({
  isAuthenticated: false,
  start: vi.fn(),
}))

vi.mock('../lib/axios', () => ({
  useIsAuthenticated: () => mocks.isAuthenticated,
  tokenManager: { start: mocks.start },
}))

vi.mock('../lib/react-query', () => ({
  queryClient: { invalidateQueries: vi.fn() },
}))

const renderAt = (initialEntry: string) =>
  render(
    <MemoryRouter initialEntries={[initialEntry]}>
      <Routes>
        <Route path="/" element={<div>home</div>} />
        <Route path="/auth/callback" element={<AuthCallbackPage />} />
      </Routes>
    </MemoryRouter>,
  )

describe('<AuthCallbackPage />', () => {
  beforeEach(() => {
    mocks.isAuthenticated = false
    mocks.start.mockClear()
  })

  it('shows the allowlist warning without touching auth state when blocked', async () => {
    renderAt('/auth/callback?error=not_allowlisted&login=someone')

    expect(await screen.findByText(/isn't on the allowlist for this server/i)).toBeInTheDocument()
    expect(mocks.start).not.toHaveBeenCalled()
  })

  it('shows a cancelled notice without touching auth state when the user denied on Twitch', async () => {
    renderAt('/auth/callback?error=access_denied')

    expect(await screen.findByText(/sign-in was cancelled on twitch/i)).toBeInTheDocument()
    expect(mocks.start).not.toHaveBeenCalled()
  })

  it('starts the token manager and redirects home when bootstrap already authenticated', async () => {
    mocks.isAuthenticated = true
    renderAt('/auth/callback')

    await waitFor(() => expect(screen.getByText('home')).toBeInTheDocument())
    expect(mocks.start).toHaveBeenCalledTimes(1)
  })

  it('shows a failure message when bootstrap did not authenticate', async () => {
    mocks.isAuthenticated = false
    renderAt('/auth/callback')

    expect(await screen.findByText(/login failed/i)).toBeInTheDocument()
    expect(mocks.start).not.toHaveBeenCalled()
  })
})
