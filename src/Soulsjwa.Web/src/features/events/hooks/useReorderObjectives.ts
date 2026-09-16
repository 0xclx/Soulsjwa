import { useMutation, useQueryClient } from '@tanstack/react-query'
import { eventsApi, EVENTS_QUERY_KEYS } from '../api/eventsApi'
import type { EventResponse } from '../../../types'

export const useReorderObjectives = (eventId: string) => {
  const queryClient = useQueryClient()
  const detailKey = EVENTS_QUERY_KEYS.detail(eventId)

  return useMutation({
    mutationFn: (params: { eventGameId: string; objectiveIds: string[] }) =>
      eventsApi.reorderObjectives(eventId, params.eventGameId, params.objectiveIds),
    // Reorder the cached game's objectives immediately so the drop feels
    // instant — waiting for invalidate+refetch to reflect the new order
    // flashes the whole card as it briefly reverts before snapping back.
    onMutate: async ({ eventGameId, objectiveIds }) => {
      await queryClient.cancelQueries({ queryKey: detailKey })
      const previous = queryClient.getQueryData<EventResponse>(detailKey)
      if (previous) {
        queryClient.setQueryData<EventResponse>(detailKey, {
          ...previous,
          games: previous.games.map((game) => {
            if (game.eventGameId !== eventGameId) return game
            const byId = new Map(game.objectives.map((o) => [o.id, o]))
            return {
              ...game,
              objectives: objectiveIds.map((id) => byId.get(id)).filter((o) => o !== undefined),
            }
          }),
        })
      }
      return { previous }
    },
    onError: (_error, _variables, context) => {
      if (context?.previous) queryClient.setQueryData(detailKey, context.previous)
    },
    onSettled: () => queryClient.invalidateQueries({ queryKey: detailKey }),
  })
}
