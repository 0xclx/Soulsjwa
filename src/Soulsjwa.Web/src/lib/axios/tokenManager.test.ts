import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

const { postMock } = vi.hoisted(() => ({ postMock: vi.fn() }))

vi.mock('axios', () => ({
  default: { post: postMock },
}))

// A test JWT: header.{"sub":"u","exp":<future>,"iat":0}.signature
function makeJwt(expSecondsFromNow: number): string {
  const exp = Math.floor(Date.now() / 1000) + expSecondsFromNow
  const header = btoa(JSON.stringify({ alg: 'HS256', typ: 'JWT' }))
  const payload = btoa(JSON.stringify({ sub: 'user-1', exp, iat: 0 }))
  return `${header}.${payload}.signature`
}

beforeEach(() => {
  vi.useFakeTimers()
  postMock.mockReset()
})

afterEach(async () => {
  const { tokenManager } = await import('./tokenManager')
  tokenManager.stop()
  vi.useRealTimers()
  vi.resetModules()
})

describe('tokenManager', () => {
  it('leaves exactly one live timer after scheduleNext runs twice back-to-back (StrictMode double-invoked bootstrap)', async () => {
    // A fresh, current-time-relative token on every call, like a real
    // server issuing a new fixed-lifetime token per refresh.
    postMock.mockImplementation(() => Promise.resolve({ data: { accessToken: makeJwt(120) } }))
    const { tokenManager } = await import('./tokenManager')

    // React 19 StrictMode double-invokes mount effects, so AppProviders'
    // effect can call bootstrap() twice in quick succession. Both share one
    // network request (refreshSession is single-flight), but each resolution
    // calls scheduleNext() — without the fix, the first call's timer handle
    // is orphaned instead of cleared.
    await Promise.all([tokenManager.bootstrap(), tokenManager.bootstrap()])

    postMock.mockClear()
    // Any orphaned timer from the first scheduleNext() call would fire an
    // extra refresh within [60s, 65s) of now; advance well past that.
    await vi.advanceTimersByTimeAsync(70_000)

    expect(postMock).toHaveBeenCalledTimes(1)
  })
})
