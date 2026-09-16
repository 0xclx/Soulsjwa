import { eventsApi } from '../api/eventsApi'
import { createEventMutation } from './createEventMutation'

export const useStopEvent = createEventMutation(
  (eventId: string) => () => eventsApi.stopEvent(eventId),
  'listing',
)
