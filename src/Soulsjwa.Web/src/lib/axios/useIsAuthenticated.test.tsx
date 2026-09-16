import { describe, it, expect, beforeEach, afterEach } from 'vitest'
import { render, screen, act, cleanup } from '@testing-library/react'
import { useIsAuthenticated } from './useIsAuthenticated'
import { tokenStore } from './tokenStore'

function makeJwt(expSecondsFromNow: number): string {
  const exp = Math.floor(Date.now() / 1000) + expSecondsFromNow
  const header = btoa(JSON.stringify({ alg: 'HS256', typ: 'JWT' }))
  const payload = btoa(JSON.stringify({ sub: 'u', exp, iat: 0 }))
  return `${header}.${payload}.sig`
}

const Probe = () => {
  const isAuthenticated = useIsAuthenticated()
  return <div>{isAuthenticated ? 'authenticated' : 'anonymous'}</div>
}

describe('useIsAuthenticated', () => {
  beforeEach(() => {
    tokenStore.clearAccessToken()
  })

  afterEach(() => {
    cleanup()
  })

  it('reflects the initial token state on mount', () => {
    tokenStore.setAccessToken(makeJwt(60))
    render(<Probe />)
    expect(screen.getByText('authenticated')).toBeInTheDocument()
  })

  it('re-renders when the token is set outside React, with no other trigger', () => {
    render(<Probe />)
    expect(screen.getByText('anonymous')).toBeInTheDocument()

    act(() => {
      tokenStore.setAccessToken(makeJwt(60))
    })

    expect(screen.getByText('authenticated')).toBeInTheDocument()
  })

  it('re-renders back to false when the token is cleared', () => {
    tokenStore.setAccessToken(makeJwt(60))
    render(<Probe />)
    expect(screen.getByText('authenticated')).toBeInTheDocument()

    act(() => {
      tokenStore.clearAccessToken()
    })

    expect(screen.getByText('anonymous')).toBeInTheDocument()
  })
})
