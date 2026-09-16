import { useMemo, useState, type FormEvent } from 'react'
import Alert from '@mui/material/Alert'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Dialog from '@mui/material/Dialog'
import DialogActions from '@mui/material/DialogActions'
import DialogContent from '@mui/material/DialogContent'
import DialogTitle from '@mui/material/DialogTitle'
import InputAdornment from '@mui/material/InputAdornment'
import MenuItem from '@mui/material/MenuItem'
import Stack from '@mui/material/Stack'
import Table from '@mui/material/Table'
import TableBody from '@mui/material/TableBody'
import TableCell from '@mui/material/TableCell'
import TableHead from '@mui/material/TableHead'
import TablePagination from '@mui/material/TablePagination'
import TableRow from '@mui/material/TableRow'
import TableSortLabel from '@mui/material/TableSortLabel'
import TextField from '@mui/material/TextField'
import Typography from '@mui/material/Typography'
import AddIcon from '@mui/icons-material/Add'
import EditIcon from '@mui/icons-material/Edit'
import SearchIcon from '@mui/icons-material/Search'
import { ErrorMessage, LoadingState, Surface } from '../../../components/ui'
import { getErrorDetail } from '../../../lib/getErrorDetail'
import { useAdminGames } from '../hooks/useAdminGames'
import { useCreateGame } from '../hooks/useCreateGame'
import { useCreatePredefinedObjective } from '../hooks/useCreatePredefinedObjective'
import { usePredefinedObjectiveCatalog } from '../hooks/usePredefinedObjectiveCatalog'
import { useUpdateGame } from '../hooks/useUpdateGame'
import type { GameResponse } from '../../../types'

type ObjectiveSortField = 'game' | 'name' | 'category' | 'score'
type SortDirection = 'asc' | 'desc'

const ROWS_PER_PAGE_OPTIONS = [10, 25, 50, 100]

export const CatalogTab = () => {
  const games = useAdminGames()
  const objectives = usePredefinedObjectiveCatalog()
  const [gameDialogOpen, setGameDialogOpen] = useState(false)
  const [editingGame, setEditingGame] = useState<GameResponse>()
  const [objectiveDialogOpen, setObjectiveDialogOpen] = useState(false)

  const [objectiveSearch, setObjectiveSearch] = useState('')
  const [objectiveGameFilter, setObjectiveGameFilter] = useState<number | 'all'>('all')
  const [sortField, setSortField] = useState<ObjectiveSortField>('game')
  const [sortDirection, setSortDirection] = useState<SortDirection>('asc')
  const [page, setPage] = useState(0)
  const [rowsPerPage, setRowsPerPage] = useState(25)

  const gameNames = useMemo(
    () => new Map(games.data?.map((game) => [game.id, game.name]) ?? []),
    [games.data],
  )

  const handleSort = (field: ObjectiveSortField) => {
    if (field === sortField) {
      setSortDirection((prev) => (prev === 'asc' ? 'desc' : 'asc'))
    } else {
      setSortField(field)
      setSortDirection('asc')
    }
    setPage(0)
  }

  const filteredSortedObjectives = useMemo(() => {
    const needle = objectiveSearch.trim().toLowerCase()
    const filtered = (objectives.data ?? []).filter((objective) => {
      if (objectiveGameFilter !== 'all' && objective.gameId !== objectiveGameFilter) return false
      if (!needle) return true
      const gameName = gameNames.get(objective.gameId) ?? ''
      return (
        objective.name.toLowerCase().includes(needle) ||
        gameName.toLowerCase().includes(needle) ||
        (objective.category ?? '').toLowerCase().includes(needle)
      )
    })

    const direction = sortDirection === 'asc' ? 1 : -1
    const withGameName = filtered.map((objective) => ({
      objective,
      gameName: gameNames.get(objective.gameId) ?? 'Unknown game',
    }))
    withGameName.sort((a, b) => {
      switch (sortField) {
        case 'score':
          return (a.objective.score - b.objective.score) * direction
        case 'name':
          return a.objective.name.localeCompare(b.objective.name) * direction
        case 'category':
          return (
            ((a.objective.category ?? '').localeCompare(b.objective.category ?? '') ||
              a.objective.name.localeCompare(b.objective.name)) * direction
          )
        case 'game':
        default:
          return (
            (a.gameName.localeCompare(b.gameName) ||
              a.objective.name.localeCompare(b.objective.name)) * direction
          )
      }
    })
    return withGameName.map((entry) => entry.objective)
  }, [objectives.data, objectiveSearch, objectiveGameFilter, gameNames, sortField, sortDirection])

  const pagedObjectives = useMemo(
    () => filteredSortedObjectives.slice(page * rowsPerPage, page * rowsPerPage + rowsPerPage),
    [filteredSortedObjectives, page, rowsPerPage],
  )

  if (games.isLoading || objectives.isLoading) return <LoadingState label="Loading catalog…" />
  if (games.isError || objectives.isError || !games.data || !objectives.data) {
    return <ErrorMessage message="Failed to load the game catalog." />
  }

  return (
    <Stack spacing={3}>
      <Surface>
        <Stack spacing={2}>
          <Stack
            direction={{ xs: 'column', sm: 'row' }}
            spacing={1}
            sx={{ justifyContent: 'space-between', alignItems: { sm: 'center' } }}
          >
            <Box>
              <Typography variant="h6">Games</Typography>
              <Typography color="text.secondary">
                Manage the global game catalog available to event owners.
              </Typography>
            </Box>
            <Button
              variant="contained"
              startIcon={<AddIcon />}
              onClick={() => {
                setEditingGame(undefined)
                setGameDialogOpen(true)
              }}
            >
              Create game
            </Button>
          </Stack>
          <Box sx={{ overflowX: 'auto' }}>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Name</TableCell>
                  <TableCell>Description</TableCell>
                  <TableCell>Connector</TableCell>
                  <TableCell align="right">Actions</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {games.data.map((game) => (
                  <TableRow key={game.id}>
                    <TableCell>{game.name}</TableCell>
                    <TableCell>{game.description || '—'}</TableCell>
                    <TableCell>{game.connectorSupported ? 'Supported' : 'Not supported'}</TableCell>
                    <TableCell align="right">
                      <Button
                        size="small"
                        startIcon={<EditIcon />}
                        onClick={() => {
                          setEditingGame(game)
                          setGameDialogOpen(true)
                        }}
                      >
                        Edit
                      </Button>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </Box>
        </Stack>
      </Surface>

      <Surface>
        <Stack spacing={2}>
          <Stack
            direction={{ xs: 'column', sm: 'row' }}
            spacing={1}
            sx={{ justifyContent: 'space-between', alignItems: { sm: 'center' } }}
          >
            <Box>
              <Typography variant="h6">Predefined objectives</Typography>
              <Typography color="text.secondary">
                Browse reusable objectives that event owners can import into a game.
              </Typography>
            </Box>
            <Button
              variant="contained"
              startIcon={<AddIcon />}
              disabled={games.data.length === 0}
              onClick={() => setObjectiveDialogOpen(true)}
            >
              Create objective
            </Button>
          </Stack>
          {objectives.data.length === 0 ? (
            <Typography color="text.secondary">No predefined objectives yet.</Typography>
          ) : (
            <>
              <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2}>
                <TextField
                  size="small"
                  placeholder="Filter by name or game…"
                  value={objectiveSearch}
                  onChange={(e) => {
                    setObjectiveSearch(e.target.value)
                    setPage(0)
                  }}
                  fullWidth
                  slotProps={{
                    input: {
                      startAdornment: (
                        <InputAdornment position="start">
                          <SearchIcon fontSize="small" />
                        </InputAdornment>
                      ),
                    },
                  }}
                />
                <TextField
                  select
                  size="small"
                  label="Game"
                  value={objectiveGameFilter}
                  onChange={(e) => {
                    setObjectiveGameFilter(
                      e.target.value === 'all' ? 'all' : Number(e.target.value),
                    )
                    setPage(0)
                  }}
                  sx={{ minWidth: { sm: 240 } }}
                >
                  <MenuItem value="all">All games</MenuItem>
                  {games.data.map((game) => (
                    <MenuItem key={game.id} value={game.id}>
                      {game.name}
                    </MenuItem>
                  ))}
                </TextField>
              </Stack>
              {filteredSortedObjectives.length === 0 ? (
                <Typography color="text.secondary">No objectives match this filter.</Typography>
              ) : (
                <Box sx={{ overflowX: 'auto' }}>
                  <Table size="small">
                    <TableHead>
                      <TableRow>
                        <TableCell sortDirection={sortField === 'game' ? sortDirection : false}>
                          <TableSortLabel
                            active={sortField === 'game'}
                            direction={sortField === 'game' ? sortDirection : 'asc'}
                            onClick={() => handleSort('game')}
                          >
                            Game
                          </TableSortLabel>
                        </TableCell>
                        <TableCell sortDirection={sortField === 'name' ? sortDirection : false}>
                          <TableSortLabel
                            active={sortField === 'name'}
                            direction={sortField === 'name' ? sortDirection : 'asc'}
                            onClick={() => handleSort('name')}
                          >
                            Objective
                          </TableSortLabel>
                        </TableCell>
                        <TableCell sortDirection={sortField === 'category' ? sortDirection : false}>
                          <TableSortLabel
                            active={sortField === 'category'}
                            direction={sortField === 'category' ? sortDirection : 'asc'}
                            onClick={() => handleSort('category')}
                          >
                            Category
                          </TableSortLabel>
                        </TableCell>
                        <TableCell
                          align="right"
                          sortDirection={sortField === 'score' ? sortDirection : false}
                        >
                          <TableSortLabel
                            active={sortField === 'score'}
                            direction={sortField === 'score' ? sortDirection : 'asc'}
                            onClick={() => handleSort('score')}
                          >
                            Score
                          </TableSortLabel>
                        </TableCell>
                      </TableRow>
                    </TableHead>
                    <TableBody>
                      {pagedObjectives.map((objective) => (
                        <TableRow key={objective.id}>
                          <TableCell>{gameNames.get(objective.gameId) ?? 'Unknown game'}</TableCell>
                          <TableCell>{objective.name}</TableCell>
                          <TableCell>{objective.category ?? '—'}</TableCell>
                          <TableCell align="right">{objective.score}</TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </Box>
              )}
              <TablePagination
                component="div"
                count={filteredSortedObjectives.length}
                page={page}
                onPageChange={(_, nextPage) => setPage(nextPage)}
                rowsPerPage={rowsPerPage}
                onRowsPerPageChange={(e) => {
                  setRowsPerPage(Number(e.target.value))
                  setPage(0)
                }}
                rowsPerPageOptions={ROWS_PER_PAGE_OPTIONS}
              />
            </>
          )}
        </Stack>
      </Surface>

      {gameDialogOpen && (
        <GameDialog
          game={editingGame}
          onClose={() => {
            setGameDialogOpen(false)
            setEditingGame(undefined)
          }}
        />
      )}
      {objectiveDialogOpen && (
        <PredefinedObjectiveDialog
          games={games.data}
          onClose={() => setObjectiveDialogOpen(false)}
        />
      )}
    </Stack>
  )
}

const GameDialog = ({ game, onClose }: { game?: GameResponse; onClose: () => void }) => {
  const createGame = useCreateGame()
  const updateGame = useUpdateGame()
  const [name, setName] = useState(game?.name ?? '')
  const [description, setDescription] = useState(game?.description ?? '')
  const mutation = game ? updateGame : createGame

  const handleSubmit = (event: FormEvent) => {
    event.preventDefault()
    const payload = { name: name.trim(), description: description.trim() }
    if (game) {
      updateGame.mutate({ gameId: game.id, payload }, { onSuccess: onClose })
    } else {
      createGame.mutate(payload, { onSuccess: onClose })
    }
  }

  return (
    <Dialog open onClose={mutation.isPending ? undefined : onClose} fullWidth maxWidth="sm">
      <Box component="form" onSubmit={handleSubmit}>
        <DialogTitle>{game ? 'Edit game' : 'Create game'}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            <TextField
              label="Name"
              value={name}
              onChange={(event) => setName(event.target.value)}
              slotProps={{ htmlInput: { maxLength: 200 } }}
              required
            />
            <TextField
              label="Description"
              value={description}
              onChange={(event) => setDescription(event.target.value)}
              slotProps={{ htmlInput: { maxLength: 1000 } }}
              multiline
              minRows={3}
            />
            {mutation.isError && (
              <Alert severity="error">
                {getErrorDetail(mutation.error, 'Failed to save game.')}
              </Alert>
            )}
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={onClose} color="inherit" disabled={mutation.isPending}>
            Cancel
          </Button>
          <Button type="submit" variant="contained" disabled={mutation.isPending || !name.trim()}>
            {mutation.isPending ? 'Saving…' : 'Save'}
          </Button>
        </DialogActions>
      </Box>
    </Dialog>
  )
}

const PredefinedObjectiveDialog = ({
  games,
  onClose,
}: {
  games: GameResponse[]
  onClose: () => void
}) => {
  const createObjective = useCreatePredefinedObjective()
  const [gameId, setGameId] = useState(String(games[0]?.id ?? ''))
  const [name, setName] = useState('')
  const [score, setScore] = useState('0')
  const [category, setCategory] = useState('')

  const handleSubmit = (event: FormEvent) => {
    event.preventDefault()
    createObjective.mutate(
      {
        gameId: Number(gameId),
        name: name.trim(),
        score: Number(score),
        category: category.trim() || undefined,
      },
      { onSuccess: onClose },
    )
  }

  return (
    <Dialog open onClose={createObjective.isPending ? undefined : onClose} fullWidth maxWidth="sm">
      <Box component="form" onSubmit={handleSubmit}>
        <DialogTitle>Create predefined objective</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            <TextField
              select
              label="Game"
              value={gameId}
              onChange={(event) => setGameId(event.target.value)}
              required
            >
              {games.map((game) => (
                <MenuItem key={game.id} value={game.id}>
                  {game.name}
                </MenuItem>
              ))}
            </TextField>
            <TextField
              label="Name"
              value={name}
              onChange={(event) => setName(event.target.value)}
              slotProps={{ htmlInput: { maxLength: 200 } }}
              required
            />
            <TextField
              label="Score"
              type="number"
              value={score}
              onChange={(event) => setScore(event.target.value)}
              slotProps={{ htmlInput: { min: 0 } }}
              required
            />
            <TextField
              label="Category"
              value={category}
              onChange={(event) => setCategory(event.target.value)}
              helperText="Optional grouping label (e.g. an in-game area) shown wherever objectives are grouped."
              slotProps={{ htmlInput: { maxLength: 200 } }}
            />
            {createObjective.isError && (
              <Alert severity="error">
                {getErrorDetail(createObjective.error, 'Failed to create objective.')}
              </Alert>
            )}
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={onClose} color="inherit" disabled={createObjective.isPending}>
            Cancel
          </Button>
          <Button
            type="submit"
            variant="contained"
            disabled={
              createObjective.isPending ||
              !gameId ||
              !name.trim() ||
              !Number.isInteger(Number(score)) ||
              Number(score) < 0
            }
          >
            {createObjective.isPending ? 'Creating…' : 'Create'}
          </Button>
        </DialogActions>
      </Box>
    </Dialog>
  )
}
