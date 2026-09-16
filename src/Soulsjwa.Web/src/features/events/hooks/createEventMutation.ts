import { useMutation, useQueryClient } from '@tanstack/react-query'
import { invalidateEventScope, type EventCacheScope } from '../api/eventCache'

/**
 * Factory for the many event-scoped mutation hooks that are just
 * `useMutation` + `invalidateEventScope`, differing only in which API call
 * they make and which cache scope that call affects.
 *
 * `mutationFn` is curried on the hook's own arguments — `eventId` alone, or
 * `eventId` plus a further fixed id such as `competitorId` — so the mutate-time
 * variable is whatever is left. The first hook argument must always be
 * `eventId`, since that's what `invalidateEventScope` invalidates against.
 */
export const createEventMutation =
  <TEventArgs extends readonly [eventId: string, ...rest: unknown[]], TVars, TData>(
    mutationFn: (...eventArgs: TEventArgs) => (vars: TVars) => Promise<TData>,
    scope: EventCacheScope,
  ) =>
  (...eventArgs: TEventArgs) => {
    const queryClient = useQueryClient()
    const [eventId] = eventArgs
    return useMutation({
      mutationFn: mutationFn(...eventArgs),
      onSuccess: () => invalidateEventScope(queryClient, eventId, scope),
    })
  }
