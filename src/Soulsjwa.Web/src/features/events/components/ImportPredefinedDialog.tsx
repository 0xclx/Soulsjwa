import { useMemo, useState } from 'react'
import Dialog from '@mui/material/Dialog'
import DialogActions from '@mui/material/DialogActions'
import DialogContent from '@mui/material/DialogContent'
import DialogTitle from '@mui/material/DialogTitle'
import Button from '@mui/material/Button'
import Checkbox from '@mui/material/Checkbox'
import FormControlLabel from '@mui/material/FormControlLabel'
import FormGroup from '@mui/material/FormGroup'
import MenuItem from '@mui/material/MenuItem'
import TextField from '@mui/material/TextField'
import Typography from '@mui/material/Typography'
import Stack from '@mui/material/Stack'
import Box from '@mui/material/Box'
import LinearProgress from '@mui/material/LinearProgress'
import { usePredefinedObjectives } from '../hooks/usePredefinedObjectives'
import { useImportPredefinedObjectives } from '../hooks/useImportPredefinedObjectives'

/** Sentinel filter value meaning "don't filter on this dimension". */
const ALL_LOCATIONS = ''
const ALL_TYPES = ''

/**
 * Predefined objective names follow `"<location> - <type>"` (e.g.
 * "Abyssal Woods - Grace Discovered", "Margit, the Fell Omen - Slain"). The
 * objective's `category` field is the location/area grouping (used as its
 * own filter below); the *kind* of objective (grace, boss, bonfire, ...)
 * isn't a stored field at all, so it's derived from this name suffix.
 */
const objectiveType = (name: string): string => {
  const idx = name.lastIndexOf(' - ')
  return idx === -1 ? name : name.slice(idx + 3)
}

interface ImportPredefinedDialogProps {
  open: boolean
  eventId: string
  eventGameId: string
  knownGameId: number
  gameName: string
  onClose: () => void
}

/**
 * Lets an event owner pick **all** or a subset of the game's predefined
 * objectives to import. Selection persists across filter-text changes; the
 * import endpoint is idempotent so re-importing the same names is safe.
 */
export const ImportPredefinedDialog = ({
  open,
  eventId,
  eventGameId,
  knownGameId,
  gameName,
  onClose,
}: ImportPredefinedDialogProps) => {
  const { data: objectives, isLoading } = usePredefinedObjectives(knownGameId, open)
  const importMutation = useImportPredefinedObjectives()
  const [selected, setSelected] = useState<Set<string>>(new Set())
  const [filter, setFilter] = useState('')
  const [locationFilter, setLocationFilter] = useState(ALL_LOCATIONS)
  const [typeFilter, setTypeFilter] = useState(ALL_TYPES)

  const locations = useMemo(() => {
    if (!objectives) return []
    const set = new Set<string>()
    for (const o of objectives) {
      if (o.category) set.add(o.category)
    }
    return Array.from(set).sort((a, b) => a.localeCompare(b))
  }, [objectives])

  const types = useMemo(() => {
    if (!objectives) return []
    const set = new Set<string>()
    for (const o of objectives) set.add(objectiveType(o.name))
    return Array.from(set).sort((a, b) => a.localeCompare(b))
  }, [objectives])

  const filtered = useMemo(() => {
    if (!objectives) return []
    const needle = filter.trim().toLowerCase()
    return objectives.filter((o) => {
      if (needle && !o.name.toLowerCase().includes(needle)) return false
      if (locationFilter !== ALL_LOCATIONS && o.category !== locationFilter) return false
      if (typeFilter !== ALL_TYPES && objectiveType(o.name) !== typeFilter) return false
      return true
    })
  }, [objectives, filter, locationFilter, typeFilter])

  const toggle = (id: string) => {
    setSelected((prev) => {
      const next = new Set(prev)
      if (next.has(id)) next.delete(id)
      else next.add(id)
      return next
    })
  }
  const selectAllVisible = () =>
    setSelected((prev) => {
      const next = new Set(prev)
      for (const o of filtered) next.add(o.id)
      return next
    })
  const clearSelection = () => setSelected(new Set())

  const close = () => {
    setSelected(new Set())
    setFilter('')
    setLocationFilter(ALL_LOCATIONS)
    setTypeFilter(ALL_TYPES)
    onClose()
  }

  const importSelected = async () => {
    if (selected.size === 0) return
    await importMutation.mutateAsync({
      eventId,
      eventGameId,
      objectiveIds: Array.from(selected),
    })
    close()
  }
  const importAll = async () => {
    await importMutation.mutateAsync({ eventId, eventGameId })
    close()
  }

  return (
    <Dialog open={open} onClose={close} fullWidth maxWidth="sm">
      <DialogTitle>Import predefined objectives — {gameName}</DialogTitle>
      <DialogContent dividers>
        {isLoading && <LinearProgress sx={{ mb: 2 }} />}
        {!isLoading && (objectives?.length ?? 0) === 0 && (
          <Typography color="text.secondary">
            No predefined objectives are available for this game.
          </Typography>
        )}
        {!isLoading && (objectives?.length ?? 0) > 0 && (
          <Stack spacing={1.5}>
            <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1.5}>
              <TextField
                size="small"
                label="Filter by name"
                value={filter}
                onChange={(e) => setFilter(e.target.value)}
                fullWidth
              />
              {locations.length > 0 && (
                <TextField
                  select
                  size="small"
                  label="Location"
                  value={locationFilter}
                  onChange={(e) => setLocationFilter(e.target.value)}
                  sx={{ minWidth: 160 }}
                >
                  <MenuItem value={ALL_LOCATIONS}>All locations</MenuItem>
                  {locations.map((location) => (
                    <MenuItem key={location} value={location}>
                      {location}
                    </MenuItem>
                  ))}
                </TextField>
              )}
              {types.length > 0 && (
                <TextField
                  select
                  size="small"
                  label="Type"
                  value={typeFilter}
                  onChange={(e) => setTypeFilter(e.target.value)}
                  sx={{ minWidth: 160 }}
                >
                  <MenuItem value={ALL_TYPES}>All types</MenuItem>
                  {types.map((type) => (
                    <MenuItem key={type} value={type}>
                      {type}
                    </MenuItem>
                  ))}
                </TextField>
              )}
            </Stack>
            <Stack direction="row" spacing={1} sx={{ alignItems: 'center', flexWrap: 'wrap' }}>
              <Button size="small" onClick={selectAllVisible}>
                Select all
                {filter || locationFilter !== ALL_LOCATIONS || typeFilter !== ALL_TYPES
                  ? ' filtered'
                  : ''}
              </Button>
              <Button size="small" onClick={clearSelection} disabled={selected.size === 0}>
                Clear
              </Button>
              <Typography variant="caption" color="text.secondary">
                {selected.size} selected
              </Typography>
            </Stack>
            <Box sx={{ maxHeight: 320, overflowY: 'auto', pr: 1 }}>
              <FormGroup>
                {filtered.map((o) => (
                  <FormControlLabel
                    key={o.id}
                    control={
                      <Checkbox
                        size="small"
                        checked={selected.has(o.id)}
                        onChange={() => toggle(o.id)}
                      />
                    }
                    label={`${o.name} (${o.score} pts)`}
                  />
                ))}
              </FormGroup>
            </Box>
          </Stack>
        )}
      </DialogContent>
      <DialogActions>
        <Button onClick={close} color="inherit" disabled={importMutation.isPending}>
          Cancel
        </Button>
        <Button
          onClick={importAll}
          disabled={importMutation.isPending || isLoading || (objectives?.length ?? 0) === 0}
        >
          Import all
        </Button>
        <Button
          onClick={importSelected}
          variant="contained"
          disabled={importMutation.isPending || selected.size === 0}
        >
          {importMutation.isPending ? 'Importing…' : `Import ${selected.size || ''}`}
        </Button>
      </DialogActions>
    </Dialog>
  )
}
