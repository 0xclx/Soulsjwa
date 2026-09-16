import { useMemo, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { AuditLogTable, type AuditFiltersState } from '../../audits/components/AuditLogTable'
import { useAdminAudits } from '../hooks/useAdminAudits'
import { eventsApi } from '../../events/api/eventsApi'

const EMPTY_AUDIT_FILTERS: AuditFiltersState = {
  types: [],
  actor: null,
  subject: null,
  from: '',
  to: '',
  event: null,
}

/**
 * System-wide audit log tab: wires the shared `AuditLogTable` to the admin
 * audit query and an event selector populated from recent events.
 */
export const AdminAuditsTab = () => {
  const [filters, setFilters] = useState<AuditFiltersState>(EMPTY_AUDIT_FILTERS)

  const { data: eventsPage } = useQuery({
    queryKey: ['admin', 'events-audit-filter'],
    queryFn: () => eventsApi.list({ page: 1, pageSize: 50, includeArchived: true }),
  })

  const { data, isLoading, isError, hasNextPage, fetchNextPage, isFetchingNextPage } =
    useAdminAudits({
      types: filters.types,
      actorUserId: filters.actor?.userId,
      subjectUserId: filters.subject?.userId,
      eventId: filters.event?.id,
      from: filters.from ? new Date(filters.from).toISOString() : undefined,
      to: filters.to ? new Date(filters.to).toISOString() : undefined,
    })

  const items = useMemo(() => {
    const all = data?.pages.flatMap((page) => page.items)
    if (!all) return all
    return filters.types.length > 1 ? all.filter((row) => filters.types.includes(row.type)) : all
  }, [data, filters.types])

  return (
    <AuditLogTable
      title="Audits"
      items={items}
      isLoading={isLoading}
      isError={isError}
      errorMessage="Failed to load audits."
      filters={filters}
      onFiltersChange={setFilters}
      hasMore={!!hasNextPage}
      onLoadMore={() => void fetchNextPage()}
      isLoadingMore={isFetchingNextPage}
      showEventFilter
      eventOptions={(eventsPage?.items ?? []).map((event) => ({ id: event.id, name: event.name }))}
    />
  )
}
