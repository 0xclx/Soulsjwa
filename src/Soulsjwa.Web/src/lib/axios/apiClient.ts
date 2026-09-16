import axios from 'axios'
import createAuthRefresh from 'axios-auth-refresh'
import { tokenStore } from './tokenStore'
import { emitAuthRefreshFailed } from './authEvents'
import { refreshSession, broadcastSignedOut } from './refreshSession'
import { etagCache } from './etagCache'

export const apiClient = axios.create({
  baseURL: '/api/v1',
  headers: { 'Content-Type': 'application/json' },
  withCredentials: true,
})

// Attach the current access token to every outgoing request, and — for a PUT
// to a URL this client has previously GET'd — the ETag that read saw, as
// If-Match. A stale write then gets rejected with 409 instead of
// silently overwriting a concurrent change; a caller that wants to skip this
// can set If-Match itself beforehand.
apiClient.interceptors.request.use((config) => {
  const token = tokenStore.getAccessToken()
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }
  if (config.method === 'put' && config.url && !config.headers['If-Match']) {
    const etag = etagCache.get(config.url)
    if (etag) config.headers['If-Match'] = etag
  }
  return config
})

// Remember every ETag this client sees, keyed by the request URL it came
// back on, so a later PUT to that same URL can send it as If-Match.
apiClient.interceptors.response.use((response) => {
  const etag = response.headers?.etag
  if (etag && response.config.url) {
    etagCache.set(response.config.url, etag)
  }
  return response
})

/**
 * Called by axios-auth-refresh on any 401: it pauses the concurrent failing
 * requests, calls this once, then retries them with the new Authorization
 * header. Delegates to `refreshSession()` so this and `tokenManager`'s
 * proactive refresh share one in-flight request.
 */
const refreshAuthLogic = async (): Promise<void> => {
  try {
    await refreshSession()
  } catch {
    tokenStore.clearAccessToken()
    broadcastSignedOut()
    // Tell the UI the session is gone so it can redirect the user to a
    // public page instead of leaving stale logged-in chrome on screen.
    emitAuthRefreshFailed()
    throw new Error('Token refresh failed')
  }
}

// deduplicateRefresh ensures only one refresh call fires even if multiple
// requests return 401 simultaneously; the pausing behaviour is built-in.
createAuthRefresh(apiClient, refreshAuthLogic, {
  statusCodes: [401],
  deduplicateRefresh: true,
})
