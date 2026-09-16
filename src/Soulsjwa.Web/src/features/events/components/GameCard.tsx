import { memo, useCallback, useEffect, useMemo, useState } from 'react'
import Alert from '@mui/material/Alert'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Chip from '@mui/material/Chip'
import IconButton from '@mui/material/IconButton'
import InputAdornment from '@mui/material/InputAdornment'
import MenuItem from '@mui/material/MenuItem'
import Pagination from '@mui/material/Pagination'
import Paper from '@mui/material/Paper'
import Stack from '@mui/material/Stack'
import TextField from '@mui/material/TextField'
import Tooltip from '@mui/material/Tooltip'
import Typography from '@mui/material/Typography'
import AddIcon from '@mui/icons-material/Add'
import DeleteIcon from '@mui/icons-material/Delete'
import DragIndicatorIcon from '@mui/icons-material/DragIndicator'
import EditIcon from '@mui/icons-material/Edit'
import PlayArrowIcon from '@mui/icons-material/PlayArrow'
import SearchIcon from '@mui/icons-material/Search'
import StopIcon from '@mui/icons-material/Stop'
import { BulkCreateObjectiveDialog } from './BulkCreateObjectiveDialog'
import { CreateObjectiveDialog } from './CreateObjectiveDialog'
import { EditEventGameDialog } from './EditEventGameDialog'
import { ImportPredefinedDialog } from './ImportPredefinedDialog'
import { ObjectiveItem } from './ObjectiveItem'
import { useDragReorder } from '../hooks/useDragReorder'
import { useReorderObjectives } from '../hooks/useReorderObjectives'
import type { EventGame, Objective } from '../../../types'

const OBJECTIVES_LOCKED_MESSAGE =
  'Objectives can only be added, edited, or deleted while the event is stopped.'
const GAME_NOT_STARTED_MESSAGE = 'Games can only be started while the event is running.'
const GAME_REMOVE_LOCKED_MESSAGE = 'Cannot remove games while the event is running.'

/** Categories per page, or 'all' to show every category on one page (required for drag-reordering). */
const GROUPS_PAGE_SIZE_OPTIONS = [5, 10, 'all'] as const

interface ObjectiveGroup {
  category: string | null
  objectives: Objective[]
}

const groupByCategory = (objectives: Objective[]): ObjectiveGroup[] => {
  const groups: ObjectiveGroup[] = []
  const indexByCategory = new Map<string | null, number>()
  for (const objective of objectives) {
    const key = objective.category ?? null
    let idx = indexByCategory.get(key)
    if (idx === undefined) {
      idx = groups.length
      indexByCategory.set(key, idx)
      groups.push({ category: key, objectives: [] })
    }
    groups[idx]!.objectives.push(objective)
  }
  return groups
}

interface CategoryObjectivesListProps {
  objectives: Objective[]
  canManage: boolean
  /**
   * Separate from `canManage`: drag-reordering is additionally turned off
   * while the objectives shown are a search-filtered or paginated subset
   * (dragging within a partial view can't express a real position), even
   * though edit/delete/complete controls stay available either way.
   */
  canReorder: boolean
  reorderPending: boolean
  completedObjectiveIds: Set<string>
  completedObjectiveTimes: Map<string, string>
  failedObjectiveIds: Set<string>
  failedObjectiveTimes: Map<string, string>
  canToggleCompletion: boolean
  canEditCompletionTimes: boolean
  toggleDisabled: boolean
  onToggleCompletion: (objectiveId: string, currentlyCompleted: boolean) => void
  onToggleFailure: (objectiveId: string, currentlyFailed: boolean) => void
  onEditObjective: (objective: Objective) => void
  onDeleteObjective: (objective: Objective) => void
  onEditCompletionTime: (objective: Objective) => void
  onReorder: (nextObjectives: Objective[]) => void
}

/**
 * Objectives within a single category group. Owns its own `useDragReorder`
 * instance so drag/drop stays scoped to this group even though every
 * category on a game card reorders independently.
 */
const CategoryObjectivesList = memo(function CategoryObjectivesList({
  objectives,
  canManage,
  canReorder,
  reorderPending,
  completedObjectiveIds,
  completedObjectiveTimes,
  failedObjectiveIds,
  failedObjectiveTimes,
  canToggleCompletion,
  canEditCompletionTimes,
  toggleDisabled,
  onToggleCompletion,
  onToggleFailure,
  onEditObjective,
  onDeleteObjective,
  onEditCompletionTime,
  onReorder,
}: CategoryObjectivesListProps) {
  const objectiveDrag = useDragReorder(objectives, onReorder)
  const canDrag = canManage && canReorder && !reorderPending

  return (
    <Stack component="ul" spacing={1} sx={{ listStyle: 'none', p: 0, m: 0 }}>
      {objectives.map((obj, objectiveIndex) => (
        <ObjectiveItem
          key={obj.id}
          objective={obj}
          isCompleted={completedObjectiveIds.has(obj.id)}
          completedAt={completedObjectiveTimes.get(obj.id) ?? null}
          isFailed={failedObjectiveIds.has(obj.id)}
          failedAt={failedObjectiveTimes.get(obj.id) ?? null}
          canToggle={canToggleCompletion}
          canManage={canManage}
          canEditTime={canEditCompletionTimes}
          disabled={toggleDisabled}
          onToggle={() => onToggleCompletion(obj.id, completedObjectiveIds.has(obj.id))}
          onToggleFailure={() => onToggleFailure(obj.id, failedObjectiveIds.has(obj.id))}
          onEdit={() => onEditObjective(obj)}
          onDelete={() => onDeleteObjective(obj)}
          onEditTime={() => onEditCompletionTime(obj)}
          dragHandleProps={canDrag ? objectiveDrag.getHandleProps(objectiveIndex) : undefined}
          dragRowProps={objectiveDrag.getRowProps(objectiveIndex)}
          dropIndicator={objectiveDrag.indicatorFor(objectiveIndex)}
          isDragging={objectiveDrag.isDraggingIndex(objectiveIndex)}
        />
      ))}
    </Stack>
  )
})

export interface GameCardProps {
  game: EventGame
  eventId: string
  connectorSupported: boolean
  /** True for the event creator or any admin — see `canManageEvent`. */
  canManageEvent: boolean
  /** False once the event has started (or been archived) — objectives are locked. */
  objectivesEditable: boolean
  /** Set of objective ids the current target user has already completed. */
  completedObjectiveIds: Set<string>
  completedObjectiveTimes: Map<string, string>
  /** Set of objective ids the current target user has already failed. */
  failedObjectiveIds: Set<string>
  failedObjectiveTimes: Map<string, string>
  /** True when toggling completion is allowed (target selected + permission). */
  canToggleCompletion: boolean
  /**
   * Why this game's official controls are read-only, or undefined when they
   * are not. Set whenever trial mode is on for the target competitor here:
   * the server refuses every official write while it is, whether the run is
   * recording (it would take the tick, which this official-only tab filters
   * back out) or dormant (it takes nothing at all).
   */
  trialBlockReason?: string
  canEditCompletionTimes: boolean
  /** Disables checkboxes while a mutation is pending or event is stopped. */
  toggleDisabled: boolean
  onToggleCompletion: (objectiveId: string, currentlyCompleted: boolean) => void
  onToggleFailure: (objectiveId: string, currentlyFailed: boolean) => void
  onEditObjective: (eventGameId: string, objective: Objective) => void
  onDeleteObjective: (eventGameId: string, objective: Objective) => void
  onEditCompletionTime: (eventGameId: string, objectiveId: string) => void
  isFormOpen: boolean
  onOpenForm: (eventGameId: string) => void
  onCloseForm: () => void
  onRemove: (eventGameId: string) => void
  onToggleEnabled: (eventGameId: string, enabled: boolean) => void
  /** True once the event has started — gates enable/add/remove game actions. */
  eventIsStarted: boolean
}

/**
 * One game within an event (shown one at a time behind the game tabs in
 * `EventGamesSection` — game-to-game reordering lives there, via the tab
 * strip's move buttons): title/metadata chips, its objective list grouped by
 * category with owner reordering, and the management-only affordances for
 * adding, importing, editing, enabling/disabling, and removing.
 */
export const GameCard = memo(function GameCard({
  game,
  eventId,
  connectorSupported,
  canManageEvent,
  objectivesEditable,
  completedObjectiveIds,
  completedObjectiveTimes,
  failedObjectiveIds,
  failedObjectiveTimes,
  canToggleCompletion,
  trialBlockReason,
  canEditCompletionTimes,
  toggleDisabled,
  onToggleCompletion,
  onToggleFailure,
  onEditObjective,
  onDeleteObjective,
  onEditCompletionTime,
  isFormOpen,
  onOpenForm,
  onCloseForm,
  onRemove,
  onToggleEnabled,
  eventIsStarted,
}: GameCardProps) {
  const handleOpen = useCallback(() => onOpenForm(game.eventGameId), [onOpenForm, game.eventGameId])
  const handleRemove = useCallback(() => onRemove(game.eventGameId), [onRemove, game.eventGameId])
  const handleToggle = useCallback(
    () => onToggleEnabled(game.eventGameId, !game.isEnabled),
    [onToggleEnabled, game.eventGameId, game.isEnabled],
  )
  const [importDialogOpen, setImportDialogOpen] = useState(false)
  const openImport = useCallback(() => setImportDialogOpen(true), [])
  const closeImport = useCallback(() => setImportDialogOpen(false), [])
  const [bulkDialogOpen, setBulkDialogOpen] = useState(false)
  const openBulk = useCallback(() => setBulkDialogOpen(true), [])
  const closeBulk = useCallback(() => setBulkDialogOpen(false), [])
  const [editDialogOpen, setEditDialogOpen] = useState(false)
  const openEdit = useCallback(() => setEditDialogOpen(true), [])
  const closeEdit = useCallback(() => setEditDialogOpen(false), [])

  const groups = useMemo(() => groupByCategory(game.objectives), [game.objectives])
  const reorderObjectives = useReorderObjectives(eventId)

  const submitGroups = useCallback(
    (nextGroups: ObjectiveGroup[]) => {
      const objectiveIds = nextGroups.flatMap((g) => g.objectives.map((o) => o.id))
      reorderObjectives.mutate({ eventGameId: game.eventGameId, objectiveIds })
    },
    [reorderObjectives, game.eventGameId],
  )

  const categoryDrag = useDragReorder(groups, submitGroups)

  const [search, setSearch] = useState('')
  const [groupsPageSize, setGroupsPageSize] =
    useState<(typeof GROUPS_PAGE_SIZE_OPTIONS)[number]>('all')
  const [page, setPage] = useState(1)

  const query = search.trim().toLowerCase()
  // A stable lookup back to each group's real position in `groups` — needed
  // because the searched view below rebuilds group objects (to filter their
  // objectives), so they're no longer the same references `categoryDrag` was
  // built from.
  const groupIndexByCategory = useMemo(() => {
    const m = new Map<string | null, number>()
    groups.forEach((g, i) => m.set(g.category, i))
    return m
  }, [groups])

  const searchedGroups = useMemo(() => {
    if (!query) return groups
    return groups
      .map((g) => ({
        ...g,
        objectives: g.objectives.filter((o) => o.name.toLowerCase().includes(query)),
      }))
      .filter((g) => g.objectives.length > 0)
  }, [groups, query])

  const pageCount =
    groupsPageSize === 'all' ? 1 : Math.max(1, Math.ceil(searchedGroups.length / groupsPageSize))
  useEffect(() => {
    setPage((p) => Math.min(p, pageCount))
  }, [pageCount])

  const visibleGroups =
    groupsPageSize === 'all'
      ? searchedGroups
      : searchedGroups.slice((page - 1) * groupsPageSize, page * groupsPageSize)

  // Reordering needs every category visible at once to make sense — disabled
  // the moment a search or a smaller page is narrowing what's on screen.
  const canDragCategories = !query && groupsPageSize === 'all'

  const handleObjectivesReorder = useCallback(
    (groupIndex: number, nextObjectives: Objective[]) => {
      const nextGroups = groups.map((g, i) =>
        i === groupIndex ? { ...g, objectives: nextObjectives } : g,
      )
      submitGroups(nextGroups)
    },
    [groups, submitGroups],
  )

  const canManage = canManageEvent && objectivesEditable
  const enableAction = !game.isEnabled
  const enableDisabled = enableAction && !eventIsStarted

  return (
    <Paper
      variant="outlined"
      sx={{
        p: { xs: 2, md: 2.5 },
        opacity: game.isEnabled ? 1 : 0.6,
      }}
    >
      <Stack
        direction={{ xs: 'column', sm: 'row' }}
        spacing={2}
        sx={{ justifyContent: 'space-between', alignItems: { xs: 'stretch', sm: 'flex-start' } }}
      >
        <Box sx={{ minWidth: 0 }}>
          <Stack
            direction="row"
            spacing={1}
            sx={{ alignItems: 'center', flexWrap: 'wrap', gap: 1 }}
          >
            <Typography variant="h6" component="h3">
              {game.gameName}
            </Typography>
            {game.knownGameName && game.knownGameName !== game.gameName && (
              <Chip label={game.knownGameName} size="small" variant="outlined" />
            )}
            {game.isCustomGame && (
              <Chip label="Custom" size="small" variant="outlined" color="secondary" />
            )}
            <Chip
              label={game.isEnabled ? 'Enabled' : 'Disabled'}
              size="small"
              color={game.isEnabled ? 'success' : 'default'}
              variant="outlined"
            />
          </Stack>
          {game.customGameDescription && (
            <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
              {game.customGameDescription}
            </Typography>
          )}
        </Box>
        {canManageEvent && (
          <Stack direction="row" spacing={0.5} sx={{ alignSelf: { xs: 'flex-start', sm: 'auto' } }}>
            <Tooltip title="Edit game">
              <IconButton size="small" onClick={openEdit} aria-label={`Edit ${game.gameName}`}>
                <EditIcon fontSize="small" />
              </IconButton>
            </Tooltip>
            <Tooltip
              title={
                enableDisabled
                  ? GAME_NOT_STARTED_MESSAGE
                  : game.isEnabled
                    ? 'Disable game'
                    : 'Enable game'
              }
            >
              <span>
                <Button
                  size="small"
                  color={game.isEnabled ? 'warning' : 'success'}
                  onClick={handleToggle}
                  disabled={enableDisabled}
                  aria-label={
                    game.isEnabled ? `Disable ${game.gameName}` : `Enable ${game.gameName}`
                  }
                  sx={{ minWidth: 'auto' }}
                >
                  {game.isEnabled ? (
                    <StopIcon fontSize="small" />
                  ) : (
                    <PlayArrowIcon fontSize="small" />
                  )}
                </Button>
              </span>
            </Tooltip>
            <Tooltip
              title={
                eventIsStarted
                  ? GAME_REMOVE_LOCKED_MESSAGE
                  : game.isCustomGame
                    ? 'Remove custom game'
                    : 'Remove game'
              }
            >
              <span>
                <Button
                  size="small"
                  color="error"
                  onClick={handleRemove}
                  disabled={eventIsStarted}
                  aria-label={`Remove ${game.gameName}`}
                  sx={{ minWidth: 'auto' }}
                >
                  <DeleteIcon fontSize="small" />
                </Button>
              </span>
            </Tooltip>
          </Stack>
        )}
      </Stack>

      {trialBlockReason && (
        <Alert severity="warning" variant="outlined" sx={{ mt: 1 }}>
          {trialBlockReason}
        </Alert>
      )}

      {game.objectives.length === 0 ? (
        <Typography variant="body2" color="text.secondary" sx={{ mt: 1 }}>
          No objectives.
        </Typography>
      ) : (
        <>
          <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1} sx={{ mt: 2 }}>
            <TextField
              size="small"
              placeholder="Search objectives…"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              slotProps={{
                input: {
                  startAdornment: (
                    <InputAdornment position="start">
                      <SearchIcon fontSize="small" />
                    </InputAdornment>
                  ),
                },
              }}
              sx={{ flex: 1 }}
            />
            <TextField
              select
              size="small"
              label="Groups per page"
              value={groupsPageSize}
              onChange={(e) =>
                setGroupsPageSize(
                  e.target.value === 'all' ? 'all' : (Number(e.target.value) as 5 | 10),
                )
              }
              sx={{ minWidth: 160 }}
            >
              {GROUPS_PAGE_SIZE_OPTIONS.map((n) => (
                <MenuItem key={n} value={n}>
                  {n === 'all' ? 'All' : n}
                </MenuItem>
              ))}
            </TextField>
          </Stack>
          {!canDragCategories && canManage && (
            <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mt: 0.5 }}>
              Clear the search and show all groups to drag-reorder categories.
            </Typography>
          )}
          {query && searchedGroups.length === 0 ? (
            <Typography variant="body2" color="text.secondary" sx={{ mt: 1 }}>
              No objectives match &quot;{search}&quot;.
            </Typography>
          ) : (
            <Stack spacing={2} sx={{ mt: 2 }}>
              {visibleGroups.map((group) => {
                const groupIndex = groupIndexByCategory.get(group.category) ?? 0
                const catIndicator = canDragCategories
                  ? categoryDrag.indicatorFor(groupIndex)
                  : null
                const categoryRowProps = categoryDrag.getRowProps(groupIndex)
                return (
                  <Box key={group.category ?? '__uncategorized__'}>
                    {catIndicator === 'before' && (
                      <Box sx={{ height: 2, bgcolor: 'primary.main', borderRadius: 1, mb: 1 }} />
                    )}
                    <Box
                      ref={categoryRowProps.ref}
                      onDragOver={categoryRowProps.onDragOver}
                      onDrop={categoryRowProps.onDrop}
                      sx={{
                        opacity:
                          canDragCategories && categoryDrag.isDraggingIndex(groupIndex) ? 0.4 : 1,
                      }}
                    >
                      <Stack direction="row" spacing={0.5} sx={{ alignItems: 'center', mb: 0.5 }}>
                        {canManage && canDragCategories && !reorderObjectives.isPending && (
                          <IconButton
                            size="small"
                            {...categoryDrag.getHandleProps(groupIndex)}
                            aria-label={`Reorder ${group.category ?? 'Uncategorized'} category`}
                          >
                            <DragIndicatorIcon fontSize="small" />
                          </IconButton>
                        )}
                        <Typography variant="subtitle2" color="text.secondary">
                          {group.category ?? 'Uncategorized'}
                        </Typography>
                      </Stack>
                      <CategoryObjectivesList
                        objectives={group.objectives}
                        canManage={canManage}
                        canReorder={canDragCategories}
                        reorderPending={reorderObjectives.isPending}
                        completedObjectiveIds={completedObjectiveIds}
                        completedObjectiveTimes={completedObjectiveTimes}
                        failedObjectiveIds={failedObjectiveIds}
                        failedObjectiveTimes={failedObjectiveTimes}
                        canToggleCompletion={canToggleCompletion}
                        canEditCompletionTimes={canEditCompletionTimes}
                        toggleDisabled={toggleDisabled}
                        onToggleCompletion={onToggleCompletion}
                        onToggleFailure={onToggleFailure}
                        onEditObjective={(obj) => onEditObjective(game.eventGameId, obj)}
                        onDeleteObjective={(obj) => onDeleteObjective(game.eventGameId, obj)}
                        onEditCompletionTime={(obj) =>
                          onEditCompletionTime(game.eventGameId, obj.id)
                        }
                        onReorder={(next) => handleObjectivesReorder(groupIndex, next)}
                      />
                    </Box>
                    {catIndicator === 'after' && (
                      <Box sx={{ height: 2, bgcolor: 'primary.main', borderRadius: 1, mt: 1 }} />
                    )}
                  </Box>
                )
              })}
            </Stack>
          )}
          {pageCount > 1 && (
            <Stack direction="row" sx={{ justifyContent: 'center', mt: 1 }}>
              <Pagination
                count={pageCount}
                page={page}
                onChange={(_e, value) => setPage(value)}
                size="small"
              />
            </Stack>
          )}
        </>
      )}

      {canManageEvent && (
        <Box sx={{ mt: 2, display: 'flex', flexWrap: 'wrap', gap: 1 }}>
          <Tooltip title={objectivesEditable ? '' : OBJECTIVES_LOCKED_MESSAGE}>
            <span>
              <Button
                size="small"
                startIcon={<AddIcon />}
                variant="outlined"
                onClick={handleOpen}
                disabled={!objectivesEditable}
              >
                Add Objective
              </Button>
            </span>
          </Tooltip>
          <Tooltip title={objectivesEditable ? '' : OBJECTIVES_LOCKED_MESSAGE}>
            <span>
              <Button
                size="small"
                variant="outlined"
                onClick={openBulk}
                disabled={!objectivesEditable}
              >
                Add multiple
              </Button>
            </span>
          </Tooltip>
          {connectorSupported && game.knownGameId != null && (
            <Tooltip
              title={
                objectivesEditable
                  ? 'Pick all or just selected predefined objectives to import. Already-imported ones are skipped.'
                  : OBJECTIVES_LOCKED_MESSAGE
              }
            >
              <span>
                <Button
                  size="small"
                  variant="outlined"
                  onClick={openImport}
                  disabled={!objectivesEditable}
                >
                  Import predefined…
                </Button>
              </span>
            </Tooltip>
          )}
        </Box>
      )}

      {canManageEvent && objectivesEditable && (
        <CreateObjectiveDialog
          open={isFormOpen}
          eventId={eventId}
          game={game}
          connectorSupported={connectorSupported}
          onClose={onCloseForm}
        />
      )}

      {canManageEvent && objectivesEditable && (
        <BulkCreateObjectiveDialog
          open={bulkDialogOpen}
          eventId={eventId}
          game={game}
          onClose={closeBulk}
        />
      )}

      {canManageEvent && objectivesEditable && connectorSupported && game.knownGameId != null && (
        <ImportPredefinedDialog
          open={importDialogOpen}
          eventId={eventId}
          eventGameId={game.eventGameId}
          knownGameId={game.knownGameId}
          gameName={game.gameName}
          onClose={closeImport}
        />
      )}

      {canManageEvent && (
        <EditEventGameDialog
          open={editDialogOpen}
          eventId={eventId}
          game={game}
          onClose={closeEdit}
        />
      )}
    </Paper>
  )
})
