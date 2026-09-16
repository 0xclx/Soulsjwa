import axios from 'axios'
import { tokenStore } from './tokenStore'

const REFRESH_URL = '/api/v1/auth/refresh'
const REFRESH_LOCK_NAME = 'soulsjwa:auth-refresh'
const AUTH_CHANNEL_NAME = 'soulsjwa:auth'

type AuthBroadcastMessage = { type: 'refreshed'; accessToken: string } | { type: 'signed-out' }

let inFlight: Promise<string> | null = null

const authChannel: BroadcastChannel | null =
  typeof BroadcastChannel !== 'undefined' ? new BroadcastChannel(AUTH_CHANNEL_NAME) : null

const postRefresh = async (): Promise<string> => {
  // Plain axios (not apiClient) so the auth-refresh interceptor doesn't
  // intercept its own refresh request and cause an infinite loop.
  const { data } = await axios.post<{ accessToken: string }>(REFRESH_URL, undefined, {
    withCredentials: true,
  })
  return data.accessToken
}

const doRefresh = async (expiryBeforeLock: number): Promise<string> => {
  // A sibling tab may have already refreshed and broadcast a newer token
  // while we were waiting to acquire the cross-tab lock.
  if (tokenStore.msUntilAccessTokenExpiry() > expiryBeforeLock) {
    const token = tokenStore.getAccessToken()
    if (token) return token
  }

  const accessToken = await postRefresh()
  tokenStore.setAccessToken(accessToken)
  authChannel?.postMessage({ type: 'refreshed', accessToken } satisfies AuthBroadcastMessage)
  return accessToken
}

const runWithLock = (expiryBeforeLock: number): Promise<string> => {
  if (typeof navigator !== 'undefined' && navigator.locks) {
    return navigator.locks.request(REFRESH_LOCK_NAME, () => doRefresh(expiryBeforeLock))
  }
  return doRefresh(expiryBeforeLock)
}

/**
 * Single-flight, cross-tab-coordinated session refresh.
 *
 * Concurrent callers within one tab (the proactive timer and a 401-triggered
 * retry racing each other) share one in-flight promise. The Web Locks API,
 * when available, additionally serialises the underlying network call across
 * browser tabs. This matters because the backend rotates the refresh token on
 * every call and treats a replayed (already-rotated) token as theft, revoking
 * every refresh token for the user — so two overlapping refreshes must never
 * both reach the network.
 */
export const refreshSession = (): Promise<string> => {
  if (!inFlight) {
    const expiryBeforeLock = tokenStore.msUntilAccessTokenExpiry()
    inFlight = runWithLock(expiryBeforeLock).finally(() => {
      inFlight = null
    })
  }
  return inFlight
}

/** Tell sibling tabs this tab's session ended, so they stop relying on it. */
export const broadcastSignedOut = (): void => {
  authChannel?.postMessage({ type: 'signed-out' } satisfies AuthBroadcastMessage)
}

/**
 * Subscribe to refresh outcomes broadcast by sibling tabs so this tab can
 * adopt a rotated token — or sign out — without making its own network call.
 * Returns an unsubscribe function.
 */
export const subscribeAuthBroadcast = (
  onRefreshed: (accessToken: string) => void,
  onSignedOut: () => void,
): (() => void) => {
  if (!authChannel) return () => {}
  const handler = (event: MessageEvent<AuthBroadcastMessage>): void => {
    if (event.data.type === 'refreshed') onRefreshed(event.data.accessToken)
    else onSignedOut()
  }
  authChannel.addEventListener('message', handler)
  return () => authChannel.removeEventListener('message', handler)
}
