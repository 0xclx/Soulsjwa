import { eventsApi } from '../api/eventsApi'
import { createEventMutation } from './createEventMutation'

export const useUnfeatureEvent = createEventMutation(
  (eventId: string) => () => eventsApi.unfeatureEvent(eventId),
  'listing',
)
