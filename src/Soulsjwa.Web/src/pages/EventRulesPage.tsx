import { RulesPanel } from '../features/events/components/RulesPanel'
import { useEventRoute } from '../features/events/hooks/useEventRoute'

/** The Rules tab within an event's own pages — see `RulesPanel` for the content. */
export const EventRulesPage = () => {
  const { eventId, canManage } = useEventRoute()
  return <RulesPanel eventId={eventId} canManage={canManage} />
}
