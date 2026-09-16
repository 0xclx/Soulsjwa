import { useState } from 'react'
import Alert from '@mui/material/Alert'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Dialog from '@mui/material/Dialog'
import DialogActions from '@mui/material/DialogActions'
import DialogContent from '@mui/material/DialogContent'
import DialogTitle from '@mui/material/DialogTitle'
import FormControlLabel from '@mui/material/FormControlLabel'
import MenuItem from '@mui/material/MenuItem'
import Stack from '@mui/material/Stack'
import Switch from '@mui/material/Switch'
import TextField from '@mui/material/TextField'
import { useTheme } from '@mui/material/styles'
import { MarkdownEditor } from '../../markdown/MarkdownEditor'
import { ImageUploadField } from '../../media/components/ImageUploadField'
import { resolveCalendarEntryColor } from '../colorResolver'
import { getErrorDetail } from '../../../lib/getErrorDetail'
import { isoToLocalDateTime } from '../../events/eventDetail/permissions'
import {
  CALENDAR_ENTRY_COLORS,
  CALENDAR_ENTRY_COLOR_LABELS,
  type CalendarEntry,
  type CalendarEntryColor,
  type UpsertCalendarEntryRequest,
} from '../../../types/calendar'
import type { UploadMediaResponse } from '../../../types'

const MAX_TITLE_LENGTH = 120

const toUploadStub = (assetId: string, url: string): UploadMediaResponse => ({
  assetId,
  url,
  width: 0,
  height: 0,
  byteSize: 0,
  contentType: '',
})

const toDateOnly = (isoValue: string) => isoValue.slice(0, 10)

interface CalendarEntryEditorDialogProps {
  open: boolean
  entry: CalendarEntry | null
  pending: boolean
  error: unknown
  onClose: () => void
  onSubmit: (request: UpsertCalendarEntryRequest) => void
}

/**
 * Admin/owner editor for one calendar entry. Colour is always a
 * semantic slot selector, never a free-form colour input — the swatch next
 * to each option is a live preview resolved from the active MUI theme.
 */
export const CalendarEntryEditorDialog = ({
  open,
  entry,
  pending,
  error,
  onClose,
  onSubmit,
}: CalendarEntryEditorDialogProps) => {
  const theme = useTheme()
  const [title, setTitle] = useState('')
  const [description, setDescription] = useState('')
  const [startsAt, setStartsAt] = useState('')
  const [endsAt, setEndsAt] = useState('')
  const [isAllDay, setIsAllDay] = useState(false)
  const [isHighlighted, setIsHighlighted] = useState(false)
  const [color, setColor] = useState<CalendarEntryColor>('Default')
  const [image, setImage] = useState<UploadMediaResponse | null>(null)
  const [validationError, setValidationError] = useState<string | null>(null)

  const resetForEntry = () => {
    setTitle(entry?.title ?? '')
    setDescription(entry?.descriptionMarkdown ?? '')
    setIsAllDay(entry?.isAllDay ?? false)
    setIsHighlighted(entry?.isHighlighted ?? false)
    setColor(entry?.color ?? 'Default')
    setImage(
      entry?.imageAssetId && entry?.imageUrl
        ? toUploadStub(entry.imageAssetId, entry.imageUrl)
        : null,
    )
    setStartsAt(
      entry
        ? entry.isAllDay
          ? toDateOnly(entry.startsAt)
          : isoToLocalDateTime(entry.startsAt)
        : '',
    )
    setEndsAt(
      entry ? (entry.isAllDay ? toDateOnly(entry.endsAt) : isoToLocalDateTime(entry.endsAt)) : '',
    )
    setValidationError(null)
  }

  const handleAllDayToggle = (checked: boolean) => {
    setIsAllDay(checked)
    if (checked) {
      if (startsAt) setStartsAt(startsAt.slice(0, 10))
      if (endsAt) setEndsAt(endsAt.slice(0, 10))
    }
  }

  const handleSubmit = () => {
    if (!title.trim()) {
      setValidationError('Title is required.')
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
    onSubmit({
      title: title.trim(),
      descriptionMarkdown: description.trim() ? description : null,
      startsAt: startIso,
      endsAt: endIso,
      isAllDay,
      isHighlighted,
      color,
      imageAssetId: image?.assetId ?? null,
    })
  }

  return (
    <Dialog
      open={open}
      onClose={onClose}
      fullWidth
      maxWidth="sm"
      slotProps={{ transition: { onEnter: resetForEntry } }}
    >
      <DialogTitle>{entry ? 'Edit calendar entry' : 'New calendar entry'}</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ mt: 1 }}>
          <TextField
            label="Title"
            value={title}
            onChange={(e) => setTitle(e.target.value)}
            required
            fullWidth
            slotProps={{ htmlInput: { maxLength: MAX_TITLE_LENGTH } }}
            helperText={`${title.length}/${MAX_TITLE_LENGTH}`}
          />

          <MarkdownEditor
            value={description}
            onChange={setDescription}
            label="Description (optional)"
            disabled={pending}
          />

          <FormControlLabel
            control={
              <Switch checked={isAllDay} onChange={(e) => handleAllDayToggle(e.target.checked)} />
            }
            label="All day"
          />

          <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2}>
            <TextField
              label="Starts"
              type={isAllDay ? 'date' : 'datetime-local'}
              value={startsAt}
              onChange={(e) => setStartsAt(e.target.value)}
              required
              fullWidth
              slotProps={{ inputLabel: { shrink: true } }}
            />
            <TextField
              label="Ends"
              type={isAllDay ? 'date' : 'datetime-local'}
              value={endsAt}
              onChange={(e) => setEndsAt(e.target.value)}
              required
              fullWidth
              slotProps={{ inputLabel: { shrink: true } }}
            />
          </Stack>

          <FormControlLabel
            control={
              <Switch
                checked={isHighlighted}
                onChange={(e) => setIsHighlighted(e.target.checked)}
              />
            }
            label="Highlight"
          />

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

          <ImageUploadField value={image} onChange={setImage} disabled={pending} />

          {validationError && <Alert severity="error">{validationError}</Alert>}
          {!!error && (
            <Alert severity="error">{getErrorDetail(error, 'Failed to save entry.')}</Alert>
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
