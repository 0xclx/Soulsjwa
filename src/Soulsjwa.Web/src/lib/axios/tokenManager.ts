import { tokenStore } from './tokenStore'
import { emitAuthRefreshFailed } from './authEvents'
import { refreshSession, broadcastSignedOut, subscribeAuthBroadcast } from './refreshSession'

/**
 * Proactive token manager: schedules a silent refresh ~60s before the access
 * token expires, so the user never hits a 401 mid-interaction. The
 * axios-auth-refresh interceptor on `apiClient` is the safety net for a
 * request that fires right at the boundary. Both paths go through
 * `refreshSession()`, which single-flights the network call within a tab and
 * across tabs.
 */

const REFRESH_BEFORE_EXPIRY_MS = 60_000 // refresh 60 s early
const REFRESH_JITTER_MAX_MS = 5_000 // spread tabs that fall back to no cross-tab lock

let scheduleHandle: ReturnType<typeof setTimeout> | null = null
let unsubscribeBroadcast: (() => void) | null = null

const performRefresh = async (): Promise<void> => {
  try {
    await refreshSession()
    scheduleNext()
  } catch {
    // Refresh failed — stop scheduling and tell the UI to redirect away
    // from any logged-in-only chrome. Mirrors apiClient.refreshAuthLogic
    // so reactive (401-triggered) and proactive failures look the same
    // to subscribers.
    tokenStore.clearAccessToken()
    broadcastSignedOut()
    tokenManager.stop()
    emitAuthRefreshFailed()
  }
}

const scheduleNext = (): void => {
  // Clear any previously pending timer first — otherwise two overlapping
  // bootstrap() calls (React 19 StrictMode double-invokes mount effects)
  // leave an orphaned timer that fires an extra, unnecessary refresh later.
  tokenManager.stop()

  const msLeft = tokenStore.msUntilAccessTokenExpiry()

  if (msLeft === 0) {
    // No token or already expired — don't attempt refresh unless
    // we already had a token (i.e. it just expired, not first visit).
    return
  }

  const jitter = Math.random() * REFRESH_JITTER_MAX_MS
  const delay = Math.max(0, msLeft - REFRESH_BEFORE_EXPIRY_MS + jitter)
  scheduleHandle = setTimeout(() => void performRefresh(), delay)
}

const ensureBroadcastSubscription = (): void => {
  if (unsubscribeBroadcast) return
  unsubscribeBroadcast = subscribeAuthBroadcast(
    (accessToken) => {
      // A sibling tab refreshed for us — adopt its token and reschedule
      // without making our own network call.
      tokenStore.setAccessToken(accessToken)
      scheduleNext()
    },
    () => {
      tokenStore.clearAccessToken()
      tokenManager.stop()
      emitAuthRefreshFailed()
    },
  )
}

export const tokenManager = {
  /** Call once when the app mounts or after a successful login. */
  start: (): void => {
    tokenManager.stop()
    ensureBroadcastSubscription()
    if (tokenStore.isAuthenticated()) {
      scheduleNext()
    }
  },

  /**
   * Restore a session on page load from the httpOnly refresh-token cookie,
   * and start scheduling if it works.
   */
  bootstrap: async (): Promise<void> => {
    try {
      await refreshSession()
      scheduleNext()
    } catch {
      // No valid session — user is not logged in. This is expected on
      // first visit or after logout, so we silently ignore.
      tokenStore.clearAccessToken()
    }
  },

  /** Cancel any pending refresh timer (call on logout). */
  stop: (): void => {
    if (scheduleHandle !== null) {
      clearTimeout(scheduleHandle)
      scheduleHandle = null
    }
  },
}
