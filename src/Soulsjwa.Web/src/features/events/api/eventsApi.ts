import axios from 'axios'
import { apiClient } from '../../../lib/axios'
import type {
  EventResponse,
  FailRemainingObjectivesResponse,
  EventListItem,
  PaginatedResponse,
  GameResponse,
  GameDataPoint,
  ScoreEntry,
  ScoreboardResponse,
  PredefinedObjective,
  CompetitorInfo,
  CompetitorInfoType,
  TieBreakMode,
  AuditLogPage,
  EditCompletionTimeRequest,
} from '../../../types'

export interface EventAuditsParams {
  page?: number
  pageSize?: number
  /** Keyset cursor from a previous page's `nextCursor`; set alongside `page`/`pageSize`, whichever the caller uses. */
  cursor?: string
  types?: string[]
  actorUserId?: string
  subjectUserId?: string
  eventGameId?: string
  objectiveId?: string
  from?: string
  to?: string
}

export interface ListEventsParams {
  page?: number
  pageSize?: number
  includeArchived?: boolean
  search?: string
  status?: EventStatusFilter
}

export const EVENT_STATUS_FILTERS = ['all', 'live', 'stopped', 'archived'] as const
export type EventStatusFilter = (typeof EVENT_STATUS_FILTERS)[number]

export const EVENT_STATUS_FILTER_LABELS: Record<EventStatusFilter, string> = {
  all: 'All statuses',
  live: 'Live',
  stopped: 'Stopped',
  archived: 'Archived',
}

export const EVENTS_QUERY_KEYS = {
  all: ['events'] as const,
  listRoot: ['events', 'list'] as const,
  list: (params: ListEventsParams) => ['events', 'list', params] as const,
  // 'detail' segment (not a bare id) so this key never prefix-matches a
  // sibling sub-resource below — see eventCache.ts.
  detail: (id: string) => ['events', 'detail', id] as const,
  featured: ['events', 'featured'] as const,
  scores: (id: string) => ['events', id, 'scores'] as const,
  scoreboard: (id: string) => ['events', id, 'scoreboard'] as const,
  games: ['games'] as const,
  gameDataDefinitions: (gameId: number) => ['games', gameId, 'data-definitions'] as const,
  predefinedObjectives: (gameId: number) => ['objectives', 'predefined', gameId] as const,
  competitorInfos: (eventId: string, eventGameId: string, userId: string) =>
    ['events', eventId, 'games', eventGameId, 'competitors', userId, 'infos'] as const,
  auditsRoot: (eventId: string) => ['events', eventId, 'audits'] as const,
  audits: (eventId: string, params: EventAuditsParams) =>
    ['events', eventId, 'audits', params] as const,
}

export const eventsApi = {
  list: async (params: ListEventsParams = {}): Promise<PaginatedResponse<EventListItem>> => {
    const { page = 1, pageSize = 20, includeArchived = false, search, status = 'all' } = params
    const { data } = await apiClient.get<PaginatedResponse<EventListItem>>('/events', {
      params: { page, pageSize, includeArchived, search: search || undefined, status },
    })
    return data
  },

  get: async (id: string): Promise<EventResponse> => {
    const { data } = await apiClient.get<EventResponse>(`/events/${id}`)
    return data
  },

  getFeatured: async (): Promise<EventResponse | null> => {
    try {
      const { data } = await apiClient.get<EventResponse>('/events/featured')
      return data
    } catch (error) {
      if (axios.isAxiosError(error) && error.response?.status === 404) return null
      throw error
    }
  },

  create: async (payload: { name: string; description: string }): Promise<EventResponse> => {
    const { data } = await apiClient.post<EventResponse>('/events', payload)
    return data
  },

  update: async (
    id: string,
    payload: {
      name?: string
      description?: string
      tieBreakMode?: TieBreakMode
      urlAlias?: string
    },
  ): Promise<EventResponse> => {
    const { data } = await apiClient.patch<EventResponse>(`/events/${id}`, payload)
    return data
  },

  archive: async (id: string): Promise<void> => {
    await apiClient.post(`/events/${id}/archive`)
  },

  duplicate: async (id: string): Promise<EventResponse> => {
    const { data } = await apiClient.post<EventResponse>(`/events/${id}/duplicate`)
    return data
  },

  startEvent: async (id: string): Promise<void> => {
    await apiClient.post(`/events/${id}/start`)
  },

  stopEvent: async (id: string): Promise<void> => {
    await apiClient.post(`/events/${id}/stop`)
  },

  featureEvent: async (id: string): Promise<void> => {
    await apiClient.post(`/events/${id}/feature`)
  },

  unfeatureEvent: async (id: string): Promise<void> => {
    await apiClient.post(`/events/${id}/unfeature`)
  },

  getScores: async (eventId: string): Promise<ScoreEntry[]> => {
    const { data } = await apiClient.get<ScoreEntry[]>(`/events/${eventId}/scores`)
    return data
  },

  getScoreboard: async (eventId: string): Promise<ScoreboardResponse> => {
    const { data } = await apiClient.get<ScoreboardResponse>(`/events/${eventId}/scoreboard`)
    return data
  },

  listEventAudits: async (
    eventId: string,
    params: EventAuditsParams = {},
  ): Promise<AuditLogPage> => {
    const { types, ...rest } = params
    const { data } = await apiClient.get<AuditLogPage>(`/events/${eventId}/audits`, {
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

  editCompletionTime: async (
    eventId: string,
    eventGameId: string,
    objectiveId: string,
    userId: string,
    body: EditCompletionTimeRequest,
  ): Promise<{ objectiveId: string; userId: string; completedAt: string }> => {
    const { data } = await apiClient.patch<{
      objectiveId: string
      userId: string
      completedAt: string
    }>(
      `/events/${eventId}/games/${eventGameId}/objectives/${objectiveId}/completions/${userId}`,
      body,
    )
    return data
  },

  listGames: async (): Promise<GameResponse[]> => {
    const { data } = await apiClient.get<GameResponse[]>('/games')
    return data
  },

  getGameDataDefinitions: async (gameId: number): Promise<GameDataPoint[]> => {
    const { data } = await apiClient.get<GameDataPoint[]>(`/games/${gameId}/data-definitions`)
    return data
  },

  createObjective: async (
    eventId: string,
    eventGameId: string,
    payload: { name: string; score: number; category?: string; rule?: string; failRule?: string },
  ): Promise<void> => {
    await apiClient.post(`/events/${eventId}/games/${eventGameId}/objectives`, payload)
  },

  addCustomGame: async (
    eventId: string,
    payload: { name: string; description?: string },
  ): Promise<{ id: string }> => {
    const { data } = await apiClient.post<{ id: string }>(
      `/events/${eventId}/games/custom`,
      payload,
    )
    return data
  },

  addGame: async (
    eventId: string,
    payload: { gameId: number; name?: string; description?: string },
  ): Promise<{ id: string }> => {
    const { data } = await apiClient.post<{ id: string }>(`/events/${eventId}/games`, payload)
    return data
  },

  removeEventGame: async (eventId: string, eventGameId: string): Promise<void> => {
    await apiClient.delete(`/events/${eventId}/games/${eventGameId}`)
  },

  enableEventGame: async (eventId: string, eventGameId: string): Promise<void> => {
    await apiClient.post(`/events/${eventId}/games/${eventGameId}/enable`)
  },

  disableEventGame: async (eventId: string, eventGameId: string): Promise<void> => {
    await apiClient.post(`/events/${eventId}/games/${eventGameId}/disable`)
  },

  patchEventGame: async (
    eventId: string,
    eventGameId: string,
    payload: { name?: string; description?: string },
  ): Promise<void> => {
    await apiClient.patch(`/events/${eventId}/games/${eventGameId}`, payload)
  },

  reorderEventGames: async (eventId: string, eventGameIds: string[]): Promise<void> => {
    await apiClient.put(`/events/${eventId}/games/reorder`, { eventGameIds })
  },

  reorderObjectives: async (
    eventId: string,
    eventGameId: string,
    objectiveIds: string[],
  ): Promise<void> => {
    await apiClient.put(`/events/${eventId}/games/${eventGameId}/objectives/reorder`, {
      objectiveIds,
    })
  },

  // Imports predefined objectives into an event-game. Pass `objectiveIds` to
  // import only a subset; omit/empty for all. Idempotent on objective name.
  importPredefinedObjectives: async (
    eventId: string,
    eventGameId: string,
    objectiveIds?: string[],
  ): Promise<{ importedCount: number; skippedCount: number }> => {
    const { data } = await apiClient.post<{
      importedCount: number
      skippedCount: number
    }>(
      `/events/${eventId}/games/${eventGameId}/objectives/import-predefined`,
      objectiveIds && objectiveIds.length > 0 ? { objectiveIds } : {},
    )
    return data
  },

  editObjective: async (
    eventId: string,
    eventGameId: string,
    objectiveId: string,
    payload: {
      name?: string
      score?: number
      category?: string | null
      metadata?: string | null
      rule?: string | null
      failRule?: string | null
    },
  ): Promise<void> => {
    await apiClient.patch(
      `/events/${eventId}/games/${eventGameId}/objectives/${objectiveId}`,
      payload,
    )
  },

  deleteObjective: async (
    eventId: string,
    eventGameId: string,
    objectiveId: string,
  ): Promise<void> => {
    await apiClient.delete(`/events/${eventId}/games/${eventGameId}/objectives/${objectiveId}`)
  },

  listPredefinedObjectives: async (gameId: number): Promise<PredefinedObjective[]> => {
    const { data } = await apiClient.get<PredefinedObjective[]>(`/objectives/predefined`, {
      params: { gameId },
    })
    return data
  },

  addModerator: async (eventId: string, competitorId: string, userId: string): Promise<void> => {
    await apiClient.post(`/events/${eventId}/competitors/${competitorId}/moderators/`, { userId })
  },
  removeModerator: async (eventId: string, competitorId: string, userId: string): Promise<void> => {
    await apiClient.delete(`/events/${eventId}/competitors/${competitorId}/moderators/${userId}`)
  },

  completeObjective: async (
    eventId: string,
    eventGameId: string,
    objectiveId: string,
    onBehalfOfUserId?: string,
    /**
     * Asserts the trial run the caller believes it is writing to. The server
     * 409s if a different run (or none) is recording by the time it lands, so
     * a run stopping between a poll and a click cannot silently write to the
     * official record instead.
     */
    expectedTrialRunId?: string,
  ): Promise<void> => {
    const params = {
      ...(onBehalfOfUserId ? { onBehalfOfUserId } : {}),
      ...(expectedTrialRunId ? { expectedTrialRunId } : {}),
    }
    await apiClient.post(
      `/events/${eventId}/games/${eventGameId}/objectives/${objectiveId}/complete`,
      null,
      { params },
    )
  },

  uncompleteObjective: async (
    eventId: string,
    eventGameId: string,
    objectiveId: string,
    onBehalfOfUserId?: string,
    /**
     * Asserts the trial run the caller believes it is writing to. The server
     * 409s if a different run (or none) is recording by the time it lands, so
     * a run stopping between a poll and a click cannot silently write to the
     * official record instead.
     */
    expectedTrialRunId?: string,
  ): Promise<void> => {
    const params = {
      ...(onBehalfOfUserId ? { onBehalfOfUserId } : {}),
      ...(expectedTrialRunId ? { expectedTrialRunId } : {}),
    }
    await apiClient.delete(
      `/events/${eventId}/games/${eventGameId}/objectives/${objectiveId}/complete`,
      { params },
    )
  },

  failObjective: async (
    eventId: string,
    eventGameId: string,
    objectiveId: string,
    onBehalfOfUserId?: string,
    /**
     * Asserts the trial run the caller believes it is writing to. The server
     * 409s if a different run (or none) is recording by the time it lands, so
     * a run stopping between a poll and a click cannot silently write to the
     * official record instead.
     */
    expectedTrialRunId?: string,
  ): Promise<void> => {
    const params = {
      ...(onBehalfOfUserId ? { onBehalfOfUserId } : {}),
      ...(expectedTrialRunId ? { expectedTrialRunId } : {}),
    }
    await apiClient.post(
      `/events/${eventId}/games/${eventGameId}/objectives/${objectiveId}/fail`,
      null,
      { params },
    )
  },

  resetFailedObjective: async (
    eventId: string,
    eventGameId: string,
    objectiveId: string,
    onBehalfOfUserId?: string,
    /**
     * Asserts the trial run the caller believes it is writing to. The server
     * 409s if a different run (or none) is recording by the time it lands, so
     * a run stopping between a poll and a click cannot silently write to the
     * official record instead.
     */
    expectedTrialRunId?: string,
  ): Promise<void> => {
    const params = {
      ...(onBehalfOfUserId ? { onBehalfOfUserId } : {}),
      ...(expectedTrialRunId ? { expectedTrialRunId } : {}),
    }
    await apiClient.delete(
      `/events/${eventId}/games/${eventGameId}/objectives/${objectiveId}/fail`,
      { params },
    )
  },

  /**
   * Marks every objective of the game still pending for the target failed in
   * one call — a run that is over (a death in a no-death run) gets its final
   * result without a click per objective. Same self/on-behalf-of and trial
   * rules as `failObjective`; objectives already completed or failed are left
   * alone.
   */
  failRemainingObjectives: async (
    eventId: string,
    eventGameId: string,
    onBehalfOfUserId?: string,
    expectedTrialRunId?: string,
  ): Promise<FailRemainingObjectivesResponse> => {
    const params = {
      ...(onBehalfOfUserId ? { onBehalfOfUserId } : {}),
      ...(expectedTrialRunId ? { expectedTrialRunId } : {}),
    }
    const { data } = await apiClient.post<FailRemainingObjectivesResponse>(
      `/events/${eventId}/games/${eventGameId}/objectives/fail-remaining`,
      null,
      { params },
    )
    return data
  },

  setLive: async (eventId: string, isLive: boolean, onBehalfOfUserId?: string): Promise<void> => {
    const params = onBehalfOfUserId ? { onBehalfOfUserId } : undefined
    await apiClient.post(`/events/${eventId}/live`, { isLive }, { params })
  },

  addCompetitor: async (
    eventId: string,
    payload: { userId?: string; twitchLogin?: string; isStreamer?: boolean },
  ): Promise<void> => {
    await apiClient.post(`/events/${eventId}/competitors`, payload)
  },

  selfJoin: async (eventId: string): Promise<void> => {
    await apiClient.post(`/events/${eventId}/competitors/self`)
  },

  updateCompetitor: async (
    eventId: string,
    userId: string,
    payload: { isStreamer: boolean },
  ): Promise<void> => {
    await apiClient.patch(`/events/${eventId}/competitors/${userId}`, payload)
  },

  removeCompetitor: async (eventId: string, userId: string): Promise<void> => {
    await apiClient.delete(`/events/${eventId}/competitors/${userId}`)
  },

  listCompetitorInfos: async (
    eventId: string,
    eventGameId: string,
    userId: string,
  ): Promise<CompetitorInfo[]> => {
    const { data } = await apiClient.get<CompetitorInfo[]>(
      `/events/${eventId}/games/${eventGameId}/competitors/${userId}/infos`,
    )
    return data
  },

  addCompetitorInfo: async (
    eventId: string,
    eventGameId: string,
    userId: string,
    payload: { type: CompetitorInfoType; url?: string; text?: string },
  ): Promise<CompetitorInfo> => {
    const { data } = await apiClient.post<CompetitorInfo>(
      `/events/${eventId}/games/${eventGameId}/competitors/${userId}/infos`,
      payload,
    )
    return data
  },

  updateCompetitorInfo: async (
    eventId: string,
    eventGameId: string,
    userId: string,
    infoId: string,
    payload: { type?: CompetitorInfoType; url?: string | null; text?: string | null },
  ): Promise<CompetitorInfo> => {
    const { data } = await apiClient.patch<CompetitorInfo>(
      `/events/${eventId}/games/${eventGameId}/competitors/${userId}/infos/${infoId}`,
      payload,
    )
    return data
  },

  removeCompetitorInfo: async (
    eventId: string,
    eventGameId: string,
    userId: string,
    infoId: string,
  ): Promise<void> => {
    await apiClient.delete(
      `/events/${eventId}/games/${eventGameId}/competitors/${userId}/infos/${infoId}`,
    )
  },

  unarchive: async (id: string): Promise<void> => {
    await apiClient.post(`/events/${id}/unarchive`)
  },
}
