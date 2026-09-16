import { eventsApi } from '../api/eventsApi'
import { createEventMutation } from './createEventMutation'

/** Fails every still-pending objective of a game for self or `onBehalfOfUserId`. */
export const useFailRemainingObjectives = createEventMutation(
  (eventId: string) => (params: { eventGameId: string; onBehalfOfUserId?: string }) =>
    eventsApi.failRemainingObjectives(eventId, params.eventGameId, params.onBehalfOfUserId),
  'progress',
)
