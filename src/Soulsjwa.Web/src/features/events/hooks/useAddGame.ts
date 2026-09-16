import { eventsApi } from '../api/eventsApi'
import { createEventMutation } from './createEventMutation'

export const useAddGame = createEventMutation(
  (eventId: string) => (payload: { gameId: number; name?: string; description?: string }) =>
    eventsApi.addGame(eventId, payload),
  'structure',
)
