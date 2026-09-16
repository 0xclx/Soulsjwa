import { useEffect } from 'react'
import { useScoreboardPolling } from '../scoreboard/useScoreboardPolling'
import { useTwitchExtension } from '../twitch/useTwitchExtension'
import { Board } from './Board'
import { SettingsForm } from './SettingsForm'
import type { ExtensionView } from '../views'

export const WAITING_FOR_TWITCH_LABEL = 'Connecting to Twitch…'
export const NO_HELPER_LABEL = 'This page only works inside Twitch.'
export const NO_EVENT_TITLE = 'No event right now'
export const NO_EVENT_BODY =
  'The scoreboard appears here as soon as an event is featured or the streamer picks one.'
export const LOADING_LABEL = 'Loading scoreboard…'
export const RETRY_LABEL = 'Retry'

interface ExtensionAppProps {
  view: ExtensionView
}

/**
 * Root of every entry page: waits for Twitch to hand over the viewer's token,
 * mirrors Twitch's theme onto the document, then renders the board (viewer
 * views) or the settings form (broadcaster views).
 */
export function ExtensionApp({ view }: ExtensionAppProps) {
  const { auth, theme, isVisible, helperMissing } = useTwitchExtension()
  const isViewer = view === 'panel' || view === 'component' || view === 'mobile'
  const polling = useScoreboardPolling(isViewer ? auth : null, isVisible)

  useEffect(() => {
    document.documentElement.dataset.theme = theme
    document.documentElement.dataset.view = view
  }, [theme, view])

  if (helperMissing) return <Notice title={NO_HELPER_LABEL} />
  if (!auth) return <Notice title={WAITING_FOR_TWITCH_LABEL} />

  if (!isViewer) return <SettingsForm token={auth.token} live={view === 'live_config'} />

  if (!polling.board) {
    return polling.error ? (
      <Notice title={polling.error} action={{ label: RETRY_LABEL, onClick: polling.refresh }} />
    ) : (
      <Notice title={LOADING_LABEL} />
    )
  }
  if (!polling.board.event) return <Notice title={NO_EVENT_TITLE} body={NO_EVENT_BODY} />

  return (
    <Board
      board={polling.board}
      token={auth.token}
      lastUpdatedAt={polling.lastUpdatedAt}
      error={polling.error}
      compact={view === 'component'}
    />
  )
}

function Notice({
  title,
  body,
  action,
}: {
  title: string
  body?: string
  action?: { label: string; onClick: () => void }
}) {
  return (
    <div className="sx-notice" role="status">
      <strong>{title}</strong>
      {body && <p>{body}</p>}
      {action && (
        <button type="button" className="sx-notice__action" onClick={action.onClick}>
          {action.label}
        </button>
      )}
    </div>
  )
}
