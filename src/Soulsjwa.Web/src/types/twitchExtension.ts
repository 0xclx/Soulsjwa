import type { ObjectiveDetail, ObjectiveOutcome, TieBreakMode, TrialProgress } from './index'

/**
 * Wire types of the Twitch extension backend (`/api/v1/twitch-extension/*`
 * and `/api/v1/me/twitch-extension`), mirroring the C# records in
 * `Features/TwitchExtension/Endpoints/TwitchExtensionEndpoint.cs` one to one.
 * Shared by the Twitch-hosted extension bundle and the app's Broadcast tab.
 */

export const TWITCH_EXTENSION_SCOPES = ['AllGames', 'ActiveGame', 'PinnedGame'] as const
export type TwitchExtensionScope = (typeof TWITCH_EXTENSION_SCOPES)[number]

export const TWITCH_EXTENSION_SCOPE_LABELS: Record<TwitchExtensionScope, string> = {
  AllGames: 'All games (official totals)',
  ActiveGame: 'The active game',
  PinnedGame: 'A game I pick',
}

/** Mirrors the server-side default for a channel with no settings row. */
export const DEFAULT_TWITCH_EXTENSION_SCOPE: TwitchExtensionScope = 'AllGames'

export const TWITCH_EXTENSION_EVENT_SOURCES = ['None', 'Featured', 'Explicit'] as const
export type TwitchExtensionEventSource = (typeof TWITCH_EXTENSION_EVENT_SOURCES)[number]

export interface TwitchExtensionStatus {
  configured: boolean
  clientId: string | null
}

/** The extension-wide rules an admin set; every channel's board and config view honour them. */
export interface TwitchExtensionPolicy {
  allowChannelEventChoice: boolean
  allowViewerScopeSwitch: boolean
  defaultScope: TwitchExtensionScope
  defaultHighlightChannelCompetitor: boolean
  defaultShowTrialProgress: boolean
}

export interface TwitchExtensionSettings {
  /** Null means "follow the featured event". */
  eventId: string | null
  defaultScope: TwitchExtensionScope
  pinnedEventGameId: string | null
  highlightChannelCompetitor: boolean
  showTrialProgress: boolean
}

export interface TwitchExtensionEvent {
  id: string
  name: string
  urlAlias: string | null
  isStarted: boolean
  isFeatured: boolean
  tieBreakMode: TieBreakMode
  activeEventGameId: string | null
  source: TwitchExtensionEventSource
}

export interface TwitchExtensionGame {
  eventGameId: string
  name: string
  isEnabled: boolean
  sortOrder: number
  totalObjectives: number
}

export interface TwitchExtensionEntryGame {
  eventGameId: string
  score: number
  completedCount: number
  failedCount: number
  lastCompletedAt: string | null
  isTrialActive: boolean
  trial: TrialProgress | null
}

export interface TwitchExtensionEntry {
  userId: string
  displayName: string
  twitchLogin: string
  profileImageUrl: string | null
  isLive: boolean
  rank: number
  totalScore: number
  completedCount: number
  failedCount: number
  isFinished: boolean
  status: ObjectiveOutcome
  lastCompletedAt: string | null
  totalInGameTimeMs: number | null
  games: TwitchExtensionEntryGame[]
}

export interface TwitchExtensionScoreboard {
  channelId: string
  policy: TwitchExtensionPolicy
  settings: TwitchExtensionSettings
  /** Null when the channel has nothing to show. */
  event: TwitchExtensionEvent | null
  /** The broadcaster's own competitor row in this event, if they compete. */
  channelCompetitorUserId: string | null
  games: TwitchExtensionGame[]
  entries: TwitchExtensionEntry[]
}

export interface TwitchExtensionCompetitorGameDetail {
  eventGameId: string
  gameName: string
  isEnabled: boolean
  isTrialActive: boolean
  objectives: ObjectiveDetail[]
}

export interface TwitchExtensionCompetitorDetail {
  userId: string
  displayName: string
  games: TwitchExtensionCompetitorGameDetail[]
}

export interface TwitchExtensionEventOptionGame {
  eventGameId: string
  name: string
  isEnabled: boolean
}

export interface TwitchExtensionEventOption {
  id: string
  name: string
  isStarted: boolean
  isFeatured: boolean
  isCompetitor: boolean
  games: TwitchExtensionEventOptionGame[]
}

export interface TwitchExtensionLinkedUser {
  id: string
  displayName: string
  twitchLogin: string
}

export interface TwitchExtensionConfiguration {
  channelId: string
  /** Null until the broadcaster has signed in to Soulsjwa with the channel's Twitch account. */
  linkedUser: TwitchExtensionLinkedUser | null
  policy: TwitchExtensionPolicy
  settings: TwitchExtensionSettings
  resolvedEvent: TwitchExtensionEvent | null
  events: TwitchExtensionEventOption[]
}

export interface UpdateTwitchExtensionConfigurationRequest {
  eventId: string | null
  defaultScope: TwitchExtensionScope
  pinnedEventGameId: string | null
  highlightChannelCompetitor: boolean
  showTrialProgress: boolean
}

export interface TwitchExtensionBundleInfo {
  available: boolean
  fileCount: number
  /** The API origin the downloaded zip will call. */
  apiUrl: string
}

/** What the admin page shows: credentials' state (read-only, from configuration), the bundle, and the rules. */
export interface TwitchExtensionAdminInfo {
  configured: boolean
  clientId: string | null
  canPush: boolean
  extensionOrigin: string | null
  localTestOrigin: string | null
  bundle: TwitchExtensionBundleInfo
  settings: TwitchExtensionPolicy
  updatedAt: string | null
  updatedById: string | null
}

export type UpdateTwitchExtensionSettingsRequest = TwitchExtensionPolicy

/** What the server pushes through Extension PubSub (`TwitchExtensionPushNotifier.PushMessage`). */
export const TWITCH_EXTENSION_PUSH_TYPES = ['scoreboard', 'configuration'] as const
export type TwitchExtensionPushType = (typeof TWITCH_EXTENSION_PUSH_TYPES)[number]

export interface TwitchExtensionPushMessage {
  type: TwitchExtensionPushType
  eventId?: string
  channelId?: string
}
