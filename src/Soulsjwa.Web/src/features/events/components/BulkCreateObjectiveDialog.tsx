import { useCallback, useEffect, useRef, useState } from 'react'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import CircularProgress from '@mui/material/CircularProgress'
import Dialog from '@mui/material/Dialog'
import DialogActions from '@mui/material/DialogActions'
import DialogContent from '@mui/material/DialogContent'
import DialogTitle from '@mui/material/DialogTitle'
import IconButton from '@mui/material/IconButton'
import Stack from '@mui/material/Stack'
import TextField from '@mui/material/TextField'
import Tooltip from '@mui/material/Tooltip'
import Typography from '@mui/material/Typography'
import AddIcon from '@mui/icons-material/Add'
import CheckCircleIcon from '@mui/icons-material/CheckCircle'
import DeleteOutlineIcon from '@mui/icons-material/DeleteOutlined'
import ErrorOutlineIcon from '@mui/icons-material/ErrorOutlined'
import { useCreateObjective } from '../hooks/useCreateObjective'
import type { EventGame } from '../../../types'

const ROW_STATUSES = ['idle', 'pending', 'success', 'error'] as const
type RowStatus = (typeof ROW_STATUSES)[number]
const [IDLE, PENDING, SUCCESS, ERROR] = ROW_STATUSES

interface ObjectiveRow {
  key: string
  name: string
  score: number
  category: string
  status: RowStatus
  error?: string
}

interface BulkCreateObjectiveDialogProps {
  open: boolean
  eventId: string
  game: EventGame
  onClose: () => void
}

const makeBlankRow = (): ObjectiveRow => ({
  key: crypto.randomUUID(),
  name: '',
  score: 0,
  category: '',
  status: IDLE,
})

const nameFieldId = (key: string) => `bulk-objective-name-${key}`

/**
 * Lets an event owner add several hand-authored objectives to a game in one
 * pass by looping the existing single-objective create mutation. No rule
 * builder here — use `CreateObjectiveDialog` for auto-completion rules.
 */
export const BulkCreateObjectiveDialog = ({
  open,
  eventId,
  game,
  onClose,
}: BulkCreateObjectiveDialogProps) => {
  const [rows, setRows] = useState<ObjectiveRow[]>([makeBlankRow()])
  const [submitting, setSubmitting] = useState(false)
  const pendingFocusKey = useRef<string | null>(null)
  const createObjective = useCreateObjective()

  const focusRow = (key: string) => {
    const el = document.getElementById(nameFieldId(key))
    if (el instanceof HTMLElement) el.focus()
  }

  const resetForm = useCallback(() => {
    setRows([makeBlankRow()])
    setSubmitting(false)
  }, [])

  const handleClose = useCallback(() => {
    if (submitting) return
    resetForm()
    onClose()
  }, [onClose, resetForm, submitting])

  const addRow = () => {
    const row = makeBlankRow()
    pendingFocusKey.current = row.key
    setRows((prev) => [...prev, row])
  }

  const removeRow = (key: string) => {
    setRows((prev) => (prev.length <= 1 ? prev : prev.filter((r) => r.key !== key)))
  }

  const updateRow = (key: string, patch: Partial<ObjectiveRow>) => {
    setRows((prev) => prev.map((r) => (r.key === key ? { ...r, ...patch } : r)))
  }

  useEffect(() => {
    if (!pendingFocusKey.current) return
    const key = pendingFocusKey.current
    pendingFocusKey.current = null
    focusRow(key)
  }, [rows])

  const isRowScoreValid = (row: ObjectiveRow) => Number.isInteger(row.score) && row.score >= 0
  const isRowValid = (row: ObjectiveRow) => row.name.trim().length > 0 && isRowScoreValid(row)
  const hasFailedRows = rows.some((r) => r.status === ERROR)
  const canSubmit =
    !submitting && rows.length > 0 && rows.every((r) => isRowValid(r) || r.status === SUCCESS)

  // Returns per-row outcomes for just this pass (keyed by row key) rather
  // than relying on a `setRows` updater's side effect, since that updater
  // isn't guaranteed to run synchronously — reading a variable it sets
  // right after calling `setRows` can observe a stale value.
  const runSubmitPass = async (): Promise<Map<string, RowStatus>> => {
    setSubmitting(true)
    setRows((prev) =>
      prev.map((r) => (r.status === SUCCESS ? r : { ...r, status: IDLE, error: undefined })),
    )

    const currentRows = rows.filter((r) => r.status !== SUCCESS)
    const results = new Map<string, RowStatus>()
    for (const row of currentRows) {
      updateRow(row.key, { status: PENDING })
      try {
        await createObjective.mutateAsync({
          eventId,
          eventGameId: game.eventGameId,
          payload: {
            name: row.name.trim(),
            score: row.score,
            category: row.category.trim() || undefined,
          },
        })
        updateRow(row.key, { status: SUCCESS, error: undefined })
        results.set(row.key, SUCCESS)
      } catch {
        updateRow(row.key, { status: ERROR, error: 'Failed to create objective.' })
        results.set(row.key, ERROR)
      }
    }

    setSubmitting(false)
    return results
  }

  const failedCount = rows.filter((r) => r.status === ERROR).length

  const onFormSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!canSubmit) return

    const results = await runSubmitPass()
    const allSucceeded = [...results.values()].every((status) => status === SUCCESS)

    if (allSucceeded) {
      resetForm()
      onClose()
    } else {
      setRows((prev) => {
        const remaining = prev.filter((r) => r.status !== SUCCESS)
        return remaining.length > 0 ? remaining : [makeBlankRow()]
      })
    }
  }

  return (
    <Dialog open={open} onClose={handleClose} fullWidth maxWidth="sm">
      <Box component="form" onSubmit={onFormSubmit}>
        <DialogTitle>Add objectives — {game.gameName}</DialogTitle>
        <DialogContent>
          <Stack spacing={1.5} sx={{ mt: 1 }}>
            <Typography variant="body2" color="text.secondary">
              Bulk-added objectives don't support auto-completion rules — use the single objective
              dialog for that.
            </Typography>

            <Box sx={{ maxHeight: 360, overflowY: 'auto', pr: 1 }}>
              <Stack spacing={1.5}>
                {rows.map((row) => (
                  <Box key={row.key}>
                    <Stack direction="row" spacing={1} sx={{ alignItems: 'flex-start' }}>
                      <TextField
                        id={nameFieldId(row.key)}
                        label="Objective name"
                        value={row.name}
                        onChange={(e) => updateRow(row.key, { name: e.target.value })}
                        slotProps={{ htmlInput: { maxLength: 200 } }}
                        required
                        disabled={submitting || row.status === SUCCESS}
                        error={row.status !== SUCCESS && row.name.trim().length === 0}
                        sx={{ flex: 1 }}
                        size="small"
                      />
                      <TextField
                        label="Score"
                        type="number"
                        value={row.score}
                        onChange={(e) => updateRow(row.key, { score: Number(e.target.value) })}
                        slotProps={{ htmlInput: { min: 0, step: 1 } }}
                        disabled={submitting || row.status === SUCCESS}
                        error={row.status !== SUCCESS && !isRowScoreValid(row)}
                        sx={{ width: 100 }}
                        size="small"
                      />
                      <TextField
                        label="Category"
                        value={row.category}
                        onChange={(e) => updateRow(row.key, { category: e.target.value })}
                        slotProps={{ htmlInput: { maxLength: 200 } }}
                        disabled={submitting || row.status === SUCCESS}
                        sx={{ flex: 1 }}
                        size="small"
                      />
                      <Box sx={{ pt: 1, minWidth: 32, textAlign: 'center' }}>
                        {row.status === PENDING && <CircularProgress size={16} />}
                        {row.status === SUCCESS && (
                          <CheckCircleIcon fontSize="small" color="success" />
                        )}
                        {row.status === ERROR && (
                          <Tooltip title={row.error ?? 'Failed'}>
                            <ErrorOutlineIcon fontSize="small" color="error" />
                          </Tooltip>
                        )}
                      </Box>
                      <IconButton
                        aria-label="Remove objective row"
                        onClick={() => removeRow(row.key)}
                        disabled={submitting || rows.length <= 1}
                        size="small"
                        sx={{ visibility: rows.length <= 1 ? 'hidden' : 'visible' }}
                      >
                        <DeleteOutlineIcon fontSize="small" />
                      </IconButton>
                    </Stack>
                    {row.status === ERROR && row.error && (
                      <Typography variant="caption" color="error" sx={{ pl: 0.5 }}>
                        {row.error}
                      </Typography>
                    )}
                  </Box>
                ))}
              </Stack>
            </Box>

            <Box>
              <Button startIcon={<AddIcon />} onClick={addRow} disabled={submitting} size="small">
                Add another objective
              </Button>
            </Box>
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={handleClose} color="inherit" disabled={submitting}>
            Cancel
          </Button>
          <Button type="submit" variant="contained" disabled={!canSubmit}>
            {submitting
              ? 'Creating…'
              : hasFailedRows
                ? `Retry failed (${failedCount})`
                : 'Create objectives'}
          </Button>
        </DialogActions>
      </Box>
    </Dialog>
  )
}
