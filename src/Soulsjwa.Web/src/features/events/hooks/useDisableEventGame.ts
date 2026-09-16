import { eventsApi } from '../api/eventsApi'
import { createEventMutation } from './createEventMutation'

export const useDisableEventGame = createEventMutation(
  (eventId: string) => (eventGameId: string) => eventsApi.disableEventGame(eventId, eventGameId),
  'structure',
)
