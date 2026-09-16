import { eventsApi } from '../api/eventsApi'
import { createEventMutation } from './createEventMutation'

export const useCompleteObjective = createEventMutation(
  (eventId: string) =>
    (params: { eventGameId: string; objectiveId: string; onBehalfOfUserId?: string }) =>
      eventsApi.completeObjective(
        eventId,
        params.eventGameId,
        params.objectiveId,
        params.onBehalfOfUserId,
      ),
  'progress',
)
