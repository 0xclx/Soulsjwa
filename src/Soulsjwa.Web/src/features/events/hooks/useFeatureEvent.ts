import { eventsApi } from '../api/eventsApi'
import { createEventMutation } from './createEventMutation'

export const useFeatureEvent = createEventMutation(
  (eventId: string) => () => eventsApi.featureEvent(eventId),
  'listing',
)
