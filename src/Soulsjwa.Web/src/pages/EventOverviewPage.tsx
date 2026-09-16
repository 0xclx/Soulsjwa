import { EventOverviewSection } from '../features/events/components/EventOverviewSection'
import { useEventRoute } from '../features/events/hooks/useEventRoute'
import { useEventScores } from '../features/events/hooks/useEventScores'

export const EventOverviewPage = () => {
  const { eventId, eventUrlIdentifier, event } = useEventRoute()
  const { data: scores } = useEventScores(eventId, event.isStarted)

  return <EventOverviewSection eventId={eventUrlIdentifier} scores={scores} />
}
