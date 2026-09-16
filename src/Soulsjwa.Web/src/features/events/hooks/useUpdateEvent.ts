import { eventsApi } from '../api/eventsApi'
import { createEventMutation } from './createEventMutation'
import type { TieBreakMode } from '../../../types'

export const useUpdateEvent = createEventMutation(
  (eventId: string) =>
    (payload: {
      name?: string
      description?: string
      tieBreakMode?: TieBreakMode
      urlAlias?: string
    }) =>
      eventsApi.update(eventId, payload),
  'listing',
)
