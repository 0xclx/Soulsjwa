import { useCallback, useState } from 'react'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Dialog from '@mui/material/Dialog'
import DialogActions from '@mui/material/DialogActions'
import DialogContent from '@mui/material/DialogContent'
import DialogTitle from '@mui/material/DialogTitle'
import Stack from '@mui/material/Stack'
import TextField from '@mui/material/TextField'
import Typography from '@mui/material/Typography'
import { useEditEventGame } from '../hooks/useEditEventGame'
import type { EventGame } from '../../../types'

interface EditEventGameDialogProps {
  open: boolean
  eventId: string
  game: EventGame
  onClose: () => void
}

const prefillName = (game: EventGame): string =>
  game.isCustomGame ? game.gameName : game.gameName !== game.knownGameName ? game.gameName : ''

/**
 * Focused dialog for renaming a game (or reverting a predefined game to its
 * catalog name) and editing its description.
 */
export const EditEventGameDialog = ({ open, eventId, game, onClose }: EditEventGameDialogProps) => {
  const [name, setName] = useState(() => prefillName(game))
  const [description, setDescription] = useState(() => game.customGameDescription ?? '')
  const [error, setError] = useState<string | null>(null)

  const editEventGame = useEditEventGame(eventId)

  const resetForm = useCallback(() => {
    setName(prefillName(game))
    setDescription(game.customGameDescription ?? '')
    setError(null)
  }, [game])

  const handleClose = useCallback(() => {
    resetForm()
    onClose()
  }, [onClose, resetForm])

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    setError(null)

    const trimmedName = name.trim()
    if (game.isCustomGame && !trimmedName) {
      setError('Name is required for a custom game.')
      return
    }

    editEventGame.mutate(
      {
        eventGameId: game.eventGameId,
        name: trimmedName,
        description: description.trim(),
      },
      {
        onSuccess: () => {
          resetForm()
          onClose()
        },
        onError: () => {
          setError('Failed to update game.')
        },
      },
    )
  }

  return (
    <Dialog open={open} onClose={handleClose} fullWidth maxWidth="sm">
      <Box component="form" onSubmit={handleSubmit}>
        <DialogTitle>Edit {game.gameName}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            <TextField
              label="Name"
              value={name}
              onChange={(e) => setName(e.target.value)}
              slotProps={{ htmlInput: { maxLength: 200 } }}
              helperText={
                !game.isCustomGame
                  ? `Leave blank to use the catalog name ("${game.knownGameName}").`
                  : undefined
              }
            />
            <TextField
              label="Description"
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              multiline
              minRows={2}
              slotProps={{ htmlInput: { maxLength: 2000 } }}
            />
            {error && (
              <Typography color="error" variant="body2">
                {error}
              </Typography>
            )}
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={handleClose} color="inherit" disabled={editEventGame.isPending}>
            Cancel
          </Button>
          <Button type="submit" variant="contained" disabled={editEventGame.isPending}>
            {editEventGame.isPending ? 'Saving…' : 'Save'}
          </Button>
        </DialogActions>
      </Box>
    </Dialog>
  )
}
