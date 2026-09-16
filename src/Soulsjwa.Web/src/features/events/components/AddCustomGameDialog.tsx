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
import { useAddCustomGame } from '../hooks/useAddCustomGame'
import { getErrorDetail } from '../../../lib/getErrorDetail'

interface AddCustomGameDialogProps {
  open: boolean
  eventId: string
  onClose: () => void
}

/**
 * Dialog for adding a custom (non-catalog) game to an event. Custom games are
 * not connector-supported, so they only take manually toggled objectives.
 */
export const AddCustomGameDialog = ({ open, eventId, onClose }: AddCustomGameDialogProps) => {
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [error, setError] = useState<string | null>(null)

  const addCustomGame = useAddCustomGame(eventId)

  const reset = () => {
    setName('')
    setDescription('')
    setError(null)
  }

  const handleClose = () => {
    reset()
    onClose()
  }

  const handleAdd = async () => {
    if (!name.trim()) {
      setError('Name is required.')
      return
    }
    setError(null)
    try {
      await addCustomGame.mutateAsync({
        name: name.trim(),
        description: description.trim() || undefined,
      })
      reset()
      onClose()
    } catch (err) {
      setError(getErrorDetail(err, 'Failed to add custom game.'))
    }
  }

  return (
    <Dialog open={open} onClose={handleClose} fullWidth maxWidth="sm">
      <DialogTitle>Add custom game</DialogTitle>
      <DialogContent>
        <DialogContentText sx={{ mb: 2 }}>
          Add a title that isn&apos;t in the seeded catalog. Custom games take manually toggled
          objectives only.
        </DialogContentText>
        <Stack spacing={2}>
          <TextField
            label="Game name"
            value={name}
            onChange={(e) => setName(e.target.value)}
            slotProps={{ htmlInput: { maxLength: 200 } }}
            required
            fullWidth
          />
          <TextField
            label="Description (optional)"
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            slotProps={{ htmlInput: { maxLength: 1000 } }}
            multiline
            minRows={2}
            fullWidth
          />
          {error && <Alert severity="error">{error}</Alert>}
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button color="inherit" onClick={handleClose} disabled={addCustomGame.isPending}>
          Cancel
        </Button>
        <Button
          variant="contained"
          onClick={handleAdd}
          disabled={addCustomGame.isPending || !name.trim()}
        >
          {addCustomGame.isPending ? 'Adding…' : 'Add'}
        </Button>
      </DialogActions>
    </Dialog>
  )
}
