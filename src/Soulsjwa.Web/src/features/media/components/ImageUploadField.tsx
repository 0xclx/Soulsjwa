import { useRef, useState } from 'react'
import Alert from '@mui/material/Alert'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import IconButton from '@mui/material/IconButton'
import LinearProgress from '@mui/material/LinearProgress'
import Stack from '@mui/material/Stack'
import Typography from '@mui/material/Typography'
import CloseIcon from '@mui/icons-material/Close'
import CloudUploadIcon from '@mui/icons-material/CloudUpload'
import { getErrorDetail } from '../../../lib/getErrorDetail'
import { useUploadMedia } from '../hooks/useUploadMedia'
import type { UploadMediaResponse } from '../../../types'

/** Mirrors the server's accepted formats and size cap (MediaEndpoint.MaxUploadBytes) so obviously-invalid files never leave the browser. */
const ACCEPTED_CONTENT_TYPES = ['image/png', 'image/jpeg', 'image/webp']
const MAX_UPLOAD_BYTES = 5 * 1024 * 1024

interface ImageUploadFieldProps {
  value: UploadMediaResponse | null
  onChange: (value: UploadMediaResponse | null) => void
  label?: string
  disabled?: boolean
}

/**
 * Uploads an image via useUploadMedia, with a client-side type/size
 * pre-check (fast, obvious rejections never touch the network — the server
 * remains the source of truth for anything subtler, like a mislabeled or
 * malformed file), upload progress, and server-error surfacing. A failed
 * upload never calls onChange, so the field stays in its prior state
 * rather than appearing to have succeeded.
 */
export const ImageUploadField = ({
  value,
  onChange,
  label = 'Image',
  disabled,
}: ImageUploadFieldProps) => {
  const upload = useUploadMedia()
  const [progress, setProgress] = useState(0)
  const [clientError, setClientError] = useState<string | null>(null)
  const inputRef = useRef<HTMLInputElement>(null)

  const handleFileSelected = (file: File | undefined) => {
    if (!file) return
    setClientError(null)

    if (!ACCEPTED_CONTENT_TYPES.includes(file.type)) {
      setClientError('Only PNG, JPEG, and WebP images are supported.')
      return
    }
    if (file.size > MAX_UPLOAD_BYTES) {
      setClientError('Image must be 5 MiB or smaller.')
      return
    }

    setProgress(0)
    upload.mutate({ file, onProgress: setProgress }, { onSuccess: onChange })
  }

  const handleRemove = () => {
    onChange(null)
    upload.reset()
    setClientError(null)
    if (inputRef.current) inputRef.current.value = ''
  }

  const error =
    clientError ?? (upload.isError ? getErrorDetail(upload.error, 'Upload failed.') : null)
  const fileInput = (
    <input
      ref={inputRef}
      type="file"
      hidden
      accept={ACCEPTED_CONTENT_TYPES.join(',')}
      onChange={(e) => handleFileSelected(e.target.files?.[0])}
    />
  )

  return (
    <Stack spacing={1}>
      <Typography variant="subtitle2">{label}</Typography>
      {value ? (
        <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
          <Box
            component="img"
            src={value.url}
            alt=""
            sx={{
              width: 64,
              height: 64,
              objectFit: 'cover',
              borderRadius: 1,
              border: 1,
              borderColor: 'divider',
            }}
          />
          <Button
            size="small"
            variant="outlined"
            component="label"
            disabled={disabled || upload.isPending}
          >
            Replace
            {fileInput}
          </Button>
          <IconButton
            size="small"
            onClick={handleRemove}
            disabled={disabled || upload.isPending}
            aria-label="Remove image"
          >
            <CloseIcon fontSize="small" />
          </IconButton>
        </Stack>
      ) : (
        <Button
          variant="outlined"
          component="label"
          startIcon={<CloudUploadIcon />}
          disabled={disabled || upload.isPending}
        >
          Upload image
          {fileInput}
        </Button>
      )}
      {upload.isPending && <LinearProgress variant="determinate" value={progress} />}
      {error && <Alert severity="error">{error}</Alert>}
    </Stack>
  )
}
