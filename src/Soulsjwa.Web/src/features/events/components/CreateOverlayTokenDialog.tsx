import { useState } from 'react'
import Alert from '@mui/material/Alert'
import Button from '@mui/material/Button'
import Dialog from '@mui/material/Dialog'
import DialogActions from '@mui/material/DialogActions'
import DialogContent from '@mui/material/DialogContent'
import DialogContentText from '@mui/material/DialogContentText'
import DialogTitle from '@mui/material/DialogTitle'
import Stack from '@mui/material/Stack'
import TextField from '@mui/material/TextField'
import ContentCopyIcon from '@mui/icons-material/ContentCopy'
import type { OverlayTokenWithSecret } from '../../../types'
import type { OverlayTokenSettings } from '../../../types/overlay'
import { useCreateOverlayToken } from '../hooks/useCreateOverlayToken'
import { OVERLAY_DEFAULT_SETTINGS } from '../overlay/overlayConfig'
import { OverlayDesigner, type OverlayDesignerGame } from './OverlayDesigner'
import type { OverlaySettingsCompetitor } from './OverlaySettingsForm'

interface CreateOverlayTokenDialogProps {
  open: boolean
  eventId: string
  eventUrlIdentifier: string
  eventName: string
  competitors?: OverlaySettingsCompetitor[]
  games: OverlayDesignerGame[]
  /** The signed-in user's id — "pin to player" defaults to them when they compete here. */
  currentUserId?: string
  onClose: () => void
}

export const CREATE_TITLE = 'Create overlay token'
export const CREATED_TITLE = 'Overlay token created'
export const CREATE_LABEL = 'Create token'
export const CREATING_LABEL = 'Creating…'
export const COPY_LABEL = 'Copy URL'
export const COPIED_LABEL = 'Copied'
export const DONE_LABEL = 'Done'
export const NAME_REQUIRED_MESSAGE = 'Give the token a name so you can tell them apart later.'
export const COPY_NOW_MESSAGE = "Copy the URL now — you won't be able to see this token again."
export const LOOK_SAVED_MESSAGE =
  'The look you designed is saved with the token. Change it any time from the token list; an OBS source using this URL follows within its refresh interval.'
const CREATE_FAILED_MESSAGE = 'Failed to create overlay token.'

function buildOverlayUrl(eventId: string, token: string): string {
  const base = window.location.origin
  return `${base}/events/${eventId}/overlay?token=${encodeURIComponent(token)}`
}

async function copyToClipboard(text: string): Promise<boolean> {
  try {
    await navigator.clipboard.writeText(text)
    return true
  } catch {
    return false
  }
}

/**
 * Minting an overlay token, design first: the look is chosen against a live
 * preview and saved with the token, so the URL that is revealed afterwards
 * carries only the token. The raw secret is returned by the API exactly
 * once, so after a successful create the dialog switches to a copy-it-now
 * view; the token list only ever shows the name, look, and actions afterwards.
 */
export function CreateOverlayTokenDialog({
  open,
  eventId,
  eventUrlIdentifier,
  eventName,
  competitors,
  games,
  currentUserId,
  onClose,
}: CreateOverlayTokenDialogProps) {
  const createMutation = useCreateOverlayToken(eventId)

  // Defaults to the streamer's own run when they compete here — the common
  // case for this dialog (a competitor minting their own OBS link) — rather
  // than the top-ranked competitor, which is rarely who's watching.
  const initialSettings = (): OverlayTokenSettings => ({
    ...OVERLAY_DEFAULT_SETTINGS,
    playerIds:
      currentUserId && competitors?.some((c) => c.userId === currentUserId)
        ? [currentUserId]
        : null,
  })

  const [name, setName] = useState('')
  const [settings, setSettings] = useState<OverlayTokenSettings>(initialSettings)
  const [revealed, setRevealed] = useState<OverlayTokenWithSecret | null>(null)
  const [copied, setCopied] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const reset = () => {
    setName('')
    setSettings(initialSettings())
    setRevealed(null)
    setCopied(false)
    setError(null)
  }

  const handleClose = () => {
    reset()
    onClose()
  }

  const handleCreate = async () => {
    setError(null)
    const trimmed = name.trim()
    if (!trimmed) {
      setError(NAME_REQUIRED_MESSAGE)
      return
    }
    try {
      const token = await createMutation.mutateAsync({ name: trimmed, settings })
      setRevealed(token)
    } catch (e) {
      setError(e instanceof Error ? e.message : CREATE_FAILED_MESSAGE)
    }
  }

  const url = revealed ? buildOverlayUrl(eventUrlIdentifier, revealed.token) : ''

  const handleCopy = async () => {
    if (await copyToClipboard(url)) {
      setCopied(true)
      window.setTimeout(() => setCopied(false), 2000)
    }
  }

  return (
    <Dialog open={open} onClose={handleClose} fullWidth maxWidth={revealed ? 'sm' : 'lg'}>
      <DialogTitle>{revealed ? CREATED_TITLE : CREATE_TITLE}</DialogTitle>
      <DialogContent>
        {revealed ? (
          <>
            <Alert severity="warning" sx={{ mb: 2 }}>
              {COPY_NOW_MESSAGE}
            </Alert>
            <Stack direction="row" spacing={1} sx={{ alignItems: 'center', mb: 2 }}>
              <TextField
                value={url}
                size="small"
                fullWidth
                slotProps={{ htmlInput: { readOnly: true, 'aria-label': 'Overlay URL' } }}
              />
              <Button
                size="small"
                variant="outlined"
                startIcon={<ContentCopyIcon />}
                onClick={() => void handleCopy()}
              >
                {copied ? COPIED_LABEL : COPY_LABEL}
              </Button>
            </Stack>
            <DialogContentText>{LOOK_SAVED_MESSAGE}</DialogContentText>
          </>
        ) : (
          <>
            <DialogContentText sx={{ mb: 2 }}>
              Design the overlay against the preview, then mint the token and paste the URL into an
              OBS browser source. Anyone with the link can view the scoreboard, but you can revoke
              the token at any time.
            </DialogContentText>
            <TextField
              label="Token name"
              placeholder="e.g. OBS – main scene"
              value={name}
              onChange={(e) => setName(e.target.value)}
              slotProps={{ htmlInput: { maxLength: 100 } }}
              fullWidth
              sx={{ mb: 3 }}
              onKeyDown={(e) => {
                if (e.key === 'Enter') {
                  e.preventDefault()
                  void handleCreate()
                }
              }}
            />
            <OverlayDesigner
              eventId={eventId}
              eventName={eventName}
              value={settings}
              onChange={setSettings}
              competitors={competitors}
              games={games}
            />
            {error && (
              <Alert severity="error" sx={{ mt: 2 }}>
                {error}
              </Alert>
            )}
          </>
        )}
      </DialogContent>
      <DialogActions>
        {revealed ? (
          <Button variant="contained" onClick={handleClose}>
            {DONE_LABEL}
          </Button>
        ) : (
          <>
            <Button color="inherit" onClick={handleClose}>
              Cancel
            </Button>
            <Button
              variant="contained"
              onClick={() => void handleCreate()}
              disabled={createMutation.isPending}
            >
              {createMutation.isPending ? CREATING_LABEL : CREATE_LABEL}
            </Button>
          </>
        )}
      </DialogActions>
    </Dialog>
  )
}
