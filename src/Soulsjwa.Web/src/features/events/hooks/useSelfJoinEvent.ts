import { eventsApi } from '../api/eventsApi'
import { createEventMutation } from './createEventMutation'

export const useSelfJoinEvent = createEventMutation(
  (eventId: string) => () => eventsApi.selfJoin(eventId),
  'structure',
)
