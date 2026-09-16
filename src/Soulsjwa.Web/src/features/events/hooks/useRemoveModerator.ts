import { eventsApi } from '../api/eventsApi'
import { createEventMutation } from './createEventMutation'

export const useRemoveModerator = createEventMutation(
  (eventId: string, competitorId: string) => (userId: string) =>
    eventsApi.removeModerator(eventId, competitorId, userId),
  'structure',
)
