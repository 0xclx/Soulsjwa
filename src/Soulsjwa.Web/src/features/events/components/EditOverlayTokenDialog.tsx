import { useState } from 'react'
import Alert from '@mui/material/Alert'
import Button from '@mui/material/Button'
import Dialog from '@mui/material/Dialog'
import DialogActions from '@mui/material/DialogActions'
import DialogContent from '@mui/material/DialogContent'
import DialogContentText from '@mui/material/DialogContentText'
import DialogTitle from '@mui/material/DialogTitle'
import type { OverlayToken } from '../../../types'
import type { OverlayTokenSettings } from '../../../types/overlay'
import { useUpdateOverlayTokenSettings } from '../hooks/useUpdateOverlayTokenSettings'
import { OVERLAY_DEFAULT_SETTINGS } from '../overlay/overlayConfig'
import { OverlayDesigner, type OverlayDesignerGame } from './OverlayDesigner'
import type { OverlaySettingsCompetitor } from './OverlaySettingsForm'

interface EditOverlayTokenDialogProps {
  /** The token being edited; `null` keeps the dialog closed. */
  token: OverlayToken | null
  eventId: string
  eventName: string
  competitors?: OverlaySettingsCompetitor[]
  games: OverlayDesignerGame[]
  onClose: () => void
}

export const EDIT_TITLE_PREFIX = 'Edit overlay look'
export const SAVE_LABEL = 'Save look'
export const SAVING_LABEL = 'Saving…'
export const FOLLOWS_WITHIN_REFRESH_MESSAGE =
  'An OBS source using this token follows the saved look within its refresh interval — no need to touch the URL.'
export const URL_DRIVEN_MESSAGE =
  'This token has no saved look yet, so the parameters in its URL drive the overlay. Once you save, the saved look wins over those parameters.'
const SAVE_FAILED_MESSAGE = 'Could not save the overlay look.'

/**
 * Re-designs an existing token's look against the live preview and saves it
 * on the token, which is what makes a source already on screen in OBS change
 * without its URL being re-pasted.
 */
export function EditOverlayTokenDialog(props: EditOverlayTokenDialogProps) {
  // Keyed on the token so each opening starts from that token's saved look
  // rather than whatever the previous edit left behind.
  return props.token ? <EditOverlayTokenDialogBody key={props.token.id} {...props} /> : null
}

function EditOverlayTokenDialogBody({
  token,
  eventId,
  eventName,
  competitors,
  games,
  onClose,
}: EditOverlayTokenDialogProps) {
  const updateMutation = useUpdateOverlayTokenSettings(eventId)
  const [settings, setSettings] = useState<OverlayTokenSettings>(
    () => token?.settings ?? OVERLAY_DEFAULT_SETTINGS,
  )
  const [error, setError] = useState<string | null>(null)

  const handleSave = async () => {
    if (!token) return
    setError(null)
    try {
      await updateMutation.mutateAsync({ tokenId: token.id, settings })
      onClose()
    } catch (e) {
      setError(e instanceof Error ? e.message : SAVE_FAILED_MESSAGE)
    }
  }

  return (
    <Dialog open={!!token} onClose={onClose} fullWidth maxWidth="lg">
      <DialogTitle>
        {EDIT_TITLE_PREFIX}: {token?.name}
      </DialogTitle>
      <DialogContent>
        <DialogContentText sx={{ mb: 2 }}>
          {token?.settings ? FOLLOWS_WITHIN_REFRESH_MESSAGE : URL_DRIVEN_MESSAGE}
        </DialogContentText>
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
      </DialogContent>
      <DialogActions>
        <Button color="inherit" onClick={onClose}>
          Cancel
        </Button>
        <Button
          variant="contained"
          onClick={() => void handleSave()}
          disabled={updateMutation.isPending}
        >
          {updateMutation.isPending ? SAVING_LABEL : SAVE_LABEL}
        </Button>
      </DialogActions>
    </Dialog>
  )
}
