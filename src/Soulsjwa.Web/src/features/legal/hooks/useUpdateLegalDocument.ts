import { useMutation, useQueryClient } from '@tanstack/react-query'
import { legalApi, LEGAL_QUERY_KEYS } from '../api/legalApi'
import type { LegalDocumentKind } from '../../../types'

export const useUpdateLegalDocument = (kind: LegalDocumentKind) => {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (content: string | null) => legalApi.update(kind, content),
    onSuccess: (data) => {
      queryClient.setQueryData(LEGAL_QUERY_KEYS.detail(kind), data)
    },
  })
}
