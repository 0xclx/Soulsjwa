import { describe, it, expect, beforeEach, vi } from 'vitest'
import { tokenStore } from './tokenStore'

// A test JWT: header.{"sub":"u","exp":<future>,"iat":0}.signature
function makeJwt(expSecondsFromNow: number): string {
  const exp = Math.floor(Date.now() / 1000) + expSecondsFromNow
  const header = btoa(JSON.stringify({ alg: 'HS256', typ: 'JWT' }))
  const payload = btoa(JSON.stringify({ sub: 'user-1', exp, iat: 0 }))
  return `${header}.${payload}.signature`
}

describe('tokenStore', () => {
  beforeEach(() => {
    tokenStore.clearAccessToken()
  })

  it('starts unauthenticated', () => {
    expect(tokenStore.isAuthenticated()).toBe(false)
    expect(tokenStore.getAccessToken()).toBeNull()
    expect(tokenStore.msUntilAccessTokenExpiry()).toBe(0)
  })

  it('reports authenticated after setAccessToken', () => {
    tokenStore.setAccessToken(makeJwt(60))
    expect(tokenStore.isAuthenticated()).toBe(true)
    expect(tokenStore.getAccessToken()).not.toBeNull()
  })

  it('clearAccessToken resets state', () => {
    tokenStore.setAccessToken(makeJwt(60))
    tokenStore.clearAccessToken()
    expect(tokenStore.isAuthenticated()).toBe(false)
    expect(tokenStore.getAccessToken()).toBeNull()
  })

  it('msUntilAccessTokenExpiry is positive for a future-expiry token', () => {
    tokenStore.setAccessToken(makeJwt(120))
    const ms = tokenStore.msUntilAccessTokenExpiry()
    expect(ms).toBeGreaterThan(60_000)
    expect(ms).toBeLessThanOrEqual(120_000)
  })

  it('msUntilAccessTokenExpiry returns 0 for an expired token', () => {
    tokenStore.setAccessToken(makeJwt(-60))
    expect(tokenStore.msUntilAccessTokenExpiry()).toBe(0)
  })

  it('msUntilAccessTokenExpiry returns 0 for a non-decodable token', () => {
    tokenStore.setAccessToken('not-a-jwt')
    expect(tokenStore.msUntilAccessTokenExpiry()).toBe(0)
  })

  it('notifies subscribers when the token is set or cleared', () => {
    const listener = vi.fn()
    const unsubscribe = tokenStore.subscribe(listener)

    tokenStore.setAccessToken(makeJwt(60))
    expect(listener).toHaveBeenCalledTimes(1)

    tokenStore.clearAccessToken()
    expect(listener).toHaveBeenCalledTimes(2)

    unsubscribe()
    tokenStore.setAccessToken(makeJwt(60))
    expect(listener).toHaveBeenCalledTimes(2)
  })
})
