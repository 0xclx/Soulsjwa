import { eventsApi } from '../api/eventsApi'
import { createEventMutation } from './createEventMutation'

export const useAddCustomGame = createEventMutation(
  (eventId: string) => (payload: { name: string; description?: string }) =>
    eventsApi.addCustomGame(eventId, payload),
  'structure',
)
