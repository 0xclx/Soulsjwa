import { describe, it, expect, beforeEach, vi } from 'vitest'
import { renderHook, act, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { useLogout } from './useLogout'
import { authApi } from '../api/authApi'
import { tokenStore } from '../../../lib/axios'
import { SCOREBOARD_CACHE_NAME } from '../../../lib/serviceWorkerCacheNames'

vi.mock('../api/authApi', () => ({
  authApi: { revoke: vi.fn(), refresh: vi.fn() },
}))

describe('useLogout', () => {
  beforeEach(() => {
    vi.mocked(authApi.revoke).mockReset().mockResolvedValue(undefined)
  })

  it('deletes the scoreboard cache so it does not survive on a shared device', async () => {
    const deleteMock = vi.fn().mockResolvedValue(true)
    vi.stubGlobal('caches', { delete: deleteMock })

    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
    const { result } = renderHook(() => useLogout(), {
      wrapper: ({ children }) => (
        <QueryClientProvider client={client}>
          <MemoryRouter>{children}</MemoryRouter>
        </QueryClientProvider>
      ),
    })

    act(() => {
      result.current.mutate()
    })

    await waitFor(() => expect(deleteMock).toHaveBeenCalledWith(SCOREBOARD_CACHE_NAME))
    expect(tokenStore.isAuthenticated()).toBe(false)

    vi.unstubAllGlobals()
  })
})
