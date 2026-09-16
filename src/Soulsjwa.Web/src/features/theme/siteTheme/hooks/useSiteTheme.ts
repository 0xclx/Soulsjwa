import { useQuery } from '@tanstack/react-query'
import { siteThemeApi, SITE_THEME_QUERY_KEYS } from '../api/siteThemeApi'

export const useSiteTheme = () =>
  useQuery({
    queryKey: SITE_THEME_QUERY_KEYS.detail,
    queryFn: siteThemeApi.get,
  })
