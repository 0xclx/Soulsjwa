import { useCallback, useMemo, useState } from 'react'
import Alert from '@mui/material/Alert'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Chip from '@mui/material/Chip'
import IconButton from '@mui/material/IconButton'
import MenuItem from '@mui/material/MenuItem'
import Stack from '@mui/material/Stack'
import Tab from '@mui/material/Tab'
import Tabs from '@mui/material/Tabs'
import TextField from '@mui/material/TextField'
import Tooltip from '@mui/material/Tooltip'
import Typography from '@mui/material/Typography'
import AddIcon from '@mui/icons-material/Add'
import ArrowBackIcon from '@mui/icons-material/ArrowBack'
import ArrowForwardIcon from '@mui/icons-material/ArrowForward'
import { ConfirmDialog, EmptyState, Surface } from '../../../components/ui'
import { canManageObjectives } from '../eventDetail/permissions'
import { useDeleteObjective } from '../hooks/useDeleteObjective'
import { useDisableEventGame } from '../hooks/useDisableEventGame'
import { useEnableEventGame } from '../hooks/useEnableEventGame'
import { useRemoveEventGame } from '../hooks/useRemoveEventGame'
import { useReorderEventGames } from '../hooks/useReorderEventGames'
import { AddGameDialog } from './AddGameDialog'
import { AddCustomGameDialog } from './AddCustomGameDialog'
import { GameCard } from './GameCard'
import { FailRemainingObjectivesButton, SELF_TARGET_NAME } from './FailRemainingObjectivesButton'
import type { useObjectiveCompletion } from '../hooks/useObjectiveCompletion'
import type { EventResponse, GameResponse, Objective } from '../../../types'

interface EventGamesSectionProps {
  event: EventResponse
  eventId: string
  /** True for the event creator or any admin — see `canManageEvent`. */
  canManage: boolean
  isAdmin: boolean
  /** Current viewer's id, used to mark their own completion-target option. */
  currentUserId?: string
  canEditCompletionTimes: boolean
  /** Known games catalog used by the "Add Game" dialog and connector support. */
  knownGames: GameResponse[]
  completion: ReturnType<typeof useObjectiveCompletion>
  onEditObjective: (eventGameId: string, objective: Objective) => void
  onEditCompletionTime: (eventGameId: string, objectiveId: string) => void
}

/** Why the bulk fail is unavailable on a disabled game: the server refuses official writes there. */
const DISABLED_GAME_HINT = 'This game is disabled, so nothing can be recorded for it.'

/**
 * Games tab: the objective-completion target selector, the list of games with
 * their objectives, and the owner-only controls for adding predefined/custom
 * games. Owns its game-management mutations and local form state.
 */
export const EventGamesSection = ({
  event,
  eventId,
  canManage,
  isAdmin,
  currentUserId,
  canEditCompletionTimes,
  knownGames,
  completion,
  onEditObjective,
  onEditCompletionTime,
}: EventGamesSectionProps) => {
  const {
    selfIsCompetitor,
    completionOptions,
    effectiveTarget,
    setCompletionTarget,
    completedObjectiveIds,
    completedObjectiveTimes,
    failedObjectiveIds,
    failedObjectiveTimes,
    trialBlockReasons,
    canToggleCompletion,
    toggleDisabled,
    completionError,
    setCompletionError,
    handleToggleCompletion,
    handleToggleFailure,
    handleFailRemaining,
    failRemainingPending,
  } = completion

  const removeEventGame = useRemoveEventGame(eventId)
  const enableEventGame = useEnableEventGame(eventId)
  const disableEventGame = useDisableEventGame(eventId)
  const deleteObjective = useDeleteObjective(eventId)
  const reorderEventGames = useReorderEventGames(eventId)

  const [showFormForGame, setShowFormForGame] = useState<string | null>(null)
  const [showAddGameDialog, setShowAddGameDialog] = useState(false)
  const [showCustomGameDialog, setShowCustomGameDialog] = useState(false)
  const [objectiveToDelete, setObjectiveToDelete] = useState<{
    gameId: string
    objective: Objective
  } | null>(null)
  const [gameToRemove, setGameToRemove] = useState<{ id: string; name: string } | null>(null)

  const objectivesEditable = canManageObjectives(event)

  const connectorSupportMap = useMemo(() => {
    const m = new Map<number, boolean>()
    knownGames.forEach((g) => m.set(g.id, g.connectorSupported))
    return m
  }, [knownGames])

  const handleOpenForm = useCallback((id: string) => setShowFormForGame(id), [])
  const handleCloseForm = useCallback(() => setShowFormForGame(null), [])

  const handleDeleteObjective = useCallback((gameId: string, objective: Objective) => {
    setObjectiveToDelete({ gameId, objective })
  }, [])

  const handleAddCustomGame = useCallback(() => setShowCustomGameDialog(true), [])

  const handleRemoveEventGame = useCallback(
    (gameId: string) => {
      const game = event.games.find((candidate) => candidate.eventGameId === gameId)
      if (game) setGameToRemove({ id: gameId, name: game.gameName })
    },
    [event.games],
  )

  const handleToggleEventGame = useCallback(
    async (gameId: string, enabled: boolean) => {
      if (enabled) {
        await enableEventGame.mutateAsync(gameId)
      } else {
        await disableEventGame.mutateAsync(gameId)
      }
    },
    [enableEventGame, disableEventGame],
  )

  // Which game's objectives are shown below the tab strip. Falls back to the
  // first game whenever the selection doesn't match one of this event's
  // current games (initial render, or the selected game was just removed).
  const [selectedGameId, setSelectedGameId] = useState<string | null>(null)
  const activeGameId =
    selectedGameId && event.games.some((g) => g.eventGameId === selectedGameId)
      ? selectedGameId
      : (event.games[0]?.eventGameId ?? null)
  const activeGameIndex = event.games.findIndex((g) => g.eventGameId === activeGameId)
  const activeGame = activeGameIndex >= 0 ? event.games[activeGameIndex]! : null

  const moveActiveGame = useCallback(
    (offset: -1 | 1) => {
      const from = activeGameIndex
      const to = from + offset
      if (from < 0 || to < 0 || to >= event.games.length) return
      const next = [...event.games]
      const [moved] = next.splice(from, 1)
      next.splice(to, 0, moved!)
      reorderEventGames.mutate(next.map((g) => g.eventGameId))
    },
    [activeGameIndex, event.games, reorderEventGames],
  )

  return (
    <Box component="section" aria-labelledby="games-heading">
      <Stack
        direction="row"
        spacing={2}
        sx={{ alignItems: 'center', flexWrap: 'wrap', mb: 1, gap: 1 }}
      >
        <Typography variant="h5" component="h2" id="games-heading">
          Games ({event.games.length})
        </Typography>
      </Stack>
      {completionError && (
        <Alert severity="error" onClose={() => setCompletionError(null)} sx={{ mb: 2 }}>
          {completionError}
        </Alert>
      )}
      {completionOptions.length > 0 && (
        <Surface>
          <Stack
            direction={{ xs: 'column', md: 'row' }}
            spacing={2}
            sx={{ justifyContent: 'space-between', alignItems: { xs: 'stretch', md: 'center' } }}
          >
            <Box>
              <Typography variant="h6">Objective completion</Typography>
              <Typography variant="body2" color="text.secondary">
                {isAdmin
                  ? 'Select the competitor whose objective status you want to update.'
                  : selfIsCompetitor
                    ? 'Updates apply to your own run.'
                    : 'Updates apply to streamer competitors delegated to you.'}
              </Typography>
            </Box>
            <TextField
              select
              size="small"
              label="Completion target"
              value={effectiveTarget}
              onChange={(e) => setCompletionTarget(e.target.value)}
              helperText="This controls which competitor the checkboxes affect."
              sx={{ minWidth: { xs: '100%', md: 260 } }}
            >
              {completionOptions.map((c) => (
                <MenuItem key={c.userId} value={c.userId}>
                  {c.displayName}
                  {currentUserId && c.userId === currentUserId ? ' (you)' : ''}
                </MenuItem>
              ))}
            </TextField>
          </Stack>
        </Surface>
      )}
      {event.games.length === 0 ? (
        <EmptyState
          title="No games added yet"
          description="Event owners can add predefined or custom games here."
        />
      ) : (
        <Box>
          <Stack direction="row" spacing={1} sx={{ alignItems: 'center', mb: 1 }}>
            <Tabs
              value={activeGameId}
              onChange={(_e, value) => setSelectedGameId(value)}
              variant="scrollable"
              allowScrollButtonsMobile
              aria-label="Games"
              sx={{ flex: 1, minHeight: 40, '& .MuiTab-root': { minHeight: 40, py: 0.5 } }}
            >
              {event.games.map((game) => (
                <Tab
                  key={game.eventGameId}
                  value={game.eventGameId}
                  label={
                    <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
                      <span>{game.gameName}</span>
                      {!game.isEnabled && <Chip label="Disabled" size="small" variant="outlined" />}
                    </Stack>
                  }
                />
              ))}
            </Tabs>
            {canManage && event.games.length > 1 && (
              <Stack direction="row" spacing={0.5}>
                <Tooltip title="Move earlier">
                  <span>
                    <IconButton
                      size="small"
                      disabled={activeGameIndex <= 0 || reorderEventGames.isPending}
                      onClick={() => moveActiveGame(-1)}
                      aria-label="Move game earlier"
                    >
                      <ArrowBackIcon fontSize="small" />
                    </IconButton>
                  </span>
                </Tooltip>
                <Tooltip title="Move later">
                  <span>
                    <IconButton
                      size="small"
                      disabled={
                        activeGameIndex < 0 ||
                        activeGameIndex >= event.games.length - 1 ||
                        reorderEventGames.isPending
                      }
                      onClick={() => moveActiveGame(1)}
                      aria-label="Move game later"
                    >
                      <ArrowForwardIcon fontSize="small" />
                    </IconButton>
                  </span>
                </Tooltip>
              </Stack>
            )}
          </Stack>
          {activeGame && canToggleCompletion && effectiveTarget && (
            <Stack direction="row" sx={{ justifyContent: 'flex-end', mb: 1 }}>
              <FailRemainingObjectivesButton
                gameName={activeGame.gameName}
                remainingCount={
                  activeGame.objectives.filter(
                    (o) => !completedObjectiveIds.has(o.id) && !failedObjectiveIds.has(o.id),
                  ).length
                }
                targetName={
                  effectiveTarget === currentUserId
                    ? SELF_TARGET_NAME
                    : (completionOptions.find((c) => c.userId === effectiveTarget)?.displayName ??
                      'this competitor')
                }
                disabled={
                  toggleDisabled ||
                  !activeGame.isEnabled ||
                  trialBlockReasons.has(activeGame.eventGameId)
                }
                disabledHint={
                  trialBlockReasons.get(activeGame.eventGameId) ??
                  (!activeGame.isEnabled ? DISABLED_GAME_HINT : undefined)
                }
                pending={failRemainingPending}
                onConfirm={() => handleFailRemaining(activeGame.eventGameId)}
              />
            </Stack>
          )}
          {activeGame && (
            <GameCard
              key={activeGame.eventGameId}
              game={activeGame}
              eventId={eventId}
              connectorSupported={
                activeGame.knownGameId != null
                  ? (connectorSupportMap.get(activeGame.knownGameId) ?? false)
                  : false
              }
              canManageEvent={canManage}
              objectivesEditable={objectivesEditable}
              completedObjectiveIds={completedObjectiveIds}
              completedObjectiveTimes={completedObjectiveTimes}
              failedObjectiveIds={failedObjectiveIds}
              failedObjectiveTimes={failedObjectiveTimes}
              canToggleCompletion={
                canToggleCompletion && !trialBlockReasons.has(activeGame.eventGameId)
              }
              trialBlockReason={trialBlockReasons.get(activeGame.eventGameId)}
              canEditCompletionTimes={canEditCompletionTimes}
              toggleDisabled={toggleDisabled}
              onToggleCompletion={handleToggleCompletion(activeGame.eventGameId)}
              onToggleFailure={handleToggleFailure(activeGame.eventGameId)}
              onEditObjective={onEditObjective}
              onDeleteObjective={handleDeleteObjective}
              onEditCompletionTime={onEditCompletionTime}
              isFormOpen={showFormForGame === activeGame.eventGameId}
              onOpenForm={handleOpenForm}
              onCloseForm={handleCloseForm}
              onRemove={handleRemoveEventGame}
              onToggleEnabled={handleToggleEventGame}
              eventIsStarted={event.isStarted}
            />
          )}
        </Box>
      )}

      {canManage && (
        <Box sx={{ mt: 2 }}>
          <Stack direction="row" spacing={1} sx={{ flexWrap: 'wrap', gap: 1 }}>
            <Tooltip title={event.isStarted ? 'Cannot add games while the event is running.' : ''}>
              <span>
                <Button
                  startIcon={<AddIcon />}
                  variant="outlined"
                  size="small"
                  onClick={() => setShowAddGameDialog(true)}
                  disabled={event.isStarted}
                >
                  Add Game
                </Button>
              </span>
            </Tooltip>
            <Tooltip title={event.isStarted ? 'Cannot add games while the event is running.' : ''}>
              <span>
                <Button
                  startIcon={<AddIcon />}
                  variant="outlined"
                  size="small"
                  onClick={handleAddCustomGame}
                  disabled={event.isStarted}
                >
                  Add Custom Game
                </Button>
              </span>
            </Tooltip>
          </Stack>
        </Box>
      )}

      <AddGameDialog
        open={showAddGameDialog}
        event={event}
        games={knownGames}
        onClose={() => setShowAddGameDialog(false)}
      />
      <AddCustomGameDialog
        open={showCustomGameDialog}
        eventId={eventId}
        onClose={() => setShowCustomGameDialog(false)}
      />
      <ConfirmDialog
        open={!!objectiveToDelete}
        title={`Delete ${objectiveToDelete?.objective.name ?? 'objective'}?`}
        description="The objective and every competitor completion recorded for it will be permanently removed."
        confirmLabel="Delete objective"
        pending={deleteObjective.isPending}
        onCancel={() => setObjectiveToDelete(null)}
        onConfirm={() => {
          if (!objectiveToDelete) return
          deleteObjective.mutate(
            {
              eventGameId: objectiveToDelete.gameId,
              objectiveId: objectiveToDelete.objective.id,
            },
            { onSuccess: () => setObjectiveToDelete(null) },
          )
        }}
      />
      <ConfirmDialog
        open={!!gameToRemove}
        title={`Remove ${gameToRemove?.name ?? 'game'}?`}
        description="The game, its objectives, and their completion records will be permanently removed from this event."
        confirmLabel="Remove game"
        pending={removeEventGame.isPending}
        onCancel={() => setGameToRemove(null)}
        onConfirm={() => {
          if (!gameToRemove) return
          removeEventGame.mutate(gameToRemove.id, { onSuccess: () => setGameToRemove(null) })
        }}
      />
    </Box>
  )
}
