import { eventsApi } from '../api/eventsApi'
import { createEventMutation } from './createEventMutation'

export const useDeleteObjective = createEventMutation(
  (eventId: string) => (params: { eventGameId: string; objectiveId: string }) =>
    eventsApi.deleteObjective(eventId, params.eventGameId, params.objectiveId),
  'structure',
)
