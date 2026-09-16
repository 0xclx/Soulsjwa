import { useMutation, useQueryClient } from '@tanstack/react-query'
import { siteThemeApi, SITE_THEME_QUERY_KEYS } from '../api/siteThemeApi'
import type { UpdateSiteThemeRequest } from '../../../../types'

export const useUpdateSiteTheme = () => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (request: UpdateSiteThemeRequest) => siteThemeApi.update(request),
    onSuccess: (data) => {
      queryClient.setQueryData(SITE_THEME_QUERY_KEYS.detail, data)
    },
  })
}
