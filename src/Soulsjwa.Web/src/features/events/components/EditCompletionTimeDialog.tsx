import { useState } from 'react'
import Alert from '@mui/material/Alert'
import Button from '@mui/material/Button'
import Dialog from '@mui/material/Dialog'
import DialogActions from '@mui/material/DialogActions'
import DialogContent from '@mui/material/DialogContent'
import DialogTitle from '@mui/material/DialogTitle'
import Stack from '@mui/material/Stack'
import TextField from '@mui/material/TextField'
import Typography from '@mui/material/Typography'
import { getErrorDetail } from '../../../lib/getErrorDetail'
import { isoToLocalDateTime } from '../eventDetail/permissions'
import type { SubmitMutation } from '../eventDetail/mutation'

export interface EditCompletionTarget {
  eventGameId: string
  objectiveId: string
  objectiveName: string
  competitorName: string
  currentCompletedAt: string
}

type EditCompletionVars = {
  eventGameId: string
  objectiveId: string
  userId: string
  body: { completedAt: string; reason: string }
}

interface EditCompletionTimeDialogProps {
  open: boolean
  target: EditCompletionTarget
  /** The competitor whose completion is being edited. */
  userId: string
  mutation: SubmitMutation<
    EditCompletionVars,
    { objectiveId: string; userId: string; completedAt: string }
  >
  onClose: () => void
  onSuccess: () => void
}

/** Admin/owner dialog for correcting an objective completion timestamp. */
export const EditCompletionTimeDialog = ({
  open,
  target,
  userId,
  mutation,
  onClose,
  onSuccess,
}: EditCompletionTimeDialogProps) => {
  const [completedAt, setCompletedAt] = useState(isoToLocalDateTime(target.currentCompletedAt))
  const [reason, setReason] = useState('')
  const [error, setError] = useState<string | null>(null)

  const handleSave = () => {
    if (!completedAt) {
      setError('New completion time is required.')
      return
    }
    if (!reason.trim()) {
      setError('Reason is required.')
      return
    }

    setError(null)
    mutation.mutate(
      {
        eventGameId: target.eventGameId,
        objectiveId: target.objectiveId,
        userId,
        body: { completedAt: new Date(completedAt).toISOString(), reason: reason.trim() },
      },
      {
        onSuccess,
        onError: (err) => setError(getErrorDetail(err, 'Failed to edit completion time.')),
      },
    )
  }

  return (
    <Dialog open={open} onClose={onClose} fullWidth>
      <DialogTitle>Edit completion time</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ mt: 1 }}>
          <Typography variant="body2" color="text.secondary">
            Competitor: {target.competitorName}
          </Typography>
          <Typography variant="body2" color="text.secondary">
            Objective: {target.objectiveName}
          </Typography>
          <Typography variant="body2" color="text.secondary">
            Current completion: {new Date(target.currentCompletedAt).toLocaleString()}
          </Typography>
          <TextField
            label="New completion time"
            type="datetime-local"
            value={completedAt}
            onChange={(e) => setCompletedAt(e.target.value)}
            required
            fullWidth
          />
          <TextField
            label="Reason"
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            required
            multiline
            minRows={3}
            helperText="Why is this edit being made? This appears in the audit log."
            fullWidth
          />
          {error && <Alert severity="error">{error}</Alert>}
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={mutation.isPending}>
          Cancel
        </Button>
        <Button
          variant="contained"
          onClick={handleSave}
          disabled={mutation.isPending || !completedAt || !reason.trim()}
        >
          Save
        </Button>
      </DialogActions>
    </Dialog>
  )
}
