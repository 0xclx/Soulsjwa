import { eventsApi } from '../api/eventsApi'
import { createEventMutation } from './createEventMutation'

export const useUncompleteObjective = createEventMutation(
  (eventId: string) =>
    (params: { eventGameId: string; objectiveId: string; onBehalfOfUserId?: string }) =>
      eventsApi.uncompleteObjective(
        eventId,
        params.eventGameId,
        params.objectiveId,
        params.onBehalfOfUserId,
      ),
  'progress',
)
