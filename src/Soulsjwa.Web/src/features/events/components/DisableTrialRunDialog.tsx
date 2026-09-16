import { useRef, useState } from 'react'
import Alert from '@mui/material/Alert'
import Button from '@mui/material/Button'
import Dialog from '@mui/material/Dialog'
import DialogActions from '@mui/material/DialogActions'
import DialogContent from '@mui/material/DialogContent'
import DialogContentText from '@mui/material/DialogContentText'
import DialogTitle from '@mui/material/DialogTitle'
import TextField from '@mui/material/TextField'

interface DisableTrialRunDialogProps {
  open: boolean
  gameName: string
  pending: boolean
  error: string | null
  onCancel: () => void
  onConfirm: () => void
}

/**
 * Disabling trial mode is destructive and irreversible: it
 * hard-deletes the competitor's trial completions/failures for this game.
 * The confirm button stays disabled until the admin types the game's name
 * exactly, so a stray click can never trigger it.
 */
export const DisableTrialRunDialog = ({
  open,
  gameName,
  pending,
  error,
  onCancel,
  onConfirm,
}: DisableTrialRunDialogProps) => {
  const [typed, setTyped] = useState('')
  const matches = typed === gameName
  const inputRef = useRef<HTMLInputElement>(null)

  return (
    <Dialog
      open={open}
      onClose={pending ? undefined : onCancel}
      slotProps={{
        transition: {
          onEnter: () => setTyped(''),
          onEntered: () => inputRef.current?.focus(),
        },
      }}
    >
      <DialogTitle>Disable trial mode for {gameName}?</DialogTitle>
      <DialogContent>
        <DialogContentText>
          This permanently deletes this competitor&apos;s trial progress for {gameName}. This cannot
          be undone. Type the game name to confirm.
        </DialogContentText>
        <TextField
          inputRef={inputRef}
          fullWidth
          margin="dense"
          label="Game name"
          placeholder={gameName}
          value={typed}
          onChange={(e) => setTyped(e.target.value)}
          disabled={pending}
          sx={{ mt: 2 }}
        />
        {error && (
          <Alert severity="error" sx={{ mt: 2 }}>
            {error}
          </Alert>
        )}
      </DialogContent>
      <DialogActions>
        <Button onClick={onCancel} disabled={pending}>
          Cancel
        </Button>
        <Button
          color="error"
          variant="contained"
          onClick={onConfirm}
          disabled={!matches || pending}
        >
          Disable trial mode
        </Button>
      </DialogActions>
    </Dialog>
  )
}
