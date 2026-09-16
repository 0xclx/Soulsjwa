import { useMutation, useQueryClient } from '@tanstack/react-query'
import { eventsApi, EVENTS_QUERY_KEYS } from '../api/eventsApi'
import type { EventResponse } from '../../../types'

export const useReorderEventGames = (eventId: string) => {
  const queryClient = useQueryClient()
  const detailKey = EVENTS_QUERY_KEYS.detail(eventId)

  return useMutation({
    mutationFn: (eventGameIds: string[]) => eventsApi.reorderEventGames(eventId, eventGameIds),
    // Reorder the cached event immediately so the drop feels instant — waiting
    // for invalidate+refetch to reflect the new order flashes the whole
    // Games list as it briefly reverts to the old order before snapping back.
    onMutate: async (eventGameIds) => {
      await queryClient.cancelQueries({ queryKey: detailKey })
      const previous = queryClient.getQueryData<EventResponse>(detailKey)
      if (previous) {
        const byId = new Map(previous.games.map((g) => [g.eventGameId, g]))
        queryClient.setQueryData<EventResponse>(detailKey, {
          ...previous,
          games: eventGameIds.map((id) => byId.get(id)).filter((g) => g !== undefined),
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
