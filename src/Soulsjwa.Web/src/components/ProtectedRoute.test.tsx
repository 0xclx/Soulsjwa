import { describe, it, expect, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter, Routes, Route } from 'react-router-dom'
import { ProtectedRoute } from './ProtectedRoute'
import { tokenStore } from '../lib/axios'

function renderAt(initialEntry: string) {
  return render(
    <MemoryRouter initialEntries={[initialEntry]}>
      <Routes>
        <Route path="/" element={<div>home</div>} />
        <Route
          path="/protected"
          element={
            <ProtectedRoute>
              <div>secret</div>
            </ProtectedRoute>
          }
        />
      </Routes>
    </MemoryRouter>,
  )
}

// Build a JWT-shaped string with the given exp offset (seconds from now)
function makeJwt(expSecondsFromNow: number): string {
  const exp = Math.floor(Date.now() / 1000) + expSecondsFromNow
  const header = btoa(JSON.stringify({ alg: 'HS256', typ: 'JWT' }))
  const payload = btoa(JSON.stringify({ sub: 'u', exp, iat: 0 }))
  return `${header}.${payload}.sig`
}

describe('<ProtectedRoute />', () => {
  beforeEach(() => {
    tokenStore.clearAccessToken()
  })

  it('redirects to / when no token is present', () => {
    renderAt('/protected')
    expect(screen.getByText('home')).toBeInTheDocument()
    expect(screen.queryByText('secret')).not.toBeInTheDocument()
  })

  it('renders children when an access token is present', () => {
    tokenStore.setAccessToken(makeJwt(60))
    renderAt('/protected')
    expect(screen.getByText('secret')).toBeInTheDocument()
  })
})
