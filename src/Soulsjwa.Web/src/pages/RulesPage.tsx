import { RulesPanel } from '../features/events/components/RulesPanel'
import { useFeaturedEvent } from '../features/events/hooks/useFeaturedEvent'
import { useCurrentUser } from '../features/users/hooks/useCurrentUser'
import { canManageEvent } from '../features/events/eventDetail/permissions'
import { EmptyState, ErrorMessage, LoadingState } from '../components/ui'

/**
 * The top nav's "Rules" destination: just the featured event's rules, with
 * none of the event layout's header/tabs/controls around them — a viewer
 * landing here from the nav bar wants the rules, not the rest of the event's
 * management chrome.
 */
export const RulesPage = () => {
  const { data: event, isLoading, isError } = useFeaturedEvent()
  const { data: currentUser } = useCurrentUser()

  if (isLoading) return <LoadingState label="Loading rules…" />
  if (isError) return <ErrorMessage message="Failed to load rules." />

  if (!event) {
    return (
      <EmptyState title="No rules to show" description="There is no featured event right now." />
    )
  }

  return <RulesPanel eventId={event.id} canManage={canManageEvent(event, currentUser)} />
}
