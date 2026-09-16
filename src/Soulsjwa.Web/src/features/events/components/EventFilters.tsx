import Stack from '@mui/material/Stack'
import TextField from '@mui/material/TextField'
import { Surface } from '../../../components/ui'
import {
  EVENT_STATUS_FILTER_LABELS,
  EVENT_STATUS_FILTERS,
  type EventStatusFilter,
} from '../api/eventsApi'

interface EventFiltersProps {
  search: string
  status: EventStatusFilter
  /** Admins may filter to archived events; the option is hidden otherwise. */
  isAdmin: boolean
  onSearchChange: (value: string) => void
  onStatusChange: (value: EventStatusFilter) => void
}

/** Search + status filter bar for the Events list, stacked on mobile. */
export const EventFilters = ({
  search,
  status,
  isAdmin,
  onSearchChange,
  onStatusChange,
}: EventFiltersProps) => (
  <Surface>
    <Stack direction={{ xs: 'column', md: 'row' }} spacing={2}>
      <TextField
        label="Search events"
        value={search}
        onChange={(e) => onSearchChange(e.target.value)}
        placeholder="Name or description"
        fullWidth
        size="small"
      />
      <TextField
        select
        label="Status"
        value={status}
        onChange={(e) => onStatusChange(e.target.value as EventStatusFilter)}
        size="small"
        sx={{ minWidth: { md: 180 } }}
        slotProps={{ select: { native: true } }}
      >
        {EVENT_STATUS_FILTERS.filter((option) => option !== 'archived' || isAdmin).map((option) => (
          <option key={option} value={option}>
            {EVENT_STATUS_FILTER_LABELS[option]}
          </option>
        ))}
      </TextField>
    </Stack>
  </Surface>
)
