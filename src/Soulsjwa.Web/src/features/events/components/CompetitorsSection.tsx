import { useState } from 'react'
import Box from '@mui/material/Box'
import Stack from '@mui/material/Stack'
import Typography from '@mui/material/Typography'
import Chip from '@mui/material/Chip'
import Button from '@mui/material/Button'
import IconButton from '@mui/material/IconButton'
import Paper from '@mui/material/Paper'
import Alert from '@mui/material/Alert'
import Avatar from '@mui/material/Avatar'
import Divider from '@mui/material/Divider'
import FormControlLabel from '@mui/material/FormControlLabel'
import Switch from '@mui/material/Switch'
import Tooltip from '@mui/material/Tooltip'
import DeleteIcon from '@mui/icons-material/Delete'
import PersonAddIcon from '@mui/icons-material/PersonAdd'
import { useRemoveCompetitor } from '../hooks/useRemoveCompetitor'
import { useSelfJoinEvent } from '../hooks/useSelfJoinEvent'
import { useUpdateCompetitor } from '../hooks/useUpdateCompetitor'
import { useAddModerator } from '../hooks/useAddModerator'
import { useRemoveModerator } from '../hooks/useRemoveModerator'
import { useSetLive } from '../hooks/useSetLive'
import { AddCompetitorDialog } from './AddCompetitorDialog'
import { TrialRunControl } from './TrialRunControl'
import { PlannedRunsSection } from '../../calendar/components/PlannedRunsSection'
import { UserPicker, type UserPickerOption } from '../../users/components/UserPicker'
import { ConfirmDialog } from '../../../components/ui'
import { canEditCompetitorInfo, canToggleFor } from '../eventDetail/permissions'
import type { EventCompetitor, EventGame, EventResponse, User } from '../../../types'

interface Props {
  event: EventResponse
  currentUser: User | undefined
}

/**
 * Owner / admin controls for the per-event competitor list. Adding happens in a
 * dialog (button sits next to "Join as competitor"); the roster is shown as a
 * responsive grid of cards optimised for scannability.
 */
export const CompetitorsSection = ({ event, currentUser }: Props) => {
  const canManage =
    !!currentUser && (currentUser.role === 'Admin' || currentUser.id === event.createdById)
  const canSelfJoin =
    !!currentUser &&
    !event.isArchived &&
    !event.isStarted &&
    !event.competitors.some((competitor) => competitor.userId === currentUser.id)

  const [addOpen, setAddOpen] = useState(false)
  const [competitorToRemove, setCompetitorToRemove] = useState<EventCompetitor | null>(null)

  const removeCompetitor = useRemoveCompetitor(event.id)
  const selfJoin = useSelfJoinEvent(event.id)
  const updateCompetitor = useUpdateCompetitor(event.id)
  const setLive = useSetLive(event.id)

  return (
    <Box component="section" aria-labelledby="competitors-heading">
      <Stack
        direction={{ xs: 'column', sm: 'row' }}
        spacing={2}
        sx={{ justifyContent: 'space-between', alignItems: { xs: 'stretch', sm: 'center' }, mb: 2 }}
      >
        <Box>
          <Typography variant="h5" component="h2" id="competitors-heading">
            Competitors ({event.competitors.length})
          </Typography>
          <Typography variant="body2" color="text.secondary">
            Manage the roster separately from games, objectives, and overlay settings.
          </Typography>
        </Box>
        <Stack direction="row" spacing={1} sx={{ alignSelf: { xs: 'flex-start', sm: 'center' } }}>
          {canSelfJoin && (
            <Button
              variant="outlined"
              onClick={() => selfJoin.mutate()}
              disabled={selfJoin.isPending}
            >
              Join as competitor
            </Button>
          )}
          {canManage && (
            <Button
              variant="contained"
              startIcon={<PersonAddIcon />}
              onClick={() => setAddOpen(true)}
            >
              Add competitor
            </Button>
          )}
        </Stack>
      </Stack>
      {selfJoin.isError && (
        <Alert severity="error" sx={{ mb: 2 }}>
          Failed to join event.
        </Alert>
      )}
      {event.competitors.length === 0 ? (
        <Typography color="text.secondary">No competitors yet.</Typography>
      ) : (
        <Box
          sx={{
            display: 'grid',
            gridTemplateColumns: { xs: '1fr', lg: 'repeat(2, minmax(0, 1fr))' },
            gap: 2,
          }}
        >
          {event.competitors.map((c) => (
            <CompetitorCard
              key={c.userId}
              eventId={event.id}
              competitor={c}
              games={event.games}
              allowTrialRuns={event.allowTrialRuns}
              canManageCompetitor={canManage}
              canManageModerators={currentUser?.role === 'Admin' || currentUser?.id === c.userId}
              canManageTrialRuns={currentUser?.role === 'Admin' || currentUser?.id === c.userId}
              canManagePlannedRuns={canEditCompetitorInfo(event, currentUser, c.userId)}
              canToggleLive={canToggleFor(event, currentUser, c.userId)}
              onToggleStreamer={(next) =>
                updateCompetitor.mutate({ userId: c.userId, isStreamer: next })
              }
              onToggleLive={(next) =>
                setLive.mutate({
                  isLive: next,
                  onBehalfOfUserId: c.userId !== currentUser?.id ? c.userId : undefined,
                })
              }
              onRemove={() => setCompetitorToRemove(c)}
            />
          ))}
        </Box>
      )}

      {canManage && (
        <AddCompetitorDialog open={addOpen} eventId={event.id} onClose={() => setAddOpen(false)} />
      )}
      <ConfirmDialog
        open={!!competitorToRemove}
        title={`Remove ${competitorToRemove?.displayName ?? 'competitor'}?`}
        description="They will lose access to this event and their event-scoped competitor data will be removed."
        confirmLabel="Remove competitor"
        pending={removeCompetitor.isPending}
        onCancel={() => setCompetitorToRemove(null)}
        onConfirm={() => {
          if (!competitorToRemove) return
          removeCompetitor.mutate(competitorToRemove.userId, {
            onSuccess: () => setCompetitorToRemove(null),
          })
        }}
      />
    </Box>
  )
}

interface CompetitorCardProps {
  eventId: string
  competitor: EventCompetitor
  games: EventGame[]
  allowTrialRuns: boolean
  canManageCompetitor: boolean
  canManageModerators: boolean
  canManageTrialRuns: boolean
  canManagePlannedRuns: boolean
  canToggleLive: boolean
  onToggleStreamer: (isStreamer: boolean) => void
  onToggleLive: (isLive: boolean) => void
  onRemove: () => void
}

const CompetitorCard = ({
  eventId,
  competitor,
  games,
  allowTrialRuns,
  canManageCompetitor,
  canManageModerators,
  canManageTrialRuns,
  canManagePlannedRuns,
  canToggleLive,
  onToggleStreamer,
  onToggleLive,
  onRemove,
}: CompetitorCardProps) => {
  const [newMod, setNewMod] = useState<UserPickerOption | null>(null)
  const [modErr, setModErr] = useState<string | null>(null)
  const [moderatorToRemove, setModeratorToRemove] = useState<{
    userId: string
    displayName: string
  } | null>(null)

  const addMod = useAddModerator(eventId, competitor.userId)
  const removeMod = useRemoveModerator(eventId, competitor.userId)

  const handleAddMod = () => {
    if (!newMod?.userId) return
    setModErr(null)
    addMod.mutate(newMod.userId, {
      onSuccess: () => setNewMod(null),
      onError: (err: unknown) => {
        const e = err as { response?: { data?: { detail?: string } } }
        setModErr(e?.response?.data?.detail ?? 'Failed to add moderator.')
      },
    })
  }

  return (
    <Paper variant="outlined" sx={{ p: { xs: 2, md: 2.5 }, height: '100%' }}>
      <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center' }}>
        <Avatar sx={{ width: 40, height: 40 }}>
          {competitor.displayName.charAt(0).toUpperCase()}
        </Avatar>
        <Box sx={{ minWidth: 0, flex: 1 }}>
          <Typography variant="subtitle1" sx={{ fontWeight: 600 }} noWrap>
            {competitor.displayName}
          </Typography>
          <Stack
            direction="row"
            spacing={1}
            sx={{ alignItems: 'center', mt: 0.5, flexWrap: 'wrap' }}
          >
            <Chip
              label={competitor.isLive ? 'Live' : 'Offline'}
              size="small"
              color={competitor.isLive ? 'success' : 'default'}
              variant="outlined"
            />
            {competitor.isStreamer && <Chip label="Streamer" size="small" color="primary" />}
          </Stack>
        </Box>
        {canManageCompetitor && (
          <Tooltip title={`Remove ${competitor.displayName}`}>
            <IconButton
              size="small"
              color="error"
              aria-label={`Remove competitor ${competitor.displayName}`}
              onClick={onRemove}
            >
              <DeleteIcon fontSize="small" />
            </IconButton>
          </Tooltip>
        )}
      </Stack>

      {canToggleLive && (
        <FormControlLabel
          sx={{ mt: 1, ml: 0, display: 'block' }}
          control={
            <Switch
              size="small"
              checked={competitor.isLive}
              onChange={(e) => onToggleLive(e.target.checked)}
            />
          }
          label={<Typography variant="body2">Live</Typography>}
        />
      )}

      {canManageCompetitor && (
        <FormControlLabel
          sx={{ mt: 1, ml: 0, display: 'block' }}
          control={
            <Switch
              size="small"
              checked={competitor.isStreamer}
              onChange={(e) => onToggleStreamer(e.target.checked)}
            />
          }
          label={<Typography variant="body2">Streamer — can delegate moderators</Typography>}
        />
      )}

      {canManageTrialRuns && allowTrialRuns && games.length > 0 && (
        <>
          <Divider sx={{ my: 1.5 }} />
          <Typography variant="body2" color="text.secondary" gutterBottom>
            Trial runs
          </Typography>
          <Stack spacing={1}>
            {games.map((game) => (
              <TrialRunControl
                key={game.eventGameId}
                eventId={eventId}
                eventGameId={game.eventGameId}
                gameName={game.gameName}
                userId={competitor.userId}
              />
            ))}
          </Stack>
        </>
      )}

      {games.length > 0 && (
        <>
          <Divider sx={{ my: 1.5 }} />
          <PlannedRunsSection
            eventId={eventId}
            userId={competitor.userId}
            games={games}
            canManage={canManagePlannedRuns}
          />
        </>
      )}

      {competitor.isStreamer && (
        <>
          <Divider sx={{ my: 1.5 }} />

          <Typography variant="body2" color="text.secondary" gutterBottom>
            Delegated moderators ({competitor.moderators.length})
          </Typography>

          {competitor.moderators.length > 0 ? (
            <Stack direction="row" spacing={1} sx={{ flexWrap: 'wrap', gap: 1, mb: 1 }}>
              {competitor.moderators.map((m) => (
                <Chip
                  key={m.userId}
                  label={m.displayName}
                  size="small"
                  onDelete={
                    canManageModerators
                      ? () =>
                          setModeratorToRemove({
                            userId: m.userId,
                            displayName: m.displayName,
                          })
                      : undefined
                  }
                />
              ))}
            </Stack>
          ) : (
            <Typography variant="body2" color="text.disabled" sx={{ mb: 1 }}>
              None.
            </Typography>
          )}
          <ConfirmDialog
            open={!!moderatorToRemove}
            title={`Remove ${moderatorToRemove?.displayName ?? 'moderator'}?`}
            description={`They will no longer be able to manage objective completions for ${competitor.displayName}.`}
            confirmLabel="Remove moderator"
            pending={removeMod.isPending}
            onCancel={() => setModeratorToRemove(null)}
            onConfirm={() => {
              if (!moderatorToRemove) return
              removeMod.mutate(moderatorToRemove.userId, {
                onSuccess: () => setModeratorToRemove(null),
              })
            }}
          />

          {canManageModerators && (
            <Stack
              direction={{ xs: 'column', sm: 'row' }}
              spacing={1}
              sx={{ alignItems: 'flex-start' }}
            >
              <Box sx={{ flex: 1, width: '100%' }}>
                <UserPicker value={newMod} onChange={setNewMod} label="Moderator" fullWidth />
              </Box>
              <Button
                size="small"
                variant="outlined"
                disabled={!newMod?.userId || addMod.isPending}
                onClick={handleAddMod}
                sx={{ mt: { xs: 0, sm: '4px' } }}
              >
                Delegate
              </Button>
            </Stack>
          )}
          {modErr && (
            <Alert severity="error" sx={{ mt: 1 }}>
              {modErr}
            </Alert>
          )}
        </>
      )}
    </Paper>
  )
}
