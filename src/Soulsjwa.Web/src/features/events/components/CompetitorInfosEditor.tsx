import { useState } from 'react'
import Box from '@mui/material/Box'
import Stack from '@mui/material/Stack'
import Typography from '@mui/material/Typography'
import Button from '@mui/material/Button'
import IconButton from '@mui/material/IconButton'
import TextField from '@mui/material/TextField'
import MenuItem from '@mui/material/MenuItem'
import Alert from '@mui/material/Alert'
import Link from '@mui/material/Link'
import Chip from '@mui/material/Chip'
import Divider from '@mui/material/Divider'
import AddIcon from '@mui/icons-material/Add'
import DeleteIcon from '@mui/icons-material/Delete'
import OpenInNewIcon from '@mui/icons-material/OpenInNew'
import { useCompetitorInfos } from '../hooks/useCompetitorInfos'
import { useAddCompetitorInfo } from '../hooks/useAddCompetitorInfo'
import { useRemoveCompetitorInfo } from '../hooks/useRemoveCompetitorInfo'
import type { CompetitorInfo, CompetitorInfoType } from '../../../types'

interface Props {
  eventId: string
  eventGameId: string
  userId: string
  /** Whether the viewer is allowed to add/remove infos for this competitor. */
  canEdit: boolean
  /**
   * Pre-loaded infos to show without firing the per-competitor query. When
   * provided, the editor uses these directly (e.g. from the scoreboard
   * payload) and only fetches when in edit-mode.
   */
  initialInfos?: CompetitorInfo[]
}

const TWITCH_HOSTS = ['twitch.tv', 'www.twitch.tv', 'm.twitch.tv', 'clips.twitch.tv']
const YOUTUBE_HOSTS = [
  'youtube.com',
  'www.youtube.com',
  'm.youtube.com',
  'music.youtube.com',
  'youtu.be',
]

function isValidUrl(url: string, deathClip: boolean): boolean {
  let parsed: URL
  try {
    parsed = new URL(url)
  } catch {
    return false
  }
  if (parsed.protocol !== 'https:' && parsed.protocol !== 'http:') return false
  if (!deathClip) return true
  if (parsed.protocol !== 'https:') return false
  const host = parsed.host.toLowerCase()
  return TWITCH_HOSTS.includes(host) || YOUTUBE_HOSTS.includes(host)
}

const TYPE_LABEL: Record<CompetitorInfoType, string> = {
  DeathClip: 'Death clip',
  Link: 'Link',
  Other: 'Note',
}

/**
 * Inline editor + viewer for additional metadata attached to a specific
 * (event-game, competitor) pair. The list is always shown; the "Add info"
 * controls only render when the viewer has edit permission for the target
 * competitor (admin / event owner / the competitor themselves / a delegated
 * moderator — gating is decided by the parent via <c>canEdit</c>).
 */
export function CompetitorInfosEditor({
  eventId,
  eventGameId,
  userId,
  canEdit,
  initialInfos,
}: Props) {
  // Only fetch live when no pre-loaded list is provided OR when the user is
  // actively editing (so deletes / adds immediately re-fetch fresh).
  const query = useCompetitorInfos(eventId, eventGameId, userId, !initialInfos)
  const infos = query.data ?? initialInfos ?? []

  const addMutation = useAddCompetitorInfo(eventId, eventGameId, userId)
  const removeMutation = useRemoveCompetitorInfo(eventId, eventGameId, userId)

  const [adding, setAdding] = useState(false)
  const [type, setType] = useState<CompetitorInfoType>('DeathClip')
  const [url, setUrl] = useState('')
  const [text, setText] = useState('')
  const [error, setError] = useState<string | null>(null)

  const reset = () => {
    setAdding(false)
    setType('DeathClip')
    setUrl('')
    setText('')
    setError(null)
  }

  const handleAdd = async () => {
    setError(null)
    if (type === 'DeathClip') {
      if (!url.trim()) return setError('A Twitch or YouTube URL is required for a death clip.')
      if (!isValidUrl(url.trim(), true))
        return setError('Death clip URL must be a Twitch or YouTube https URL.')
    } else if (type === 'Link') {
      if (!url.trim()) return setError('A URL is required.')
      if (!isValidUrl(url.trim(), false)) return setError('Please enter a valid http(s) URL.')
    } else if (type === 'Other') {
      if (!text.trim()) return setError('Please enter some text.')
    }
    try {
      await addMutation.mutateAsync({
        type,
        url: url.trim() || undefined,
        text: text.trim() || undefined,
      })
      reset()
    } catch (e) {
      const message =
        (e as { response?: { data?: { detail?: string } } })?.response?.data?.detail ??
        'Failed to add info.'
      setError(message)
    }
  }

  return (
    <Stack spacing={1}>
      {infos.length === 0 && !adding && !canEdit ? null : (
        <Stack spacing={0.5} divider={<Divider flexItem />}>
          {infos.map((info) => (
            <InfoRow
              key={info.id}
              info={info}
              canEdit={canEdit}
              onRemove={() => removeMutation.mutate(info.id)}
              removing={removeMutation.isPending && removeMutation.variables === info.id}
            />
          ))}
        </Stack>
      )}

      {canEdit && !adding && (
        <Box>
          <Button
            size="small"
            startIcon={<AddIcon />}
            onClick={() => setAdding(true)}
            variant="text"
          >
            Add info
          </Button>
        </Box>
      )}

      {canEdit && adding && (
        <Stack spacing={1} sx={{ p: 1, borderRadius: 1, bgcolor: 'action.hover' }}>
          <TextField
            select
            size="small"
            label="Type"
            value={type}
            onChange={(e) => setType(e.target.value as CompetitorInfoType)}
            sx={{ maxWidth: 200 }}
          >
            <MenuItem value="DeathClip">Death clip (Twitch / YouTube)</MenuItem>
            <MenuItem value="Link">Link (any URL)</MenuItem>
            <MenuItem value="Other">Note (plain text)</MenuItem>
          </TextField>

          {(type === 'DeathClip' || type === 'Link') && (
            <TextField
              size="small"
              label="URL"
              value={url}
              onChange={(e) => setUrl(e.target.value)}
              placeholder={
                type === 'DeathClip'
                  ? 'https://clips.twitch.tv/... or https://youtu.be/...'
                  : 'https://example.com/...'
              }
              fullWidth
              slotProps={{ htmlInput: { maxLength: 2048 } }}
            />
          )}

          <TextField
            size="small"
            label={type === 'Other' ? 'Text' : 'Caption (optional)'}
            value={text}
            onChange={(e) => setText(e.target.value)}
            multiline
            minRows={type === 'Other' ? 2 : 1}
            fullWidth
            slotProps={{ htmlInput: { maxLength: 2000 } }}
          />

          {error && <Alert severity="error">{error}</Alert>}

          <Stack direction="row" spacing={1}>
            <Button
              size="small"
              variant="contained"
              onClick={handleAdd}
              disabled={addMutation.isPending}
            >
              Add
            </Button>
            <Button size="small" onClick={reset} disabled={addMutation.isPending}>
              Cancel
            </Button>
          </Stack>
        </Stack>
      )}
    </Stack>
  )
}

interface InfoRowProps {
  info: CompetitorInfo
  canEdit: boolean
  onRemove: () => void
  removing: boolean
}

function InfoRow({ info, canEdit, onRemove, removing }: InfoRowProps) {
  return (
    <Stack direction="row" spacing={1} sx={{ alignItems: 'flex-start', py: 0.5 }}>
      <Chip
        size="small"
        label={info.type === 'DeathClip' ? '💀 Death clip' : TYPE_LABEL[info.type]}
        sx={{ flexShrink: 0 }}
      />
      <Box sx={{ flex: 1, minWidth: 0 }}>
        {info.url ? (
          <Link
            href={info.url}
            target="_blank"
            rel="noopener noreferrer"
            sx={{
              display: 'inline-flex',
              alignItems: 'center',
              gap: 0.5,
              wordBreak: 'break-all',
            }}
          >
            {info.url}
            <OpenInNewIcon fontSize="inherit" />
          </Link>
        ) : null}
        {info.text ? (
          <Typography
            variant="body2"
            sx={{ whiteSpace: 'pre-wrap', color: info.url ? 'text.secondary' : 'text.primary' }}
          >
            {info.text}
          </Typography>
        ) : null}
      </Box>
      {canEdit && (
        <IconButton size="small" onClick={onRemove} disabled={removing} aria-label="Remove info">
          <DeleteIcon fontSize="small" />
        </IconButton>
      )}
    </Stack>
  )
}
