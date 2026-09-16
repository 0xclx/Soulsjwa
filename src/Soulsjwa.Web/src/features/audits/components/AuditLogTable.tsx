import { Fragment, useMemo, useState } from 'react'
import Autocomplete from '@mui/material/Autocomplete'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Chip from '@mui/material/Chip'
import Collapse from '@mui/material/Collapse'
import IconButton from '@mui/material/IconButton'
import Paper from '@mui/material/Paper'
import Stack from '@mui/material/Stack'
import Table from '@mui/material/Table'
import TableBody from '@mui/material/TableBody'
import TableCell from '@mui/material/TableCell'
import TableContainer from '@mui/material/TableContainer'
import TableHead from '@mui/material/TableHead'
import TableRow from '@mui/material/TableRow'
import TextField from '@mui/material/TextField'
import Typography from '@mui/material/Typography'
import KeyboardArrowDownIcon from '@mui/icons-material/KeyboardArrowDown'
import KeyboardArrowUpIcon from '@mui/icons-material/KeyboardArrowUp'
import type { AuditLog } from '../../../types'
import { getAuditTypeLabel, AUDIT_EVENT_TYPES } from '../auditEventTypes'
import { Spinner, ErrorMessage } from '../../../components/ui'
import { UserPicker, type UserPickerOption } from '../../users/components/UserPicker'

export interface AuditFiltersState {
  types: string[]
  actor: UserPickerOption | null
  subject: UserPickerOption | null
  from: string
  to: string
  event: { id: string; name: string } | null
}

interface Props {
  title?: string
  items?: AuditLog[]
  isLoading: boolean
  isError: boolean
  errorMessage: string
  filters: AuditFiltersState
  onFiltersChange: (next: AuditFiltersState) => void
  /** Cursor pagination: true while there's another page to fetch. */
  hasMore: boolean
  onLoadMore: () => void
  isLoadingMore: boolean
  showEventFilter?: boolean
  eventOptions?: { id: string; name: string }[]
}

const prettyJson = (value: string | null) => {
  if (!value) return '—'

  try {
    return JSON.stringify(JSON.parse(value), null, 2)
  } catch {
    return value
  }
}

const AuditLogDetails = ({ row }: { row: AuditLog }) => (
  <Box sx={{ p: 2, bgcolor: 'action.hover' }}>
    <Stack direction={{ xs: 'column', md: 'row' }} spacing={2}>
      <Paper variant="outlined" sx={{ flex: 1, borderColor: 'error.main', bgcolor: 'error.light' }}>
        <Box sx={{ px: 1.5, py: 1, borderBottom: '1px solid', borderColor: 'divider' }}>
          <Typography variant="subtitle2" sx={{ color: 'error.dark' }}>
            Before
          </Typography>
        </Box>
        <Box component="pre" sx={{ m: 0, p: 1.5, overflowX: 'auto', fontSize: '0.75rem' }}>
          {prettyJson(row.beforeJson)}
        </Box>
      </Paper>
      <Paper
        variant="outlined"
        sx={{ flex: 1, borderColor: 'success.main', bgcolor: 'success.light' }}
      >
        <Box sx={{ px: 1.5, py: 1, borderBottom: '1px solid', borderColor: 'divider' }}>
          <Typography variant="subtitle2" sx={{ color: 'success.dark' }}>
            After
          </Typography>
        </Box>
        <Box component="pre" sx={{ m: 0, p: 1.5, overflowX: 'auto', fontSize: '0.75rem' }}>
          {prettyJson(row.afterJson)}
        </Box>
      </Paper>
    </Stack>
  </Box>
)

const AuditLogRow = ({ row }: { row: AuditLog }) => {
  const [expanded, setExpanded] = useState(false)

  return (
    <Fragment>
      <TableRow hover>
        <TableCell sx={{ whiteSpace: 'nowrap' }}>
          {new Date(row.createdAt).toLocaleString()}
        </TableCell>
        <TableCell>
          <Chip size="small" color="primary" label={getAuditTypeLabel(row.type)} />
        </TableCell>
        <TableCell>{row.actor.displayName}</TableCell>
        <TableCell>{row.subject?.displayName ?? '—'}</TableCell>
        <TableCell>{row.reason ?? '—'}</TableCell>
        <TableCell align="right">
          <IconButton size="small" onClick={() => setExpanded((v) => !v)}>
            {expanded ? <KeyboardArrowUpIcon /> : <KeyboardArrowDownIcon />}
          </IconButton>
        </TableCell>
      </TableRow>
      <TableRow>
        <TableCell sx={{ p: 0 }} colSpan={6}>
          <Collapse in={expanded} timeout="auto" unmountOnExit>
            <AuditLogDetails row={row} />
          </Collapse>
        </TableCell>
      </TableRow>
    </Fragment>
  )
}

export const AuditLogTable = ({
  title,
  items,
  isLoading,
  isError,
  errorMessage,
  filters,
  onFiltersChange,
  hasMore,
  onLoadMore,
  isLoadingMore,
  showEventFilter,
  eventOptions,
}: Props) => {
  const eventValue = useMemo(() => {
    if (!filters.event || !eventOptions) return filters.event
    return eventOptions.find((e) => e.id === filters.event?.id) ?? filters.event
  }, [eventOptions, filters.event])

  return (
    <Stack spacing={2}>
      {title && (
        <Typography variant="h5" component="h2">
          {title}
        </Typography>
      )}

      <Paper variant="outlined" sx={{ p: 2 }}>
        <Stack spacing={2}>
          <Autocomplete<string, true, false, true>
            multiple
            freeSolo
            options={Array.from(AUDIT_EVENT_TYPES)}
            value={filters.types}
            onChange={(_, value) => {
              const cleaned = value
                .map((v) => v.trim())
                .filter((v) => v.length > 0)
                .filter((v, idx, arr) => arr.indexOf(v) === idx)
              onFiltersChange({ ...filters, types: cleaned })
            }}
            renderInput={(params) => <TextField {...params} label="Type" size="small" />}
          />
          <Stack direction={{ xs: 'column', md: 'row' }} spacing={2}>
            <UserPicker
              value={filters.actor}
              onChange={(actor) => onFiltersChange({ ...filters, actor })}
              label="Actor user"
              fullWidth
            />
            <UserPicker
              value={filters.subject}
              onChange={(subject) => onFiltersChange({ ...filters, subject })}
              label="Subject user"
              fullWidth
            />
          </Stack>
          {showEventFilter && (
            <Autocomplete<{ id: string; name: string }, false, false, false>
              options={eventOptions ?? []}
              value={eventValue}
              onChange={(_, event) => onFiltersChange({ ...filters, event })}
              getOptionLabel={(option) => option.name}
              isOptionEqualToValue={(option, value) => option.id === value.id}
              renderInput={(params) => <TextField {...params} label="Event" size="small" />}
            />
          )}
          <Stack direction={{ xs: 'column', md: 'row' }} spacing={2}>
            <TextField
              label="From"
              type="datetime-local"
              size="small"
              value={filters.from}
              onChange={(e) => onFiltersChange({ ...filters, from: e.target.value })}
              fullWidth
            />
            <TextField
              label="To"
              type="datetime-local"
              size="small"
              value={filters.to}
              onChange={(e) => onFiltersChange({ ...filters, to: e.target.value })}
              fullWidth
            />
          </Stack>
          <Box>
            <Button
              variant="outlined"
              onClick={() =>
                onFiltersChange({
                  types: [],
                  actor: null,
                  subject: null,
                  from: '',
                  to: '',
                  event: null,
                })
              }
            >
              Reset
            </Button>
          </Box>
        </Stack>
      </Paper>

      {isLoading && <Spinner />}
      {isError && <ErrorMessage message={errorMessage} />}
      {items && (
        <>
          <TableContainer component={Paper} variant="outlined">
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Time</TableCell>
                  <TableCell>Type</TableCell>
                  <TableCell>Actor</TableCell>
                  <TableCell>Subject</TableCell>
                  <TableCell>Reason</TableCell>
                  <TableCell align="right">Details</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {items.length === 0 && (
                  <TableRow>
                    <TableCell colSpan={6} align="center">
                      <Typography color="text.secondary">No activity yet.</Typography>
                    </TableCell>
                  </TableRow>
                )}
                {items.map((row) => (
                  <AuditLogRow key={row.id} row={row} />
                ))}
              </TableBody>
            </Table>
          </TableContainer>
          <Stack direction="row" sx={{ justifyContent: 'center' }}>
            {hasMore ? (
              <Button variant="outlined" onClick={onLoadMore} disabled={isLoadingMore}>
                {isLoadingMore ? 'Loading…' : 'Load more'}
              </Button>
            ) : (
              items.length > 0 && (
                <Typography variant="body2" color="text.secondary">
                  End of activity.
                </Typography>
              )
            )}
          </Stack>
        </>
      )}
    </Stack>
  )
}
