import { LoadingState } from '../components/ui'
import { EventActivitySection } from '../features/events/components/EventActivitySection'
import { useEventRoute } from '../features/events/hooks/useEventRoute'
import { useCurrentUser } from '../features/users/hooks/useCurrentUser'

export const EventActivityPage = () => {
  const { isLoading: isLoadingUser } = useCurrentUser()
  const { eventId, canViewActivity } = useEventRoute()

  if (isLoadingUser) {
    return <LoadingState label="Loading user…" />
  }

  return <EventActivitySection eventId={eventId} canViewActivity={canViewActivity} />
}
