/**
 * Minimal structural shape of a TanStack Query mutation result — just the bits
 * the edit dialogs need. Kept structural so dialogs don't couple to the exact
 * generic parameters of each `useMutation` hook.
 */
export interface SubmitMutation<TVars, TData = void> {
  mutate: (
    vars: TVars,
    options?: { onSuccess?: (data: TData) => void; onError?: (err: unknown) => void },
  ) => void
  isPending: boolean
}
