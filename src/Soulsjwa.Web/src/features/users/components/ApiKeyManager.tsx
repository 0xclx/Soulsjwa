import { memo, useCallback, useState } from 'react'
import Box from '@mui/material/Box'
import Stack from '@mui/material/Stack'
import Typography from '@mui/material/Typography'
import Button from '@mui/material/Button'
import IconButton from '@mui/material/IconButton'
import TextField from '@mui/material/TextField'
import MenuItem from '@mui/material/MenuItem'
import Paper from '@mui/material/Paper'
import Alert from '@mui/material/Alert'
import AlertTitle from '@mui/material/AlertTitle'
import Chip from '@mui/material/Chip'
import Tooltip from '@mui/material/Tooltip'
import DeleteOutlineIcon from '@mui/icons-material/DeleteOutlined'
import { useApiKeys, useCreateApiKey, useDeleteApiKey } from '../hooks/useApiKeys'
import { Spinner, ErrorMessage } from '../../../components/ui'
import type { ApiKey } from '../../../types'

interface ApiKeyRowProps {
  apiKey: ApiKey
  onRevoke: (id: string) => void
  revoking: boolean
}

const formatExpiry = (iso: string | null | undefined): string => {
  if (!iso) return 'Never expires'
  const d = new Date(iso)
  if (Number.isNaN(d.getTime())) return 'Never expires'
  return `Expires ${d.toLocaleDateString()}`
}

const ApiKeyRow = memo(function ApiKeyRow({ apiKey, onRevoke, revoking }: ApiKeyRowProps) {
  const handleRevoke = useCallback(() => onRevoke(apiKey.id), [onRevoke, apiKey.id])

  return (
    <Stack
      component="li"
      direction="row"
      spacing={1}
      sx={{
        alignItems: 'center',
        justifyContent: 'space-between',
        p: 1.5,
        border: 1,
        borderColor: 'divider',
        borderRadius: 1,
        opacity: apiKey.isRevoked ? 0.6 : 1,
      }}
    >
      <Stack
        direction="row"
        spacing={1}
        sx={{ alignItems: 'center', minWidth: 0, flexWrap: 'wrap' }}
      >
        <Typography sx={{ fontWeight: 500 }}>{apiKey.name}</Typography>
        <Typography variant="caption" color="text.secondary">
          sk_{apiKey.keyPrefix}…
        </Typography>
        <Typography variant="caption" color="text.secondary">
          · {formatExpiry(apiKey.expiresAt)}
        </Typography>
        {apiKey.isRevoked && <Chip label="revoked" size="small" color="error" variant="outlined" />}
      </Stack>
      {!apiKey.isRevoked && (
        <Tooltip title="Revoke API key">
          <span>
            <IconButton
              aria-label={`Revoke API key ${apiKey.name}`}
              color="error"
              size="small"
              onClick={handleRevoke}
              disabled={revoking}
            >
              <DeleteOutlineIcon fontSize="small" />
            </IconButton>
          </span>
        </Tooltip>
      )}
    </Stack>
  )
})

type ExpiryOption = 'never' | '30d' | '90d' | '1y'

const EXPIRY_LABELS: Record<ExpiryOption, string> = {
  never: 'Never expires',
  '30d': 'In 30 days',
  '90d': 'In 90 days',
  '1y': 'In 1 year',
}

const expiryOptionToIso = (option: ExpiryOption): string | undefined => {
  if (option === 'never') return undefined
  const days = option === '30d' ? 30 : option === '90d' ? 90 : 365
  const d = new Date()
  d.setUTCDate(d.getUTCDate() + days)
  return d.toISOString()
}

export const ApiKeyManager = () => {
  const { data: apiKeys, isLoading, isError } = useApiKeys()
  const createKey = useCreateApiKey()
  const deleteKey = useDeleteApiKey()
  const [newKeyName, setNewKeyName] = useState('')
  const [expiryOption, setExpiryOption] = useState<ExpiryOption>('never')
  const [createdKey, setCreatedKey] = useState<string | null>(null)

  const handleCreate = useCallback(async () => {
    if (!newKeyName.trim()) return
    const expiresAt = expiryOptionToIso(expiryOption)
    const result = await createKey.mutateAsync(
      expiresAt ? { name: newKeyName.trim(), expiresAt } : { name: newKeyName.trim() },
    )
    setCreatedKey(result.key)
    setNewKeyName('')
    setExpiryOption('never')
  }, [createKey, newKeyName, expiryOption])

  const handleRevoke = useCallback((id: string) => deleteKey.mutate(id), [deleteKey])

  if (isLoading) return <Spinner />
  if (isError) return <ErrorMessage message="Failed to load API keys." />

  return (
    <Box component="section" aria-labelledby="api-keys-heading">
      <Typography variant="h5" component="h2" id="api-keys-heading" gutterBottom>
        API Keys
      </Typography>

      {createdKey && (
        <Alert severity="success" sx={{ mb: 2 }} onClose={() => setCreatedKey(null)}>
          <AlertTitle>Key created — copy it now, it won&apos;t be shown again</AlertTitle>
          <Paper
            component="code"
            sx={{
              display: 'block',
              p: 1,
              wordBreak: 'break-all',
              typography: 'body2',
              fontFamily: 'monospace',
            }}
          >
            {createdKey}
          </Paper>
        </Alert>
      )}

      <Stack
        direction={{ xs: 'column', sm: 'row' }}
        spacing={1}
        sx={{ mb: 2, alignItems: { sm: 'flex-start' } }}
      >
        <TextField
          size="small"
          fullWidth
          placeholder="Key name"
          value={newKeyName}
          onChange={(e) => setNewKeyName(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === 'Enter') {
              e.preventDefault()
              void handleCreate()
            }
          }}
          slotProps={{ htmlInput: { 'aria-label': 'New API key name' } }}
        />
        <TextField
          select
          size="small"
          label="Expires"
          value={expiryOption}
          onChange={(e) => setExpiryOption(e.target.value as ExpiryOption)}
          sx={{ minWidth: { sm: 160 } }}
          slotProps={{ htmlInput: { 'aria-label': 'API key expiry' } }}
        >
          {(Object.keys(EXPIRY_LABELS) as ExpiryOption[]).map((opt) => (
            <MenuItem key={opt} value={opt}>
              {EXPIRY_LABELS[opt]}
            </MenuItem>
          ))}
        </TextField>
        <Button
          onClick={handleCreate}
          disabled={createKey.isPending || !newKeyName.trim()}
          variant="contained"
        >
          {createKey.isPending ? 'Creating…' : 'Create'}
        </Button>
      </Stack>

      {apiKeys?.length === 0 && <Typography color="text.secondary">No API keys yet.</Typography>}

      <Stack component="ul" spacing={1} sx={{ listStyle: 'none', p: 0, m: 0 }}>
        {apiKeys?.map((k) => (
          <ApiKeyRow key={k.id} apiKey={k} onRevoke={handleRevoke} revoking={deleteKey.isPending} />
        ))}
      </Stack>
    </Box>
  )
}
