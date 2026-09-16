import type { User, ApiKey, UserSearchResult } from '../../../types'
import { apiClient } from '../../../lib/axios'

export const USERS_QUERY_KEYS = {
  me: ['users', 'me'] as const,
  apiKeys: ['users', 'me', 'api-keys'] as const,
  search: (q: string) => ['users', 'search', q] as const,
}

export const usersApi = {
  getMe: async (): Promise<User> => {
    const { data } = await apiClient.get<User>('/users/me')
    return data
  },

  search: async (q: string, limit = 20): Promise<UserSearchResult[]> => {
    const { data } = await apiClient.get<UserSearchResult[]>('/users/search', {
      params: { q, limit },
    })
    return data
  },

  getApiKeys: async (): Promise<ApiKey[]> => {
    // The endpoint excludes revoked keys by default; this UI shows
    // them (dimmed, with a "revoked" chip) as part of the user's key
    // history, so opt back in explicitly.
    const { data } = await apiClient.get<ApiKey[]>('/users/me/api-keys', {
      params: { includeRevoked: true },
    })
    return data
  },

  createApiKey: async (payload: {
    name: string
    expiresAt?: string
  }): Promise<ApiKey & { key: string }> => {
    const { data } = await apiClient.post<ApiKey & { key: string }>('/users/me/api-keys', payload)
    return data
  },

  deleteApiKey: async (id: string): Promise<void> => {
    await apiClient.delete(`/users/me/api-keys/${id}`)
  },
}
