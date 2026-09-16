import { useOutletContext } from 'react-router-dom'
import type { EventResponse, User } from '../../../types'

export interface EventRouteContext {
  eventId: string
  eventUrlIdentifier: string
  event: EventResponse
  currentUser: User | undefined
  isOwner: boolean
  isAdmin: boolean
  canManage: boolean
  canViewActivity: boolean
}

export const useEventRoute = () => useOutletContext<EventRouteContext>()
