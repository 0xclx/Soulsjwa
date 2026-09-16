/**
 * Global "auth has failed" signal, dispatched whenever a silent refresh
 * fails. Subscribers (typically the root layout) clear client-side state and
 * redirect to the public landing page, so a logged-out user never sees
 * logged-in chrome on stale data.
 *
 * A `CustomEvent` on `window` rather than a React context because the
 * producer lives in the axios layer (no React) and the consumer in the
 * router — neither side has to import the other.
 */
export const AUTH_REFRESH_FAILED_EVENT = 'soulsjwa:auth-refresh-failed'

/** Dispatch the refresh-failure signal. Safe to call outside the browser. */
export const emitAuthRefreshFailed = (): void => {
  if (typeof window === 'undefined') return
  window.dispatchEvent(new CustomEvent(AUTH_REFRESH_FAILED_EVENT))
}

/** Returns an unsubscribe function, for an effect's cleanup. */
export const onAuthRefreshFailed = (handler: () => void): (() => void) => {
  if (typeof window === 'undefined') return () => {}
  window.addEventListener(AUTH_REFRESH_FAILED_EVENT, handler)
  return () => window.removeEventListener(AUTH_REFRESH_FAILED_EVENT, handler)
}
