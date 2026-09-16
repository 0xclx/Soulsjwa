import { useState } from 'react'
import Alert from '@mui/material/Alert'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Checkbox from '@mui/material/Checkbox'
import Dialog from '@mui/material/Dialog'
import DialogActions from '@mui/material/DialogActions'
import DialogContent from '@mui/material/DialogContent'
import DialogContentText from '@mui/material/DialogContentText'
import DialogTitle from '@mui/material/DialogTitle'
import FormControlLabel from '@mui/material/FormControlLabel'
import Stack from '@mui/material/Stack'
import { useAddCompetitor } from '../hooks/useAddCompetitor'
import { UserPicker, type UserPickerOption } from '../../users/components/UserPicker'

interface AddCompetitorDialogProps {
  open: boolean
  eventId: string
  onClose: () => void
}

/**
 * Owner / admin dialog for adding a competitor. Supports both picking an
 * existing user and inviting by raw Twitch handle (the API pre-allowlists +
 * creates a placeholder user that gets adopted on first login).
 */
export const AddCompetitorDialog = ({ open, eventId, onClose }: AddCompetitorDialogProps) => {
  const [picked, setPicked] = useState<UserPickerOption | null>(null)
  const [isStreamer, setIsStreamer] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const addCompetitor = useAddCompetitor(eventId)

  const reset = () => {
    setPicked(null)
    setIsStreamer(false)
    setError(null)
  }

  const handleClose = () => {
    reset()
    onClose()
  }

  const handleAdd = () => {
    if (!picked) return
    setError(null)
    addCompetitor.mutate(
      { userId: picked.userId, twitchLogin: picked.twitchLogin, isStreamer },
      {
        onSuccess: () => {
          reset()
          onClose()
        },
        onError: (err: unknown) => {
          const e = err as { response?: { status?: number; data?: { detail?: string } } }
          if (e?.response?.status === 409) setError('That user is already a competitor.')
          else setError(e?.response?.data?.detail ?? 'Failed to add competitor.')
        },
      },
    )
  }

  return (
    <Dialog open={open} onClose={handleClose} fullWidth maxWidth="sm">
      <DialogTitle>Add competitor</DialogTitle>
      <DialogContent>
        <DialogContentText sx={{ mb: 2 }}>
          Search an existing user or invite a Twitch handle into this event roster.
        </DialogContentText>
        <Stack spacing={2}>
          <Box>
            <UserPicker
              value={picked}
              onChange={setPicked}
              allowFreeText
              label="Twitch handle or known user"
              fullWidth
            />
          </Box>
          <FormControlLabel
            control={
              <Checkbox checked={isStreamer} onChange={(e) => setIsStreamer(e.target.checked)} />
            }
            label="Streamer (can delegate moderators)"
          />
          {error && <Alert severity="error">{error}</Alert>}
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button color="inherit" onClick={handleClose} disabled={addCompetitor.isPending}>
          Cancel
        </Button>
        <Button
          variant="contained"
          onClick={handleAdd}
          disabled={!picked || addCompetitor.isPending}
        >
          {addCompetitor.isPending ? 'Adding…' : 'Add'}
        </Button>
      </DialogActions>
    </Dialog>
  )
}
