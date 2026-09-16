import { useCallback, useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import Box from '@mui/material/Box'
import Stack from '@mui/material/Stack'
import Button from '@mui/material/Button'
import FormControlLabel from '@mui/material/FormControlLabel'
import Switch from '@mui/material/Switch'
import AddIcon from '@mui/icons-material/Add'
import { useEvents } from '../features/events/hooks/useEvents'
import { useCreateEvent } from '../features/events/hooks/useCreateEvent'
import { useDuplicateEvent } from '../features/events/hooks/useDuplicateEvent'
import { useUnarchiveEvent } from '../features/events/hooks/useUnarchiveEvent'
import { getEventPath } from '../features/events/eventUrl'
import { type EventStatusFilter } from '../features/events/api/eventsApi'
import { EventFilters } from '../features/events/components/EventFilters'
import { EventListCard } from '../features/events/components/EventListCard'
import { CreateEventDialog } from '../features/events/components/CreateEventDialog'
import { useIsAuthenticated } from '../lib/axios'
import { useDebouncedValue } from '../lib/useDebouncedValue'
import { useCurrentUser } from '../features/users/hooks/useCurrentUser'
import {
  CardListSkeleton,
  EmptyState,
  ErrorMessage,
  PageHeader,
  PaginationControls,
} from '../components/ui'

export const EventsPage = () => {
  const [page, setPage] = useState(1)
  const [showArchived, setShowArchived] = useState(false)
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState<EventStatusFilter>('all')
  const isAuthenticated = useIsAuthenticated()
  const { data: me } = useCurrentUser()
  const isAdmin = me?.role === 'Admin'
  const includeArchived = isAdmin && (showArchived || status === 'archived')
  const debouncedSearch = useDebouncedValue(search.trim(), 300)
  const { data, isLoading, isError } = useEvents(page, includeArchived, debouncedSearch, status)
  const createEvent = useCreateEvent()
  const unarchiveEvent = useUnarchiveEvent()
  const duplicateEvent = useDuplicateEvent()
  const navigate = useNavigate()

  const [showCreate, setShowCreate] = useState(false)

  const handleSearchChange = useCallback((value: string) => {
    setSearch(value)
  }, [])

  // Reset to page 1 once the debounced search actually changes the query,
  // not on every keystroke — otherwise paging is reset mid-type. Deferred so
  // we don't synchronously setState from inside an effect (React 19's
  // `react-hooks/set-state-in-effect` rule).
  useEffect(() => {
    const handle = window.setTimeout(() => setPage(1), 0)
    return () => window.clearTimeout(handle)
  }, [debouncedSearch])

  const handleStatusChange = useCallback((value: EventStatusFilter) => {
    setStatus(value)
    setPage(1)
  }, [])

  const handleUnarchive = useCallback((id: string) => unarchiveEvent.mutate(id), [unarchiveEvent])

  const handleDuplicate = useCallback(
    (id: string) =>
      duplicateEvent.mutate(id, {
        onSuccess: (copy) => navigate(getEventPath(copy.id, copy.urlAlias)),
      }),
    [duplicateEvent, navigate],
  )

  const handlePrev = useCallback(() => setPage((p) => Math.max(1, p - 1)), [])
  const handleNext = useCallback(() => setPage((p) => p + 1), [])

  if (isError) {
    return <ErrorMessage message="Failed to load events." />
  }

  const totalPages = data ? Math.max(1, Math.ceil((data.totalCount ?? 0) / data.pageSize)) : 1

  return (
    <Stack spacing={3}>
      <PageHeader
        eyebrow="Events"
        title="Events"
        description="Search events by name and status."
        actions={
          <>
            {isAdmin && (
              <FormControlLabel
                control={
                  <Switch
                    size="small"
                    checked={showArchived}
                    onChange={(e) => {
                      setShowArchived(e.target.checked)
                      setPage(1)
                    }}
                  />
                }
                label="Show archived"
              />
            )}
            {isAuthenticated && isAdmin && (
              <Button
                startIcon={<AddIcon />}
                variant="contained"
                onClick={() => setShowCreate(true)}
              >
                Create event
              </Button>
            )}
          </>
        }
      />

      <EventFilters
        search={search}
        status={status}
        isAdmin={isAdmin}
        onSearchChange={handleSearchChange}
        onStatusChange={handleStatusChange}
      />

      {isLoading ? (
        <CardListSkeleton count={4} columns={2} label="Loading events…" />
      ) : data?.items.length === 0 ? (
        <EmptyState
          title="No events found"
          description="Adjust your filters or create the first event when you have admin access."
        />
      ) : (
        <Box
          sx={{
            display: 'grid',
            gridTemplateColumns: { xs: '1fr', lg: 'repeat(2, minmax(0, 1fr))' },
            gap: 2,
          }}
        >
          {data?.items.map((event) => (
            <EventListCard
              key={event.id}
              event={event}
              isAdmin={isAdmin}
              onUnarchive={handleUnarchive}
              unarchivePending={unarchiveEvent.isPending}
              onDuplicate={handleDuplicate}
              duplicatePending={duplicateEvent.isPending}
            />
          ))}
        </Box>
      )}

      {data && (data.hasNextPage || data.hasPreviousPage) && (
        <PaginationControls
          page={data.page}
          totalPages={totalPages}
          hasPreviousPage={data.hasPreviousPage}
          hasNextPage={data.hasNextPage}
          onPrevious={handlePrev}
          onNext={handleNext}
          label="events"
        />
      )}

      <CreateEventDialog
        open={showCreate}
        mutation={createEvent}
        onClose={() => setShowCreate(false)}
      />
    </Stack>
  )
}
