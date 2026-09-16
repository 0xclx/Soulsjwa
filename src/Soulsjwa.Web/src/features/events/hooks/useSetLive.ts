import { eventsApi } from '../api/eventsApi'
import { createEventMutation } from './createEventMutation'

export const useSetLive = createEventMutation(
  (eventId: string) => (params: { isLive: boolean; onBehalfOfUserId?: string }) =>
    eventsApi.setLive(eventId, params.isLive, params.onBehalfOfUserId),
  'structure',
)
