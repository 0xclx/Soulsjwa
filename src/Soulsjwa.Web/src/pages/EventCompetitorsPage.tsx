import { CompetitorsSection } from '../features/events/components/CompetitorsSection'
import { useEventRoute } from '../features/events/hooks/useEventRoute'

export const EventCompetitorsPage = () => {
  const { event, currentUser } = useEventRoute()
  return <CompetitorsSection event={event} currentUser={currentUser} />
}
