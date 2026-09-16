import { useMutation } from '@tanstack/react-query'
import { mediaApi } from '../api/mediaApi'

interface UploadMediaVariables {
  file: File
  onProgress?: (percent: number) => void
}

export const useUploadMedia = () =>
  useMutation({
    mutationFn: ({ file, onProgress }: UploadMediaVariables) => mediaApi.upload(file, onProgress),
  })
