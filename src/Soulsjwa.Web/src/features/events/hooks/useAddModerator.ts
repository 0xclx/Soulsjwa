import { eventsApi } from '../api/eventsApi'
import { createEventMutation } from './createEventMutation'

export const useAddModerator = createEventMutation(
  (eventId: string, competitorId: string) => (userId: string) =>
    eventsApi.addModerator(eventId, competitorId, userId),
  'structure',
)
