import { useState } from 'react'
import Alert from '@mui/material/Alert'
import Box from '@mui/material/Box'
import Chip from '@mui/material/Chip'
import IconButton from '@mui/material/IconButton'
import Stack from '@mui/material/Stack'
import Typography from '@mui/material/Typography'
import AddIcon from '@mui/icons-material/Add'
import DeleteIcon from '@mui/icons-material/Delete'
import EditIcon from '@mui/icons-material/Edit'
import { useTheme } from '@mui/material/styles'
import { ConfirmDialog } from '../../../components/ui'
import { getErrorDetail } from '../../../lib/getErrorDetail'
import { resolveCalendarEntryColor } from '../colorResolver'
import { usePlannedRuns } from '../hooks/usePlannedRuns'
import { useCreatePlannedRun } from '../hooks/useCreatePlannedRun'
import { useUpdatePlannedRun } from '../hooks/useUpdatePlannedRun'
import { useDeletePlannedRun } from '../hooks/useDeletePlannedRun'
import { PlannedRunEditorDialog } from './PlannedRunEditorDialog'
import type { PlannedRun } from '../../../types/calendar'
import type { EventGame } from '../../../types'

interface PlannedRunsSectionProps {
  eventId: string
  userId: string
  games: EventGame[]
  canManage: boolean
}

const gameName = (games: EventGame[], eventGameId: string) =>
  games.find((g) => g.eventGameId === eventGameId)?.gameName ?? 'Unknown game'

/** A competitor's scheduled play sessions, editable from their tile. */
export const PlannedRunsSection = ({
  eventId,
  userId,
  games,
  canManage,
}: PlannedRunsSectionProps) => {
  const theme = useTheme()
  const { data: plannedRuns, isLoading } = usePlannedRuns(eventId, userId)
  const createRun = useCreatePlannedRun(eventId, userId)
  const updateRun = useUpdatePlannedRun(eventId, userId)
  const deleteRun = useDeletePlannedRun(eventId, userId)

  const [editorOpen, setEditorOpen] = useState(false)
  const [editingRun, setEditingRun] = useState<PlannedRun | null>(null)
  const [runToDelete, setRunToDelete] = useState<PlannedRun | null>(null)
  const [deleteError, setDeleteError] = useState<string | null>(null)

  if (isLoading || games.length === 0) return null

  const activeMutation = editingRun ? updateRun : createRun

  return (
    <Stack spacing={1}>
      <Stack
        direction="row"
        spacing={1}
        sx={{ alignItems: 'center', justifyContent: 'space-between' }}
      >
        <Typography variant="body2" color="text.secondary">
          Planned runs
        </Typography>
        {canManage && (
          <IconButton
            size="small"
            aria-label="Add planned run"
            onClick={() => {
              setEditingRun(null)
              setEditorOpen(true)
            }}
          >
            <AddIcon fontSize="small" />
          </IconButton>
        )}
      </Stack>

      {!plannedRuns || plannedRuns.length === 0 ? (
        <Typography variant="body2" color="text.disabled">
          None.
        </Typography>
      ) : (
        <Stack spacing={0.75}>
          {plannedRuns.map((run) => (
            <Stack
              key={run.id}
              direction="row"
              spacing={1}
              sx={{ alignItems: 'center', flexWrap: 'wrap' }}
            >
              <Box
                sx={{
                  width: 10,
                  height: 10,
                  borderRadius: '50%',
                  flexShrink: 0,
                  bgcolor: resolveCalendarEntryColor(theme, run.color),
                }}
              />
              <Chip label={gameName(games, run.eventGameId)} size="small" variant="outlined" />
              <Typography variant="caption" color="text.secondary">
                {new Date(run.startsAt).toLocaleString()} – {new Date(run.endsAt).toLocaleString()}
              </Typography>
              {canManage && (
                <>
                  <IconButton
                    size="small"
                    aria-label="Edit planned run"
                    onClick={() => {
                      setEditingRun(run)
                      setEditorOpen(true)
                    }}
                  >
                    <EditIcon fontSize="inherit" />
                  </IconButton>
                  <IconButton
                    size="small"
                    color="error"
                    aria-label="Delete planned run"
                    onClick={() => setRunToDelete(run)}
                  >
                    <DeleteIcon fontSize="inherit" />
                  </IconButton>
                </>
              )}
            </Stack>
          ))}
        </Stack>
      )}

      {deleteError && <Alert severity="error">{deleteError}</Alert>}

      {canManage && (
        <>
          <PlannedRunEditorDialog
            open={editorOpen}
            games={games}
            plannedRun={editingRun}
            pending={activeMutation.isPending}
            error={activeMutation.error}
            onClose={() => setEditorOpen(false)}
            onSubmit={(eventGameId, startsAt, endsAt, color) => {
              if (editingRun) {
                updateRun.mutate(
                  { plannedRunId: editingRun.id, request: { startsAt, endsAt, color } },
                  { onSuccess: () => setEditorOpen(false) },
                )
              } else {
                createRun.mutate(
                  { eventGameId, startsAt, endsAt, color },
                  { onSuccess: () => setEditorOpen(false) },
                )
              }
            }}
          />
          <ConfirmDialog
            open={!!runToDelete}
            title="Delete planned run?"
            description="This planned run will be permanently removed."
            confirmLabel="Delete"
            pending={deleteRun.isPending}
            onCancel={() => setRunToDelete(null)}
            onConfirm={() => {
              if (!runToDelete) return
              setDeleteError(null)
              deleteRun.mutate(runToDelete.id, {
                onSuccess: () => setRunToDelete(null),
                onError: (err) =>
                  setDeleteError(getErrorDetail(err, 'Failed to delete planned run.')),
              })
            }}
          />
        </>
      )}
    </Stack>
  )
}
