import { eventsApi } from '../api/eventsApi'
import { createEventMutation } from './createEventMutation'

export const useEditObjective = createEventMutation(
  (eventId: string) =>
    (params: {
      eventGameId: string
      objectiveId: string
      payload: {
        name?: string
        score?: number
        category?: string | null
        metadata?: string | null
        rule?: string | null
        failRule?: string | null
      }
    }) =>
      eventsApi.editObjective(eventId, params.eventGameId, params.objectiveId, params.payload),
  'structure',
)
