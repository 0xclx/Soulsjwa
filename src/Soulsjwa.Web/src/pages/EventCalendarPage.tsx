import { useState } from 'react'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Chip from '@mui/material/Chip'
import IconButton from '@mui/material/IconButton'
import Paper from '@mui/material/Paper'
import Stack from '@mui/material/Stack'
import Typography from '@mui/material/Typography'
import DeleteIcon from '@mui/icons-material/Delete'
import EditIcon from '@mui/icons-material/Edit'
import AddIcon from '@mui/icons-material/Add'
import { useTheme } from '@mui/material/styles'
import { ConfirmDialog, EmptyState, ErrorMessage, LoadingState } from '../components/ui'
import { MarkdownView } from '../features/markdown/MarkdownView'
import { useEventRoute } from '../features/events/hooks/useEventRoute'
import { useCalendarEntries } from '../features/calendar/hooks/useCalendarEntries'
import { useCreateCalendarEntry } from '../features/calendar/hooks/useCreateCalendarEntry'
import { useUpdateCalendarEntry } from '../features/calendar/hooks/useUpdateCalendarEntry'
import { useDeleteCalendarEntry } from '../features/calendar/hooks/useDeleteCalendarEntry'
import { CalendarEntryEditorDialog } from '../features/calendar/components/CalendarEntryEditorDialog'
import { resolveCalendarEntryColor } from '../features/calendar/colorResolver'
import { CALENDAR_ENTRY_COLOR_LABELS, type CalendarEntry } from '../types/calendar'

const formatRange = (entry: CalendarEntry) => {
  const start = new Date(entry.startsAt)
  const end = new Date(entry.endsAt)
  const dateOpts: Intl.DateTimeFormatOptions = entry.isAllDay
    ? { dateStyle: 'medium' }
    : { dateStyle: 'medium', timeStyle: 'short' }
  return `${start.toLocaleString(undefined, dateOpts)} – ${end.toLocaleString(undefined, dateOpts)}`
}

/** An event's admin-authored calendar entries: public read, owner/admin write. */
export const EventCalendarPage = () => {
  const { eventId, canManage } = useEventRoute()
  const theme = useTheme()
  const { data: entries, isLoading, isError } = useCalendarEntries(eventId)
  const createEntry = useCreateCalendarEntry(eventId)
  const updateEntry = useUpdateCalendarEntry(eventId)
  const deleteEntry = useDeleteCalendarEntry(eventId)

  const [editorOpen, setEditorOpen] = useState(false)
  const [editingEntry, setEditingEntry] = useState<CalendarEntry | null>(null)
  const [entryToDelete, setEntryToDelete] = useState<CalendarEntry | null>(null)

  if (isLoading) return <LoadingState label="Loading calendar entries…" />
  if (isError || !entries) return <ErrorMessage message="Failed to load calendar entries." />

  const openCreate = () => {
    setEditingEntry(null)
    setEditorOpen(true)
  }

  const openEdit = (entry: CalendarEntry) => {
    setEditingEntry(entry)
    setEditorOpen(true)
  }

  const activeMutation = editingEntry ? updateEntry : createEntry

  return (
    <Box component="section" aria-labelledby="calendar-entries-heading">
      <Stack
        direction={{ xs: 'column', sm: 'row' }}
        spacing={2}
        sx={{ justifyContent: 'space-between', alignItems: { xs: 'stretch', sm: 'center' }, mb: 2 }}
      >
        <Typography variant="h5" component="h2" id="calendar-entries-heading">
          Calendar
        </Typography>
        {canManage && (
          <Button variant="contained" startIcon={<AddIcon />} onClick={openCreate}>
            Add entry
          </Button>
        )}
      </Stack>

      {entries.length === 0 ? (
        <EmptyState title="No calendar entries yet." />
      ) : (
        <Stack spacing={1.5}>
          {entries.map((entry) => (
            <Paper key={entry.id} variant="outlined" sx={{ p: 2 }}>
              <Stack direction="row" spacing={1.5} sx={{ alignItems: 'flex-start' }}>
                <Box
                  sx={{
                    width: 10,
                    height: 10,
                    borderRadius: '50%',
                    mt: 0.75,
                    flexShrink: 0,
                    bgcolor: resolveCalendarEntryColor(theme, entry.color),
                  }}
                />
                <Box sx={{ flex: 1, minWidth: 0 }}>
                  <Stack
                    direction="row"
                    spacing={1}
                    sx={{ alignItems: 'center', flexWrap: 'wrap' }}
                  >
                    <Typography variant="subtitle1" sx={{ fontWeight: 600 }}>
                      {entry.title}
                    </Typography>
                    {entry.isHighlighted && <Chip label="Highlight" size="small" color="warning" />}
                    <Chip
                      label={CALENDAR_ENTRY_COLOR_LABELS[entry.color]}
                      size="small"
                      variant="outlined"
                    />
                  </Stack>
                  <Typography variant="body2" color="text.secondary">
                    {formatRange(entry)}
                    {entry.isAllDay ? ' (all day)' : ''}
                  </Typography>
                  {entry.descriptionMarkdown && (
                    <Box sx={{ mt: 1 }}>
                      <MarkdownView source={entry.descriptionMarkdown} />
                    </Box>
                  )}
                </Box>
                {canManage && (
                  <Stack direction="row">
                    <IconButton
                      size="small"
                      aria-label={`Edit ${entry.title}`}
                      onClick={() => openEdit(entry)}
                    >
                      <EditIcon fontSize="small" />
                    </IconButton>
                    <IconButton
                      size="small"
                      color="error"
                      aria-label={`Delete ${entry.title}`}
                      onClick={() => setEntryToDelete(entry)}
                    >
                      <DeleteIcon fontSize="small" />
                    </IconButton>
                  </Stack>
                )}
              </Stack>
            </Paper>
          ))}
        </Stack>
      )}

      {canManage && (
        <CalendarEntryEditorDialog
          open={editorOpen}
          entry={editingEntry}
          pending={activeMutation.isPending}
          error={activeMutation.error}
          onClose={() => setEditorOpen(false)}
          onSubmit={(request) => {
            if (editingEntry) {
              updateEntry.mutate(
                { entryId: editingEntry.id, request, version: editingEntry.version },
                { onSuccess: () => setEditorOpen(false) },
              )
            } else {
              createEntry.mutate(request, { onSuccess: () => setEditorOpen(false) })
            }
          }}
        />
      )}

      <ConfirmDialog
        open={!!entryToDelete}
        title={`Delete "${entryToDelete?.title ?? ''}"?`}
        description="This calendar entry will be permanently removed."
        confirmLabel="Delete entry"
        pending={deleteEntry.isPending}
        onCancel={() => setEntryToDelete(null)}
        onConfirm={() => {
          if (!entryToDelete) return
          deleteEntry.mutate(entryToDelete.id, { onSuccess: () => setEntryToDelete(null) })
        }}
      />
    </Box>
  )
}
