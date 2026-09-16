import { useCallback, useState } from 'react'
import Button from '@mui/material/Button'
import Dialog from '@mui/material/Dialog'
import DialogActions from '@mui/material/DialogActions'
import DialogContent from '@mui/material/DialogContent'
import DialogTitle from '@mui/material/DialogTitle'
import Stack from '@mui/material/Stack'
import TextField from '@mui/material/TextField'
import type { useCreateEvent } from '../hooks/useCreateEvent'

interface CreateEventDialogProps {
  open: boolean
  mutation: ReturnType<typeof useCreateEvent>
  onClose: () => void
}

/** Admin-only focused dialog for creating a new event (name + description). */
export const CreateEventDialog = ({ open, mutation, onClose }: CreateEventDialogProps) => {
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')

  const handleCreate = useCallback(async () => {
    if (!name.trim()) return
    await mutation.mutateAsync({ name: name.trim(), description: description.trim() })
    setName('')
    setDescription('')
    onClose()
  }, [mutation, name, description, onClose])

  return (
    <Dialog open={open} onClose={onClose} fullWidth maxWidth="sm">
      <DialogTitle>Create event</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ mt: 1 }}>
          <TextField
            label="Event name"
            value={name}
            onChange={(e) => setName(e.target.value)}
            slotProps={{ htmlInput: { maxLength: 200 } }}
            helperText="Keep it short and recognizable in navigation and scoreboard views."
            required
            fullWidth
          />
          <TextField
            label="Description"
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            slotProps={{ htmlInput: { maxLength: 2000 } }}
            multiline
            minRows={3}
            fullWidth
          />
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={mutation.isPending}>
          Cancel
        </Button>
        <Button
          onClick={handleCreate}
          disabled={mutation.isPending || !name.trim()}
          variant="contained"
        >
          {mutation.isPending ? 'Creating…' : 'Create'}
        </Button>
      </DialogActions>
    </Dialog>
  )
}
