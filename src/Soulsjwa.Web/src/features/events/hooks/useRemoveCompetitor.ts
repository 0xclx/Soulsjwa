import { eventsApi } from '../api/eventsApi'
import { createEventMutation } from './createEventMutation'

export const useRemoveCompetitor = createEventMutation(
  (eventId: string) => (userId: string) => eventsApi.removeCompetitor(eventId, userId),
  'structure',
)
