import { apiClient } from '../../../lib/axios'
import type { LegalDocument, LegalDocumentKind } from '../../../types'

export const LEGAL_QUERY_KEYS = {
  detail: (kind: LegalDocumentKind) => ['legal', kind] as const,
}

export const legalApi = {
  get: async (kind: LegalDocumentKind): Promise<LegalDocument> => {
    const { data } = await apiClient.get<LegalDocument>(`/legal/${kind}`)
    return data
  },

  update: async (kind: LegalDocumentKind, content: string | null): Promise<LegalDocument> => {
    const { data } = await apiClient.put<LegalDocument>(`/legal/${kind}`, { content })
    return data
  },
}
