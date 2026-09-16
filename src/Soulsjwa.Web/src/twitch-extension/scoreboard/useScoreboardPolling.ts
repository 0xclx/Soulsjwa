import { useCallback, useEffect, useRef, useState } from 'react'
import type {
  TwitchExtensionPushMessage,
  TwitchExtensionScoreboard,
} from '../../types/twitchExtension'
import { twitchExtensionClient, TwitchExtensionApiError } from '../api/twitchExtensionClient'
import type { TwitchExtensionAuth } from '../twitch/useTwitchExtension'

/** While the event runs a completion should show within seconds; otherwise the board barely moves. */
export const STARTED_POLL_MS = 5_000
export const IDLE_POLL_MS = 30_000
/** After an error, back off but keep trying — the panel must recover on its own when the API returns. */
export const ERROR_RETRY_MS = 10_000

export interface ScoreboardPolling {
  board: TwitchExtensionScoreboard | null
  /** Human-readable, null while the last request succeeded. */
  error: string | null
  /** When the board was last confirmed current (a 200 or a 304), for the "updated Ns ago" stamp. */
  lastUpdatedAt: number | null
  /** Fetch now, outside the schedule — what a push message and the retry button call. */
  refresh: () => void
}

const describe = (error: unknown): string =>
  error instanceof TwitchExtensionApiError
    ? error.status === 401
      ? 'Twitch session expired. Reload the page.'
      : `Scoreboard unavailable (${error.status}).`
    : 'Scoreboard unavailable. Retrying…'

/**
 * Polls the channel's board with the viewer's Twitch token, revalidating by
 * ETag so an unchanged board costs a bodiless 304, pausing while Twitch
 * reports the view hidden, and refetching at once when the server pushes a
 * "changed" ping for the event on screen.
 */
export function useScoreboardPolling(
  auth: TwitchExtensionAuth | null,
  isVisible: boolean,
): ScoreboardPolling {
  const [board, setBoard] = useState<TwitchExtensionScoreboard | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [lastUpdatedAt, setLastUpdatedAt] = useState<number | null>(null)
  const [generation, setGeneration] = useState(0)
  const etag = useRef<string | null>(null)
  const shownEventId = useRef<string | null>(null)
  // Read inside the polling closure without restarting the loop on every board.
  const isStartedRef = useRef(false)

  const refresh = useCallback(() => setGeneration((g) => g + 1), [])

  useEffect(() => {
    isStartedRef.current = board?.event?.isStarted ?? false
  }, [board])

  useEffect(() => {
    if (!auth || !isVisible) return
    const token = auth.token
    let cancelled = false
    let timer: ReturnType<typeof window.setTimeout> | null = null

    const schedule = (delay: number) => {
      if (cancelled) return
      timer = window.setTimeout(() => void poll(), delay)
    }

    const poll = async () => {
      try {
        const result = await twitchExtensionClient.scoreboard(token, etag.current)
        if (cancelled) return
        if (result.status === 'ok') {
          etag.current = result.etag
          shownEventId.current = result.data.event?.id ?? null
          setBoard(result.data)
        }
        setError(null)
        setLastUpdatedAt(Date.now())
        schedule(isStartedRef.current ? STARTED_POLL_MS : IDLE_POLL_MS)
      } catch (caught) {
        if (cancelled) return
        setError(describe(caught))
        schedule(ERROR_RETRY_MS)
      }
    }

    void poll()
    return () => {
      cancelled = true
      if (timer !== null) window.clearTimeout(timer)
    }
    // `generation` restarts the loop on demand (push message, retry button).
  }, [auth, isVisible, generation])

  useEffect(() => {
    const ext = window.Twitch?.ext
    if (!ext) return
    const onMessage = (_target: string, _contentType: string, raw: string) => {
      let message: TwitchExtensionPushMessage
      try {
        message = JSON.parse(raw) as TwitchExtensionPushMessage
      } catch {
        return
      }
      if (message.type === 'configuration') refresh()
      else if (message.type === 'scoreboard' && message.eventId === shownEventId.current) refresh()
    }
    ext.listen('global', onMessage)
    ext.listen('broadcast', onMessage)
    return () => {
      ext.unlisten('global', onMessage)
      ext.unlisten('broadcast', onMessage)
    }
  }, [refresh])

  return { board, error, lastUpdatedAt, refresh }
}
