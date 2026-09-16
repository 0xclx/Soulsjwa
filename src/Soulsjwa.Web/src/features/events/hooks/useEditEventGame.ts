import { eventsApi } from '../api/eventsApi'
import { createEventMutation } from './createEventMutation'

export const useEditEventGame = createEventMutation(
  (eventId: string) => (params: { eventGameId: string; name: string; description: string }) =>
    eventsApi.patchEventGame(eventId, params.eventGameId, {
      name: params.name,
      description: params.description,
    }),
  'structure',
)
