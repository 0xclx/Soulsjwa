import { apiClient } from '../../../lib/axios'
import type { UploadMediaResponse } from '../../../types'

export const mediaApi = {
  upload: async (
    file: File,
    onProgress?: (percent: number) => void,
  ): Promise<UploadMediaResponse> => {
    const formData = new FormData()
    formData.append('file', file)

    const { data } = await apiClient.post<UploadMediaResponse>('/uploads', formData, {
      // apiClient defaults every request to application/json; that must not
      // apply here; explicitly undefined removes it (rather than override
      // with a hand-typed multipart/form-data) so the browser sets the
      // header itself, boundary included.
      headers: { 'Content-Type': undefined },
      onUploadProgress: (event) => {
        if (onProgress && event.total) {
          onProgress(Math.round((event.loaded / event.total) * 100))
        }
      },
    })
    return data
  },
}
