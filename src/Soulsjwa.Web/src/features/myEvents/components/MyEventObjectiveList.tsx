import { Fragment, useMemo, useState } from 'react'
import Box from '@mui/material/Box'
import Checkbox from '@mui/material/Checkbox'
import Chip from '@mui/material/Chip'
import Divider from '@mui/material/Divider'
import IconButton from '@mui/material/IconButton'
import InputAdornment from '@mui/material/InputAdornment'
import MenuItem from '@mui/material/MenuItem'
import Pagination from '@mui/material/Pagination'
import Stack from '@mui/material/Stack'
import TextField from '@mui/material/TextField'
import Tooltip from '@mui/material/Tooltip'
import Typography from '@mui/material/Typography'
import CancelIcon from '@mui/icons-material/Cancel'
import ReplayIcon from '@mui/icons-material/Replay'
import SearchIcon from '@mui/icons-material/Search'
import {
  TRIAL_RUN_CONTROL_SUFFIX,
  TRIAL_RUN_LIST_LABEL,
  TRIAL_TOOLTIP,
} from '../../events/scoreboard/trialPresentation'
import {
  FailRemainingObjectivesButton,
  SELF_TARGET_NAME,
} from '../../events/components/FailRemainingObjectivesButton'
import type { MyEventGame, MyEventObjective } from '../../../types'

/** Fallback group label for objectives that have no category set. */
const UNCATEGORIZED_LABEL = 'Other'

const PAGE_SIZE_OPTIONS = [10, 25, 50, 100] as const

type GroupMode = 'category' | 'none'

/**
 * Groups objectives by category (a boss objective's category is its
 * in-game location), preserving first-seen order of both categories and the
 * objectives within each — mirrors `ScoreboardGameBreakdown`'s grouping so
 * "by location" reads the same way everywhere it appears.
 */
function groupByCategory(objectives: MyEventObjective[]): Array<[string, MyEventObjective[]]> {
  const byCategory = new Map<string, MyEventObjective[]>()
  for (const objective of objectives) {
    const category = objective.category?.trim() || UNCATEGORIZED_LABEL
    const bucket = byCategory.get(category)
    if (bucket) bucket.push(objective)
    else byCategory.set(category, [objective])
  }
  return Array.from(byCategory)
}

interface MyEventObjectiveListProps {
  games: MyEventGame[]
  /** Per-game gate: a game whose trial is recording is read-only here. */
  canToggle: (game: MyEventGame) => boolean
  /** Explains, per game, why its controls are disabled. */
  disabledHint?: (game: MyEventGame) => string | undefined
  isPending: boolean
  /**
   * This list writes to a trial run rather than the official record. Marked on
   * the list itself — per game heading and in every control's accessible name —
   * because the panel's own trial cues scroll out of view and the rows are
   * otherwise indistinguishable from the official ones.
   */
  isTrial?: boolean
  onToggleCompleted: (game: MyEventGame, objectiveId: string, completed: boolean) => void
  onToggleFailed: (game: MyEventGame, objectiveId: string, failed: boolean) => void
  /**
   * Fails every objective of the game still pending. Offered per game heading
   * when given and the game is editable; the confirmation is the button's.
   */
  onFailRemaining?: (game: MyEventGame) => Promise<unknown>
  failRemainingPending?: boolean
  /** Whose record the list edits, for the bulk-fail confirmation. */
  targetName?: string
}

/**
 * A competitor's objectives grouped by game, with the complete/fail controls.
 * Shared by the regular objectives tab, the Trial tab, and delegated
 * competitors so every one of those gets the same search, grouping and
 * pagination controls for free — a long list (a full boss catalog can run
 * past 200 entries) is otherwise unscannable.
 */
export const MyEventObjectiveList = ({
  games,
  canToggle,
  disabledHint,
  isPending,
  isTrial = false,
  onToggleCompleted,
  onToggleFailed,
  onFailRemaining,
  failRemainingPending = false,
  targetName = SELF_TARGET_NAME,
}: MyEventObjectiveListProps) => {
  const [search, setSearch] = useState('')
  const [groupMode, setGroupMode] = useState<GroupMode>('category')
  const [pageSize, setPageSize] = useState<number>(PAGE_SIZE_OPTIONS[1])

  const query = search.trim().toLowerCase()
  const filteredGames = useMemo(
    () =>
      games.map((game) => ({
        game,
        objectives: query
          ? game.objectives.filter((o) => o.name.toLowerCase().includes(query))
          : game.objectives,
      })),
    [games, query],
  )
  const hasAnyMatch = filteredGames.some(({ objectives }) => objectives.length > 0)

  return (
    <Stack spacing={2}>
      <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1}>
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
          label="Group by"
          value={groupMode}
          onChange={(e) => setGroupMode(e.target.value as GroupMode)}
          sx={{ minWidth: 180 }}
        >
          <MenuItem value="category">Location / category</MenuItem>
          <MenuItem value="none">None</MenuItem>
        </TextField>
        <TextField
          select
          size="small"
          label="Page size"
          value={pageSize}
          onChange={(e) => setPageSize(Number(e.target.value))}
          sx={{ minWidth: 120 }}
        >
          {PAGE_SIZE_OPTIONS.map((n) => (
            <MenuItem key={n} value={n}>
              {n}
            </MenuItem>
          ))}
        </TextField>
      </Stack>

      {query && !hasAnyMatch && (
        <Typography variant="body2" color="text.secondary">
          No objectives match &quot;{search}&quot;.
        </Typography>
      )}

      {filteredGames.map(({ game, objectives }) => {
        if (objectives.length === 0) return null
        const editable = canToggle(game)
        const hint = disabledHint?.(game)
        return (
          <MyEventObjectiveGameSection
            key={game.gameId}
            game={game}
            objectives={objectives}
            editable={editable}
            hint={hint}
            isPending={isPending}
            isTrial={isTrial}
            groupMode={groupMode}
            pageSize={pageSize}
            onToggleCompleted={onToggleCompleted}
            onToggleFailed={onToggleFailed}
            onFailRemaining={onFailRemaining}
            failRemainingPending={failRemainingPending}
            targetName={targetName}
          />
        )
      })}
    </Stack>
  )
}

interface MyEventObjectiveGameSectionProps {
  game: MyEventGame
  /** Already search-filtered objectives for this game. */
  objectives: MyEventObjective[]
  editable: boolean
  hint: string | undefined
  isPending: boolean
  isTrial: boolean
  groupMode: GroupMode
  pageSize: number
  onToggleCompleted: (game: MyEventGame, objectiveId: string, completed: boolean) => void
  onToggleFailed: (game: MyEventGame, objectiveId: string, failed: boolean) => void
  onFailRemaining?: (game: MyEventGame) => Promise<unknown>
  failRemainingPending: boolean
  targetName: string
}

/**
 * One game's objective list: paginated over the (search-filtered) flat list
 * so the page count stays meaningful, with the current page's items then
 * grouped by category for display — a category can span a page boundary,
 * which is the accepted trade-off for keeping "page 2 of 5" a stable, simple
 * number instead of one that shifts with how categories happen to bucket.
 */
const MyEventObjectiveGameSection = ({
  game,
  objectives,
  editable,
  hint,
  isPending,
  isTrial,
  groupMode,
  pageSize,
  onToggleCompleted,
  onToggleFailed,
  onFailRemaining,
  failRemainingPending,
  targetName,
}: MyEventObjectiveGameSectionProps) => {
  const [page, setPage] = useState(1)
  const pageCount = Math.max(1, Math.ceil(objectives.length / pageSize))

  // A narrower search or a smaller page size can leave the stored page past
  // the end of the new result set. Clamped on read rather than synced back
  // through an effect: deriving it lands on the last page in the same render
  // the result set shrank, where a setState in an effect would render a blank
  // page first and then correct itself (React 19's
  // `react-hooks/set-state-in-effect` rule).
  const currentPage = Math.min(page, pageCount)

  const pageItems = objectives.slice((currentPage - 1) * pageSize, currentPage * pageSize)
  const groups: Array<[string | null, MyEventObjective[]]> =
    groupMode === 'category' ? groupByCategory(pageItems) : [[null, pageItems]]

  return (
    <Box>
      <Stack direction="row" spacing={1} sx={{ alignItems: 'center', mb: 0.5 }}>
        <Typography component="h3" variant="subtitle1" sx={{ fontWeight: 700 }}>
          {game.gameName}
        </Typography>
        {isTrial && (
          <Chip
            label={TRIAL_RUN_LIST_LABEL}
            size="small"
            color="warning"
            variant="outlined"
            title={TRIAL_TOOLTIP}
          />
        )}
        <Typography variant="caption" color="text.secondary">
          {objectives.length} objective{objectives.length === 1 ? '' : 's'}
        </Typography>
        {editable && onFailRemaining && (
          <Box sx={{ ml: 'auto' }}>
            <FailRemainingObjectivesButton
              gameName={game.gameName}
              // Counted over the whole game, not the search-filtered page:
              // the write is game-wide whatever the list currently shows.
              remainingCount={game.objectives.filter((o) => !o.completed && !o.failed).length}
              targetName={targetName}
              isTrial={isTrial}
              disabled={isPending}
              pending={failRemainingPending}
              onConfirm={() => onFailRemaining(game)}
            />
          </Box>
        )}
      </Stack>
      {!editable && hint && (
        <Typography variant="caption" color="text.secondary" sx={{ display: 'block', mb: 0.5 }}>
          {hint}
        </Typography>
      )}
      {groups.map(([category, items]) => (
        <Fragment key={category ?? 'flat'}>
          {groupMode === 'category' && (
            <Typography
              variant="caption"
              sx={{ display: 'block', mt: 1, mb: 0.25, fontWeight: 600, color: 'text.secondary' }}
            >
              {category}
            </Typography>
          )}
          <Stack component="ul" divider={<Divider />} sx={{ p: 0, m: 0 }}>
            {items.map((objective) => (
              <Stack
                component="li"
                key={objective.objectiveId}
                direction="row"
                spacing={1}
                sx={{ alignItems: 'center', py: 0.75 }}
              >
                <Checkbox
                  checked={objective.completed}
                  disabled={!editable || isPending || objective.failed}
                  onChange={() =>
                    onToggleCompleted(game, objective.objectiveId, objective.completed)
                  }
                  slotProps={{
                    input: {
                      'aria-label': [
                        objective.completed ? 'Uncomplete' : 'Complete',
                        objective.name,
                        isTrial ? TRIAL_RUN_CONTROL_SUFFIX : '',
                      ]
                        .filter(Boolean)
                        .join(' '),
                    },
                  }}
                />
                <Typography
                  sx={{
                    flex: 1,
                    textDecoration:
                      objective.completed || objective.failed ? 'line-through' : 'none',
                    color: objective.failed ? 'error.main' : undefined,
                  }}
                >
                  {objective.name}
                </Typography>
                {objective.failed && (
                  <Chip label="Failed" size="small" color="error" variant="outlined" />
                )}
                {editable && !objective.completed && (
                  <Tooltip title={objective.failed ? 'Reset' : 'Mark failed'}>
                    <span>
                      <IconButton
                        size="small"
                        disabled={isPending}
                        onClick={() =>
                          onToggleFailed(game, objective.objectiveId, objective.failed)
                        }
                        sx={{ color: objective.failed ? 'warning.main' : 'error.main' }}
                      >
                        {objective.failed ? (
                          <ReplayIcon fontSize="small" />
                        ) : (
                          <CancelIcon fontSize="small" />
                        )}
                      </IconButton>
                    </span>
                  </Tooltip>
                )}
                <Typography variant="body2" color="text.secondary">
                  {objective.score} pts
                </Typography>
              </Stack>
            ))}
          </Stack>
        </Fragment>
      ))}
      {pageCount > 1 && (
        <Stack direction="row" sx={{ justifyContent: 'center', mt: 1 }}>
          <Pagination
            count={pageCount}
            page={currentPage}
            onChange={(_e, value) => setPage(value)}
            size="small"
          />
        </Stack>
      )}
    </Box>
  )
}
