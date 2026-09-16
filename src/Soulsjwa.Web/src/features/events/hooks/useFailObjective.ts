import { eventsApi } from '../api/eventsApi'
import { createEventMutation } from './createEventMutation'

export const useFailObjective = createEventMutation(
  (eventId: string) =>
    (params: { eventGameId: string; objectiveId: string; onBehalfOfUserId?: string }) =>
      eventsApi.failObjective(
        eventId,
        params.eventGameId,
        params.objectiveId,
        params.onBehalfOfUserId,
      ),
  'progress',
)
