import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

const { postMock } = vi.hoisted(() => ({ postMock: vi.fn() }))

vi.mock('axios', () => ({
  default: { post: postMock },
}))

// A test JWT: header.{"sub":"u","exp":<future>,"iat":0}.signature — the
// cross-tab re-check compares real expiry via jwt-decode, so plain strings
// won't do for that one test.
function makeJwt(expSecondsFromNow: number): string {
  const exp = Math.floor(Date.now() / 1000) + expSecondsFromNow
  const header = btoa(JSON.stringify({ alg: 'HS256', typ: 'JWT' }))
  const payload = btoa(JSON.stringify({ sub: 'user-1', exp, iat: 0 }))
  return `${header}.${payload}.signature`
}

/** In-memory `BroadcastChannel` shared across all instances of the same name,
 * so two "tabs" created via separate module instances can talk to each other. */
class FakeBroadcastChannel {
  static channels = new Map<string, Set<FakeBroadcastChannel>>()
  name: string
  private listeners = new Set<(event: MessageEvent) => void>()

  constructor(name: string) {
    this.name = name
    const set = FakeBroadcastChannel.channels.get(name) ?? new Set()
    set.add(this)
    FakeBroadcastChannel.channels.set(name, set)
  }

  postMessage(data: unknown): void {
    const set = FakeBroadcastChannel.channels.get(this.name)
    if (!set) return
    for (const channel of set) {
      if (channel === this) continue
      const event = { data } as MessageEvent
      // Deliver synchronously (unlike the real cross-process BroadcastChannel)
      // so the "second tab adopts the token" assertion below isn't a race.
      channel.listeners.forEach((listener) => listener(event))
    }
  }

  addEventListener(_type: 'message', listener: (event: MessageEvent) => void): void {
    this.listeners.add(listener)
  }

  removeEventListener(_type: 'message', listener: (event: MessageEvent) => void): void {
    this.listeners.delete(listener)
  }
}

/** In-memory `navigator.locks` that actually serialises callers by lock name,
 * mirroring the mutual exclusion the real Web Locks API provides across tabs. */
const createFakeLockManager = () => {
  const queues = new Map<string, Promise<unknown>>()
  return {
    request: (name: string, callback: () => Promise<unknown>): Promise<unknown> => {
      const previous = queues.get(name) ?? Promise.resolve()
      const settled = previous.then(callback, callback)
      queues.set(
        name,
        settled.catch(() => undefined),
      )
      return settled
    },
  }
}

beforeEach(() => {
  postMock.mockReset()
  FakeBroadcastChannel.channels.clear()
  vi.stubGlobal('BroadcastChannel', FakeBroadcastChannel)
})

afterEach(() => {
  vi.unstubAllGlobals()
  vi.resetModules()
})

describe('refreshSession', () => {
  it('issues exactly one HTTP request for 5 concurrent callers and resolves them all to the same token', async () => {
    postMock.mockResolvedValue({ data: { accessToken: 'token-1' } })
    const { refreshSession } = await import('./refreshSession')

    const results = await Promise.all([
      refreshSession(),
      refreshSession(),
      refreshSession(),
      refreshSession(),
      refreshSession(),
    ])

    expect(postMock).toHaveBeenCalledTimes(1)
    expect(results).toEqual(['token-1', 'token-1', 'token-1', 'token-1', 'token-1'])
  })

  it('a proactive call racing a reactive call issues one request', async () => {
    postMock.mockResolvedValue({ data: { accessToken: 'token-2' } })
    const { refreshSession } = await import('./refreshSession')

    const proactive = refreshSession()
    const reactive = refreshSession()

    await expect(proactive).resolves.toBe('token-2')
    await expect(reactive).resolves.toBe('token-2')
    expect(postMock).toHaveBeenCalledTimes(1)
  })

  it('rejects every concurrent caller when the request fails, without clearing tokenStore itself', async () => {
    postMock.mockRejectedValue(new Error('network error'))
    const { refreshSession } = await import('./refreshSession')
    const { tokenStore } = await import('./tokenStore')

    await expect(Promise.all([refreshSession(), refreshSession()])).rejects.toThrow()
    expect(postMock).toHaveBeenCalledTimes(1)
    // refreshSession() itself is policy-free on failure; callers (apiClient,
    // tokenManager) decide whether to clear the token and notify the UI.
    expect(tokenStore.getAccessToken()).toBeNull()
  })

  it('two tabs sharing a lock and broadcast channel perform one network refresh between them', async () => {
    const fakeLocks = createFakeLockManager()
    vi.stubGlobal('navigator', { ...navigator, locks: fakeLocks })
    const sharedToken = makeJwt(120)
    postMock.mockResolvedValue({ data: { accessToken: sharedToken } })

    vi.resetModules()
    const tabA = await import('./refreshSession')
    const tabATokenStore = (await import('./tokenStore')).tokenStore

    vi.resetModules()
    const tabB = await import('./refreshSession')
    const tabBTokenStore = (await import('./tokenStore')).tokenStore

    // Tab B adopts whatever tab A broadcasts.
    tabB.subscribeAuthBroadcast(
      (accessToken) => tabBTokenStore.setAccessToken(accessToken),
      () => tabBTokenStore.clearAccessToken(),
    )

    const [tokenFromA] = await Promise.all([tabA.refreshSession(), tabB.refreshSession()])

    expect(postMock).toHaveBeenCalledTimes(1)
    expect(tokenFromA).toBe(sharedToken)
    expect(tabATokenStore.getAccessToken()).toBe(sharedToken)
    expect(tabBTokenStore.getAccessToken()).toBe(sharedToken)
  })
})
