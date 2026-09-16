import { eventsApi } from '../api/eventsApi'
import { createEventMutation } from './createEventMutation'

export const useRemoveEventGame = createEventMutation(
  (eventId: string) => (eventGameId: string) => eventsApi.removeEventGame(eventId, eventGameId),
  'structure',
)
