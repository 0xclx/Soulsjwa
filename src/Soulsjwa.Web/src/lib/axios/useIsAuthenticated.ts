import { useSyncExternalStore } from 'react'
import { tokenStore } from './tokenStore'

/**
 * Reactive view of `tokenStore.isAuthenticated()`. `tokenStore` is a plain
 * module variable outside React, so reading it during render never triggers
 * a re-render when the token changes; subscribing via `useSyncExternalStore`
 * makes auth-dependent UI update the moment the token is set or cleared.
 */
export const useIsAuthenticated = (): boolean =>
  useSyncExternalStore(tokenStore.subscribe, tokenStore.isAuthenticated, () => false)
