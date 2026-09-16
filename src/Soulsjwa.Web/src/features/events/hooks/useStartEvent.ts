import { eventsApi } from '../api/eventsApi'
import { createEventMutation } from './createEventMutation'

export const useStartEvent = createEventMutation(
  (eventId: string) => () => eventsApi.startEvent(eventId),
  'listing',
)
