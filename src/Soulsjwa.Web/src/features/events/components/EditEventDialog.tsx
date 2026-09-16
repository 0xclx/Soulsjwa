import { useState } from 'react'
import Alert from '@mui/material/Alert'
import Button from '@mui/material/Button'
import Dialog from '@mui/material/Dialog'
import DialogActions from '@mui/material/DialogActions'
import DialogContent from '@mui/material/DialogContent'
import DialogTitle from '@mui/material/DialogTitle'
import Stack from '@mui/material/Stack'
import TextField from '@mui/material/TextField'
import { getErrorDetail } from '../../../lib/getErrorDetail'
import type { EventResponse, TieBreakMode } from '../../../types'
import { DEFAULT_TIE_BREAK_MODE, TIE_BREAK_MODES, TIE_BREAK_MODE_LABELS } from '../../../types'
import type { SubmitMutation } from '../eventDetail/mutation'

type UpdateEventVars = {
  name?: string
  description?: string
  tieBreakMode?: TieBreakMode
  urlAlias?: string
}

interface EditEventDialogProps {
  open: boolean
  event: EventResponse
  mutation: SubmitMutation<UpdateEventVars, EventResponse>
  onClose: () => void
  onSaved: (event: EventResponse) => void
}

/** Owner dialog for editing an event's name, description, and tie-break mode. */
export const EditEventDialog = ({
  open,
  event,
  mutation,
  onClose,
  onSaved,
}: EditEventDialogProps) => {
  // Remount (via `key` at the call site) reseeds these from the current event
  // whenever the dialog is (re)opened.
  const [name, setName] = useState(event.name)
  const [description, setDescription] = useState(event.description ?? '')
  const [urlAlias, setUrlAlias] = useState(event.urlAlias ?? '')
  const [tieBreakMode, setTieBreakMode] = useState<TieBreakMode>(
    event.tieBreakMode ?? DEFAULT_TIE_BREAK_MODE,
  )
  const [error, setError] = useState<string | null>(null)

  const handleSave = () => {
    setError(null)
    if (!name.trim()) {
      setError('Name is required.')
      return
    }
    mutation.mutate(
      {
        name: name.trim(),
        description: description.trim(),
        tieBreakMode,
        urlAlias: urlAlias.trim(),
      },
      {
        onSuccess: onSaved,
        onError: (err) => setError(getErrorDetail(err, 'Failed to save event.')),
      },
    )
  }

  return (
    <Dialog open={open} onClose={onClose} fullWidth maxWidth="sm">
      <DialogTitle>Edit event</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ mt: 1 }}>
          <TextField
            label="Name"
            size="small"
            value={name}
            onChange={(e) => setName(e.target.value)}
            fullWidth
          />
          <TextField
            label="URL alias"
            size="small"
            value={urlAlias}
            onChange={(e) => setUrlAlias(e.target.value)}
            helperText="Optional: 3–64 lowercase letters or numbers separated by single hyphens."
            slotProps={{ htmlInput: { maxLength: 64 } }}
            fullWidth
          />
          <TextField
            label="Description"
            size="small"
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            multiline
            minRows={2}
            fullWidth
          />
          <TextField
            select
            label="Tie-break mode"
            size="small"
            value={tieBreakMode}
            onChange={(e) => setTieBreakMode(e.target.value as TieBreakMode)}
            helperText="How tied scores are ranked on the scoreboard."
            slotProps={{ select: { native: true } }}
            fullWidth
          >
            {TIE_BREAK_MODES.map((mode) => (
              <option key={mode} value={mode}>
                {TIE_BREAK_MODE_LABELS[mode]}
              </option>
            ))}
          </TextField>
          {error && <Alert severity="error">{error}</Alert>}
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={mutation.isPending}>
          Cancel
        </Button>
        <Button variant="contained" onClick={handleSave} disabled={mutation.isPending}>
          Save
        </Button>
      </DialogActions>
    </Dialog>
  )
}
