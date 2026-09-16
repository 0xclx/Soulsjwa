import { eventsApi } from '../api/eventsApi'
import { createEventMutation } from './createEventMutation'

export const useEnableEventGame = createEventMutation(
  (eventId: string) => (eventGameId: string) => eventsApi.enableEventGame(eventId, eventGameId),
  'structure',
)
