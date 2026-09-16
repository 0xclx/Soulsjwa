import { useEffect, useRef, useState } from 'react'

/** A real change: the timestamp advanced to an actual value. */
const advanced = (previous: string | null, next: string | null): boolean =>
  next !== null && previous !== next

/**
 * Tracks which items have a "fresh" timestamp since the last render and
 * reports their keys for a configurable duration. Pure JS so it works
 * whether or not CSS animations are enabled. `shownOf` returns every
 * timestamp an item is currently displaying; the item is fresh when any one
 * of them advanced, and the first render only establishes the baseline.
 *
 * Expiry is tracked as an absolute deadline per key rather than a timer
 * scoped to a single effect run: a per-effect timer gets cancelled by the
 * next effect's cleanup, so any item flashed while an earlier one is still
 * pending would have its expiry silently cancelled and never rescheduled.
 * Deadlines survive across effect runs; a single timer is always (re)armed
 * for the earliest one still pending.
 */
export function useFreshChanges<T>(
  items: readonly T[],
  keyOf: (item: T) => string,
  shownOf: (item: T) => readonly (string | null)[],
  enabled: boolean,
  durationSeconds: number,
): Set<string> {
  const previous = useRef<Map<string, readonly (string | null)[]>>(new Map())
  const deadlines = useRef<Map<string, number>>(new Map())
  const timerHandle = useRef<ReturnType<typeof window.setTimeout> | null>(null)
  const [active, setActive] = useState<Set<string>>(() => new Set())

  useEffect(() => {
    if (!enabled) return

    const fresh: string[] = []
    for (const item of items) {
      const key = keyOf(item)
      const shown = shownOf(item)
      const prev = previous.current.get(key)
      if (prev !== undefined && shown.some((next, i) => advanced(prev[i] ?? null, next))) {
        fresh.push(key)
      }
      previous.current.set(key, shown)
    }

    if (fresh.length > 0) {
      const expiresAt = Date.now() + durationSeconds * 1000
      for (const key of fresh) deadlines.current.set(key, expiresAt)
    }

    const scheduleNextExpiry = (): void => {
      if (timerHandle.current !== null) {
        window.clearTimeout(timerHandle.current)
        timerHandle.current = null
      }
      if (deadlines.current.size === 0) return
      const nextDeadline = Math.min(...deadlines.current.values())
      const delay = Math.max(0, nextDeadline - Date.now())
      timerHandle.current = window.setTimeout(() => {
        const now = Date.now()
        for (const [key, deadline] of deadlines.current) {
          if (deadline <= now) deadlines.current.delete(key)
        }
        setActive(new Set(deadlines.current.keys()))
        scheduleNextExpiry()
      }, delay)
    }

    // Defer the state update so we don't synchronously re-render from inside
    // an effect (React 19's `react-hooks/set-state-in-effect` rule). Always
    // (re)schedule the earliest pending expiry — even when nothing new
    // flashed this run — so a highlight from an earlier run is never left
    // without a timer to eventually clear it.
    const applyHandle = window.setTimeout(() => {
      if (fresh.length > 0) {
        setActive(new Set(deadlines.current.keys()))
      }
      scheduleNextExpiry()
    }, 0)

    return () => window.clearTimeout(applyHandle)
  }, [items, keyOf, shownOf, enabled, durationSeconds])

  // Clear the expiry timer on unmount — it outlives any single effect run.
  useEffect(() => {
    return () => {
      if (timerHandle.current !== null) window.clearTimeout(timerHandle.current)
    }
  }, [])

  return active
}
