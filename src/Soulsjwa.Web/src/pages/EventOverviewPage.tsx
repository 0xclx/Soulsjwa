import { EventOverviewSection } from '../features/events/components/EventOverviewSection'
import { useEventRoute } from '../features/events/hooks/useEventRoute'

export const EventOverviewPage = () => {
  const { eventId, eventUrlIdentifier, event, currentUser } = useEventRoute()

  return (
    <EventOverviewSection
      eventId={eventId}
      eventUrlIdentifier={eventUrlIdentifier}
      event={event}
      currentUser={currentUser}
    />
  )
}
