import { apiClient } from '../../../../lib/axios'
import type { SiteTheme, UpdateSiteThemeRequest } from '../../../../types'

export const SITE_THEME_QUERY_KEYS = {
  detail: ['siteTheme'] as const,
}

export const siteThemeApi = {
  get: async (): Promise<SiteTheme> => {
    const { data } = await apiClient.get<SiteTheme>('/theme')
    return data
  },

  update: async (request: UpdateSiteThemeRequest): Promise<SiteTheme> => {
    const { data } = await apiClient.put<SiteTheme>('/theme', request)
    return data
  },
}
