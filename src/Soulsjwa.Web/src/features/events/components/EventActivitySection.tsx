import { useMemo, useState } from 'react'
import { ErrorMessage } from '../../../components/ui'
import { AuditLogTable, type AuditFiltersState } from '../../audits/components/AuditLogTable'
import { useEventAudits } from '../hooks/useEventAudits'

const EMPTY_AUDIT_FILTERS: AuditFiltersState = {
  types: [],
  actor: null,
  subject: null,
  from: '',
  to: '',
  event: null,
}

interface EventActivitySectionProps {
  eventId: string
  /** Members-only; a non-member sees an explanatory message instead. */
  canViewActivity: boolean
}

/** Activity tab: the filterable, "load more"-paginated event audit log. */
export const EventActivitySection = ({ eventId, canViewActivity }: EventActivitySectionProps) => {
  const [filters, setFilters] = useState<AuditFiltersState>(EMPTY_AUDIT_FILTERS)

  const { data, isLoading, isError, hasNextPage, fetchNextPage, isFetchingNextPage } =
    useEventAudits(
      eventId,
      {
        types: filters.types,
        actorUserId: filters.actor?.userId,
        subjectUserId: filters.subject?.userId,
        from: filters.from ? new Date(filters.from).toISOString() : undefined,
        to: filters.to ? new Date(filters.to).toISOString() : undefined,
      },
      canViewActivity,
    )

  const items = useMemo(() => {
    const all = data?.pages.flatMap((page) => page.items)
    if (!all) return all
    return filters.types.length > 1 ? all.filter((row) => filters.types.includes(row.type)) : all
  }, [data, filters.types])

  if (!canViewActivity) {
    return <ErrorMessage message="You need to be an event member to view this activity log." />
  }

  return (
    <AuditLogTable
      title="Activity"
      items={items}
      isLoading={isLoading}
      isError={isError}
      errorMessage="Failed to load activity."
      filters={filters}
      onFiltersChange={setFilters}
      hasMore={!!hasNextPage}
      onLoadMore={() => void fetchNextPage()}
      isLoadingMore={isFetchingNextPage}
    />
  )
}
