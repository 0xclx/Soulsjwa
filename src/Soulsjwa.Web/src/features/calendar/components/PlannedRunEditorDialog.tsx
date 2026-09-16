import { useState } from 'react'
import Alert from '@mui/material/Alert'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Dialog from '@mui/material/Dialog'
import DialogActions from '@mui/material/DialogActions'
import DialogContent from '@mui/material/DialogContent'
import DialogTitle from '@mui/material/DialogTitle'
import MenuItem from '@mui/material/MenuItem'
import Stack from '@mui/material/Stack'
import TextField from '@mui/material/TextField'
import { useTheme } from '@mui/material/styles'
import { resolveCalendarEntryColor } from '../colorResolver'
import { getErrorDetail } from '../../../lib/getErrorDetail'
import { isoToLocalDateTime } from '../../events/eventDetail/permissions'
import {
  CALENDAR_ENTRY_COLORS,
  CALENDAR_ENTRY_COLOR_LABELS,
  type CalendarEntryColor,
  type PlannedRun,
} from '../../../types/calendar'
import type { EventGame } from '../../../types'

interface PlannedRunEditorDialogProps {
  open: boolean
  games: EventGame[]
  plannedRun: PlannedRun | null
  pending: boolean
  error: unknown
  onClose: () => void
  onSubmit: (
    eventGameId: string,
    startsAt: string,
    endsAt: string,
    color: CalendarEntryColor,
  ) => void
}

/**
 * Add/edit dialog for one competitor's planned run. Colour is
 * the same semantic slot picker as `CalendarEntryEditorDialog` — a live
 * theme-resolved swatch beside each option, never a free-form colour input —
 * so a run can be told apart from others on the global calendar without
 * relying on a fixed per-game color no one chose.
 */
export const PlannedRunEditorDialog = ({
  open,
  games,
  plannedRun,
  pending,
  error,
  onClose,
  onSubmit,
}: PlannedRunEditorDialogProps) => {
  const theme = useTheme()
  const [eventGameId, setEventGameId] = useState('')
  const [startsAt, setStartsAt] = useState('')
  const [endsAt, setEndsAt] = useState('')
  const [color, setColor] = useState<CalendarEntryColor>('Default')
  const [validationError, setValidationError] = useState<string | null>(null)

  const resetForPlannedRun = () => {
    setEventGameId(plannedRun?.eventGameId ?? games[0]?.eventGameId ?? '')
    setStartsAt(plannedRun ? isoToLocalDateTime(plannedRun.startsAt) : '')
    setEndsAt(plannedRun ? isoToLocalDateTime(plannedRun.endsAt) : '')
    setColor(plannedRun?.color ?? 'Default')
    setValidationError(null)
  }

  const handleSubmit = () => {
    if (!eventGameId) {
      setValidationError('A game is required.')
      return
    }
    if (!startsAt || !endsAt) {
      setValidationError('Start and end are both required.')
      return
    }
    const startIso = new Date(startsAt).toISOString()
    const endIso = new Date(endsAt).toISOString()
    if (new Date(endIso) <= new Date(startIso)) {
      setValidationError('End must be after start.')
      return
    }
    setValidationError(null)
    onSubmit(eventGameId, startIso, endIso, color)
  }

  return (
    <Dialog
      open={open}
      onClose={onClose}
      fullWidth
      maxWidth="sm"
      slotProps={{ transition: { onEnter: resetForPlannedRun } }}
    >
      <DialogTitle>{plannedRun ? 'Edit planned run' : 'New planned run'}</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ mt: 1 }}>
          <TextField
            select
            label="Game"
            value={eventGameId}
            onChange={(e) => setEventGameId(e.target.value)}
            disabled={!!plannedRun}
            fullWidth
          >
            {games.map((game) => (
              <MenuItem key={game.eventGameId} value={game.eventGameId}>
                {game.gameName}
              </MenuItem>
            ))}
          </TextField>

          <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2}>
            <TextField
              label="Starts"
              type="datetime-local"
              value={startsAt}
              onChange={(e) => setStartsAt(e.target.value)}
              required
              fullWidth
              slotProps={{ inputLabel: { shrink: true } }}
            />
            <TextField
              label="Ends"
              type="datetime-local"
              value={endsAt}
              onChange={(e) => setEndsAt(e.target.value)}
              required
              fullWidth
              slotProps={{ inputLabel: { shrink: true } }}
            />
          </Stack>

          <TextField
            select
            label="Colour"
            value={color}
            onChange={(e) => setColor(e.target.value as CalendarEntryColor)}
            fullWidth
          >
            {CALENDAR_ENTRY_COLORS.map((slot) => (
              <MenuItem key={slot} value={slot}>
                <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
                  <Box
                    sx={{
                      width: 14,
                      height: 14,
                      borderRadius: '50%',
                      bgcolor: resolveCalendarEntryColor(theme, slot),
                      border: 1,
                      borderColor: 'divider',
                    }}
                  />
                  <span>{CALENDAR_ENTRY_COLOR_LABELS[slot]}</span>
                </Stack>
              </MenuItem>
            ))}
          </TextField>

          {validationError && <Alert severity="error">{validationError}</Alert>}
          {!!error && (
            <Alert severity="error">{getErrorDetail(error, 'Failed to save planned run.')}</Alert>
          )}
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={pending}>
          Cancel
        </Button>
        <Button variant="contained" onClick={handleSubmit} disabled={pending}>
          Save
        </Button>
      </DialogActions>
    </Dialog>
  )
}
