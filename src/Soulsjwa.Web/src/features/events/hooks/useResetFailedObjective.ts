import { eventsApi } from '../api/eventsApi'
import { createEventMutation } from './createEventMutation'

export const useResetFailedObjective = createEventMutation(
  (eventId: string) =>
    (params: { eventGameId: string; objectiveId: string; onBehalfOfUserId?: string }) =>
      eventsApi.resetFailedObjective(
        eventId,
        params.eventGameId,
        params.objectiveId,
        params.onBehalfOfUserId,
      ),
  'progress',
)
