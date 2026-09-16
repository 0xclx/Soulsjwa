import type {
  AdminUserSummary,
  AllowlistEntry,
  AuditLogPage,
  PaginatedResponse,
  UserRole,
  FeatureFlag,
  GameResponse,
  PredefinedObjective,
} from '../../../types'
import { apiClient } from '../../../lib/axios'

export interface AdminAuditsParams {
  page?: number
  pageSize?: number
  /** Keyset cursor from a previous page's `nextCursor`; set alongside `page`/`pageSize`, whichever the caller uses. */
  cursor?: string
  types?: string[]
  actorUserId?: string
  subjectUserId?: string
  eventId?: string
  eventGameId?: string
  objectiveId?: string
  from?: string
  to?: string
}

export const ADMIN_QUERY_KEYS = {
  allowlist: ['admin', 'allowlist'] as const,
  users: (page: number, search?: string) => ['admin', 'users', page, search] as const,
  audits: (params: AdminAuditsParams) => ['admin', 'audits', params] as const,
  featureFlags: ['admin', 'feature-flags'] as const,
  games: ['games'] as const,
  predefinedObjectives: ['objectives', 'predefined'] as const,
}

export const FEATURE_FLAG_KEYS = {
  myEventsQuickComplete: 'myevents.quick_complete.enabled',
} as const

export const adminApi = {
  listAllowlist: async (): Promise<AllowlistEntry[]> => {
    const { data } = await apiClient.get<AllowlistEntry[]>('/admin/allowlist/')
    return data
  },

  addAllowlist: async (payload: {
    twitchLogin: string
    note?: string
  }): Promise<AllowlistEntry> => {
    const { data } = await apiClient.post<AllowlistEntry>('/admin/allowlist/', payload)
    return data
  },

  removeAllowlist: async (id: string): Promise<void> => {
    await apiClient.delete(`/admin/allowlist/${id}`)
  },

  listUsers: async (page = 1, search?: string): Promise<PaginatedResponse<AdminUserSummary>> => {
    const { data } = await apiClient.get<PaginatedResponse<AdminUserSummary>>('/admin/users/', {
      params: { page, search },
    })
    return data
  },

  setUserRole: async (userId: string, role: UserRole): Promise<AdminUserSummary> => {
    const { data } = await apiClient.patch<AdminUserSummary>(`/admin/users/${userId}/role`, {
      role,
    })
    return data
  },

  listAdminAudits: async (params: AdminAuditsParams = {}): Promise<AuditLogPage> => {
    const { types, ...rest } = params
    const { data } = await apiClient.get<AuditLogPage>('/admin/audits', {
      params: {
        ...rest,
        // Multi-value query: ASP.NET binds `string[]?` from repeated
        // `?type=a&type=b`. The `indexes: null` serializer option emits
        // exactly that form (instead of axios's default `type[]=a`).
        type: types && types.length > 0 ? types : undefined,
      },
      paramsSerializer: { indexes: null },
    })
    return data
  },

  getFeatureFlag: async (key: string): Promise<FeatureFlag> => {
    const { data } = await apiClient.get<FeatureFlag>(
      `/admin/feature-flags/${encodeURIComponent(key)}`,
    )
    return data
  },

  updateFeatureFlag: async (key: string, enabled: boolean): Promise<FeatureFlag> => {
    const { data } = await apiClient.put<FeatureFlag>(
      `/admin/feature-flags/${encodeURIComponent(key)}`,
      { enabled },
    )
    return data
  },

  listGames: async (): Promise<GameResponse[]> => {
    const { data } = await apiClient.get<GameResponse[]>('/games')
    return data
  },

  createGame: async (payload: { name: string; description?: string }): Promise<GameResponse> => {
    const { data } = await apiClient.post<GameResponse>('/games', payload)
    return data
  },

  updateGame: async (
    gameId: number,
    payload: { name: string; description: string },
  ): Promise<GameResponse> => {
    const { data } = await apiClient.patch<GameResponse>(`/games/${gameId}`, payload)
    return data
  },

  listPredefinedObjectives: async (): Promise<PredefinedObjective[]> => {
    const { data } = await apiClient.get<PredefinedObjective[]>('/objectives/predefined')
    return data
  },

  createPredefinedObjective: async (payload: {
    gameId: number
    name: string
    score: number
    category?: string
  }): Promise<PredefinedObjective> => {
    const { data } = await apiClient.post<PredefinedObjective>('/objectives/predefined', payload)
    return data
  },
}
