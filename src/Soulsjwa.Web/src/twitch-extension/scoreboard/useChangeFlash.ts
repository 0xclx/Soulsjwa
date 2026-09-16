import { useEffect, useRef, useState } from 'react'
import type { BoardRow } from './scope'

/** Long enough to catch the eye between two polls, short enough not to nag. */
export const FLASH_MS = 5_000

interface Shown {
  official: string | null
  trial: string | null
}

const advanced = (previous: string | null, next: string | null): boolean =>
  next !== null && previous !== next

/**
 * The set of row ids whose newest completion (official or trial, watched
 * separately so a pause or resume never flashes) advanced within the last
 * few seconds. The same rule as the OBS overlay's highlight, over the
 * extension's own row shape.
 */
export function useChangeFlash(rows: BoardRow[]): Set<string> {
  const previous = useRef<Map<string, Shown>>(new Map())
  const deadlines = useRef<Map<string, number>>(new Map())
  const [active, setActive] = useState<Set<string>>(() => new Set())

  useEffect(() => {
    const flashed: string[] = []
    for (const row of rows) {
      const shown: Shown = { official: row.lastCompletedAt, trial: row.trialLastCompletedAt }
      const prev = previous.current.get(row.userId)
      if (prev && (advanced(prev.official, shown.official) || advanced(prev.trial, shown.trial)))
        flashed.push(row.userId)
      previous.current.set(row.userId, shown)
    }
    if (flashed.length === 0) return

    const expiresAt = Date.now() + FLASH_MS
    for (const id of flashed) deadlines.current.set(id, expiresAt)
    const apply = window.setTimeout(() => setActive(new Set(deadlines.current.keys())), 0)
    const expire = window.setTimeout(() => {
      const now = Date.now()
      for (const [id, deadline] of deadlines.current)
        if (deadline <= now) deadlines.current.delete(id)
      setActive(new Set(deadlines.current.keys()))
    }, FLASH_MS)
    return () => {
      window.clearTimeout(apply)
      window.clearTimeout(expire)
    }
  }, [rows])

  return active
}
