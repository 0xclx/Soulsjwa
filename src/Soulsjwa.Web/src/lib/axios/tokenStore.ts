import { jwtDecode } from 'jwt-decode'

let accessToken: string | null = null

interface JwtPayload {
  exp: number
}

const listeners = new Set<() => void>()

const notify = (): void => {
  listeners.forEach((listener) => listener())
}

export const tokenStore = {
  getAccessToken: (): string | null => accessToken,

  setAccessToken: (token: string): void => {
    accessToken = token
    notify()
  },

  clearAccessToken: (): void => {
    accessToken = null
    notify()
  },

  isAuthenticated: (): boolean => !!accessToken,

  /** 0 when there is no token, or it has already expired. */
  msUntilAccessTokenExpiry: (): number => {
    if (!accessToken) return 0
    try {
      const { exp } = jwtDecode<JwtPayload>(accessToken)
      const msLeft = exp * 1000 - Date.now()
      return Math.max(0, msLeft)
    } catch {
      return 0
    }
  },

  /**
   * Returns an unsubscribe function. For `useIsAuthenticated`'s
   * `useSyncExternalStore` — non-React callers should keep reading
   * `isAuthenticated()`/`getAccessToken()` directly.
   */
  subscribe: (listener: () => void): (() => void) => {
    listeners.add(listener)
    return () => listeners.delete(listener)
  },
}
