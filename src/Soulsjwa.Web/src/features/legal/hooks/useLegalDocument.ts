import { useQuery } from '@tanstack/react-query'
import { legalApi, LEGAL_QUERY_KEYS } from '../api/legalApi'
import type { LegalDocumentKind } from '../../../types'

export const useLegalDocument = (kind: LegalDocumentKind) =>
  useQuery({
    queryKey: LEGAL_QUERY_KEYS.detail(kind),
    queryFn: () => legalApi.get(kind),
  })
