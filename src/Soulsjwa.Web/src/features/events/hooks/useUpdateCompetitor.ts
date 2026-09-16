import { eventsApi } from '../api/eventsApi'
import { createEventMutation } from './createEventMutation'

export const useUpdateCompetitor = createEventMutation(
  (eventId: string) =>
    ({ userId, isStreamer }: { userId: string; isStreamer: boolean }) =>
      eventsApi.updateCompetitor(eventId, userId, { isStreamer }),
  'structure',
)
