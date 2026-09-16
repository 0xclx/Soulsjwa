import { useMemo, useState } from 'react'
import Alert from '@mui/material/Alert'
import Button from '@mui/material/Button'
import Dialog from '@mui/material/Dialog'
import DialogActions from '@mui/material/DialogActions'
import DialogContent from '@mui/material/DialogContent'
import DialogTitle from '@mui/material/DialogTitle'
import MenuItem from '@mui/material/MenuItem'
import Stack from '@mui/material/Stack'
import TextField from '@mui/material/TextField'
import Typography from '@mui/material/Typography'
import { useAddGame } from '../hooks/useAddGame'
import type { EventResponse, GameResponse } from '../../../types'

interface Props {
  open: boolean
  event: EventResponse
  games: GameResponse[]
  onClose: () => void
}

export const AddGameDialog = ({ open, event, games, onClose }: Props) => {
  const addGame = useAddGame(event.id)
  const [gameId, setGameId] = useState('')
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [error, setError] = useState<string | null>(null)

  const selectedGame = useMemo(
    () => games.find((game) => game.id === Number(gameId)),
    [games, gameId],
  )

  const handleClose = () => {
    if (addGame.isPending) return
    setGameId('')
    setName('')
    setDescription('')
    setError(null)
    onClose()
  }

  const handleSubmit = () => {
    const selectedGameId = Number(gameId)
    if (!Number.isInteger(selectedGameId)) return
    setError(null)
    addGame.mutate(
      {
        gameId: selectedGameId,
        name: name.trim() || undefined,
        description: description.trim() || undefined,
      },
      {
        onSuccess: handleClose,
        onError: (err: unknown) => {
          const e = err as { response?: { data?: { detail?: string } } }
          setError(e?.response?.data?.detail ?? 'Failed to add game.')
        },
      },
    )
  }

  return (
    <Dialog open={open} onClose={handleClose} fullWidth maxWidth="sm">
      <DialogTitle>Add predefined game</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ mt: 1 }}>
          <TextField
            select
            label="Game"
            value={gameId}
            onChange={(e) => setGameId(e.target.value)}
            required
            fullWidth
          >
            {games.map((game) => (
              <MenuItem key={game.id} value={game.id}>
                {game.name}
              </MenuItem>
            ))}
          </TextField>
          {selectedGame && (
            <Typography variant="body2" color="text.secondary">
              {selectedGame.description}
            </Typography>
          )}
          <TextField
            label="Display name override"
            value={name}
            onChange={(e) => setName(e.target.value)}
            helperText="Optional. Leave empty to use the game's default name."
            slotProps={{ htmlInput: { maxLength: 200 } }}
            fullWidth
          />
          <TextField
            label="Description override"
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            helperText="Optional per-event description."
            slotProps={{ htmlInput: { maxLength: 1000 } }}
            multiline
            minRows={2}
            fullWidth
          />
          {error && <Alert severity="error">{error}</Alert>}
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={handleClose} disabled={addGame.isPending}>
          Cancel
        </Button>
        <Button variant="contained" onClick={handleSubmit} disabled={addGame.isPending || !gameId}>
          Add
        </Button>
      </DialogActions>
    </Dialog>
  )
}
