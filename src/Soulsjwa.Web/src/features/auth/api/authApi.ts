import { apiClient } from '../../../lib/axios'
import type { AccessTokenResponse } from '../../../types'

export const authApi = {
  refresh: async (): Promise<AccessTokenResponse> => {
    const { data } = await apiClient.post<AccessTokenResponse>('/auth/refresh')
    return data
  },

  revoke: async (): Promise<void> => {
    await apiClient.post('/auth/revoke')
  },
}
