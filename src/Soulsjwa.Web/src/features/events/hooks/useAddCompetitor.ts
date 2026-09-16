import { eventsApi } from '../api/eventsApi'
import { createEventMutation } from './createEventMutation'

export const useAddCompetitor = createEventMutation(
  (eventId: string) => (payload: { userId?: string; twitchLogin?: string; isStreamer?: boolean }) =>
    eventsApi.addCompetitor(eventId, payload),
  'structure',
)
