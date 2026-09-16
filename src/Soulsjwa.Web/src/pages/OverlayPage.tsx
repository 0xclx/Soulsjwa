import { useMemo } from 'react'
import GlobalStyles from '@mui/material/GlobalStyles'
import { useParams, useSearchParams } from 'react-router-dom'
import { useEvent } from '../features/events/hooks/useEvent'
import { useOverlayScoreboard } from '../features/events/hooks/useOverlayScoreboard'
import {
  applyOverlaySettings,
  isOverlayPreviewRequest,
  parseOverlayConfig,
  type OverlayConfig,
} from '../features/events/overlay/overlayConfig'
import { OverlayRenderer } from '../features/events/overlay/components/OverlayRenderer'
import { useOverlayPreviewFeed } from '../features/events/overlay/useOverlayPreviewFeed'

export const MISSING_TOKEN_MESSAGE = 'Missing overlay token. Add ?token=… to the URL.'
export const INVALID_TOKEN_MESSAGE = 'Overlay token is invalid or has been revoked.'
/** What a preview frame shows until the app that embeds it has posted a look and data. */
export const PREVIEW_WAITING_MESSAGE = 'Waiting for the preview…'

/**
 * The OBS browser-source route. Polls the token-gated scoreboard and draws
 * it with the look the URL asks for — or, once the token has a saved look,
 * with that look, which wins so an edit in the app reaches a source already
 * on screen. In preview mode (`?preview=1`) it polls nothing and instead
 * renders what the app's designer posts into it; see
 * `overlayPreviewProtocol.ts`.
 */
export const OverlayPage = () => {
  const [searchParams] = useSearchParams()
  const urlConfig = useMemo(() => parseOverlayConfig(searchParams), [searchParams])
  return isOverlayPreviewRequest(searchParams) ? (
    <PreviewMode urlConfig={urlConfig} />
  ) : (
    <LiveMode urlConfig={urlConfig} token={searchParams.get('token')} />
  )
}

const LiveMode = ({ urlConfig, token }: { urlConfig: OverlayConfig; token: string | null }) => {
  const { id } = useParams<{ id: string }>()

  // Token-gated overlay endpoint — no cookies, no API key, just an opaque
  // per-event token the streamer pastes into OBS. Polls on a configurable
  // cadence so OBS viewers see fresh data without WebSocket plumbing.
  const { data: event } = useEvent(id!)
  const { data, error } = useOverlayScoreboard(event?.id, token, urlConfig.refreshSeconds)
  const config = useMemo(() => applyOverlaySettings(urlConfig, data?.settings), [urlConfig, data])

  // A missing or invalid token is the most common misconfiguration in OBS:
  // surface a clear, theme-aware message instead of an empty overlay so the
  // streamer can spot it before going live.
  const message = !token ? MISSING_TOKEN_MESSAGE : error ? INVALID_TOKEN_MESSAGE : null

  return (
    <>
      <PageBackground config={config} />
      <OverlayRenderer
        config={config}
        scoreboard={data?.scoreboard}
        eventName={event?.name}
        message={message}
      />
    </>
  )
}

const PreviewMode = ({ urlConfig }: { urlConfig: OverlayConfig }) => {
  const feed = useOverlayPreviewFeed()
  const config = feed?.config ?? urlConfig
  return (
    <>
      <PageBackground config={config} />
      <OverlayRenderer
        config={config}
        scoreboard={feed?.scoreboard}
        eventName={feed?.eventName}
        message={feed ? null : PREVIEW_WAITING_MESSAGE}
      />
    </>
  )
}

/**
 * The app-wide CssBaseline paints an opaque gradient on <body>; override it
 * here so OBS "Browser Source" transparency works (or so a custom `bg` colour
 * shows through) instead of the site background. The same override is what
 * lets the designer's backdrop show through the preview frame.
 */
const PageBackground = ({ config }: { config: OverlayConfig }) => (
  <GlobalStyles
    styles={{ 'html, body, #root': { background: config.background ?? 'transparent' } }}
  />
)
