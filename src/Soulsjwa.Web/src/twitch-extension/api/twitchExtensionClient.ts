import type {
  TwitchExtensionCompetitorDetail,
  TwitchExtensionConfiguration,
  TwitchExtensionEvent,
  TwitchExtensionScoreboard,
  UpdateTwitchExtensionConfigurationRequest,
} from '../../types/twitchExtension'

/**
 * Where the API lives. The bundle runs on Twitch's CDN, so the origin comes
 * from `extension-config.js`, a file the API writes into the zip an admin
 * downloads (see `TwitchExtensionBundle`). Empty during `npm run dev:twitch`,
 * where the dev server proxies `/api` to the local backend, same origin.
 */
export const API_BASE_URL: string = (window.SOULSJWA_TWITCH_EXTENSION?.apiUrl ?? '').replace(
  /\/$/,
  '',
)

const API_PREFIX = '/api/v1/twitch-extension'

/** The web app served by the same host as the API — where "full scoreboard" and "sign in" links go. */
export const SITE_BASE_URL = API_BASE_URL

/** The app's public scoreboard page for an event, by alias when it has one. */
export const scoreboardPageUrl = (event: Pick<TwitchExtensionEvent, 'id' | 'urlAlias'>): string =>
  `${SITE_BASE_URL}/scoreboard/${event.urlAlias ?? event.id}`

export type ConditionalResult<T> =
  { status: 'ok'; data: T; etag: string | null } | { status: 'notModified' }

export class TwitchExtensionApiError extends Error {
  readonly status: number

  constructor(status: number, message: string) {
    super(message)
    this.name = 'TwitchExtensionApiError'
    this.status = status
  }
}

interface RequestOptions {
  method?: 'GET' | 'PUT'
  body?: unknown
  etag?: string | null
}

const headersFor = (token: string, options: RequestOptions): HeadersInit => {
  const headers: Record<string, string> = { Authorization: `Bearer ${token}` }
  if (options.etag) headers['If-None-Match'] = options.etag
  if (options.body !== undefined) headers['Content-Type'] = 'application/json'
  return headers
}

async function problemMessage(response: Response): Promise<string> {
  try {
    const body = (await response.json()) as { detail?: string; title?: string }
    return body.detail ?? body.title ?? response.statusText
  } catch {
    return response.statusText
  }
}

/**
 * One fetch with the Twitch-issued bearer token. `cache: 'no-store'` keeps
 * the browser's own cache out of it so a 304 is a real 304 and the ETag
 * loop below is the only revalidation in play.
 */
async function request<T>(
  path: string,
  token: string,
  options: RequestOptions = {},
): Promise<ConditionalResult<T>> {
  const response = await fetch(`${API_BASE_URL}${API_PREFIX}${path}`, {
    method: options.method ?? 'GET',
    headers: headersFor(token, options),
    body: options.body === undefined ? undefined : JSON.stringify(options.body),
    cache: 'no-store',
  })
  if (response.status === 304) return { status: 'notModified' }
  if (!response.ok)
    throw new TwitchExtensionApiError(response.status, await problemMessage(response))
  return { status: 'ok', data: (await response.json()) as T, etag: response.headers.get('ETag') }
}

export const twitchExtensionClient = {
  scoreboard: (token: string, etag: string | null) =>
    request<TwitchExtensionScoreboard>('/scoreboard', token, { etag }),

  competitorDetail: (token: string, userId: string) =>
    request<TwitchExtensionCompetitorDetail>(`/scoreboard/competitors/${userId}`, token),

  configuration: (token: string) => request<TwitchExtensionConfiguration>('/configuration', token),

  saveConfiguration: (token: string, body: UpdateTwitchExtensionConfigurationRequest) =>
    request<TwitchExtensionConfiguration>('/configuration', token, { method: 'PUT', body }),
}
