import type { OverlayTokenSettings } from './overlay'

export type UserRole = 'User' | 'Admin'

export interface User {
  id: string
  twitchLogin: string
  displayName: string
  email?: string
  profileImageUrl?: string
  createdAt: string
  role: UserRole
  isAllowlisted: boolean
}

export interface ApiKey {
  id: string
  name: string
  keyPrefix: string
  createdAt: string
  expiresAt?: string
  lastUsedAt?: string
  isRevoked: boolean
}

export interface AccessTokenResponse {
  accessToken: string
}

export interface PaginatedResponse<T> {
  items: T[]
  // Omitted (null) on a keyset/cursor page — computing it would
  // defeat the point of avoiding OFFSET on a large table. Offset-mode
  // responses (the only kind any current page uses) always populate it.
  totalCount: number | null
  page: number
  pageSize: number
  // Present when a full page was returned, meaning there may be more;
  // absent on the last page. Pass it back as the `cursor` query param to
  // fetch the next page without OFFSET.
  nextCursor?: string | null
  hasNextPage: boolean
  hasPreviousPage: boolean
}

export interface AuditUserSummary {
  id: string
  displayName: string
  twitchLogin: string
}

export interface AuditLog {
  id: string
  type: string
  eventId: string | null
  eventGameId: string | null
  objectiveId: string | null
  actor: AuditUserSummary
  subject: AuditUserSummary | null
  beforeJson: string | null
  afterJson: string | null
  reason: string | null
  createdAt: string
}

export type AuditLogPage = PaginatedResponse<AuditLog>

export interface EditCompletionTimeRequest {
  completedAt: string
  reason: string
}

export const TIE_BREAK_MODES = ['ByTime', 'SharedPlace'] as const
export type TieBreakMode = (typeof TIE_BREAK_MODES)[number]

export const TIE_BREAK_MODE_LABELS: Record<TieBreakMode, string> = {
  ByTime: 'By completion time (1st, 2nd, 3rd)',
  SharedPlace: 'Shared place (ties share rank: 1, 1, 3)',
}

/** Mirrors the server-side default applied to newly created events. */
export const DEFAULT_TIE_BREAK_MODE: TieBreakMode = 'SharedPlace'

export const OBJECTIVE_OUTCOMES = ['Pending', 'Completed', 'Failed'] as const
export type ObjectiveOutcome = (typeof OBJECTIVE_OUTCOMES)[number]

export const OBJECTIVE_OUTCOME_LABELS: Record<ObjectiveOutcome, string> = {
  Pending: 'Pending',
  Completed: 'Completed',
  Failed: 'Failed',
}

export interface EventResponse {
  id: string
  name: string
  urlAlias: string | null
  description: string
  createdById: string
  isArchived: boolean
  isStarted: boolean
  isFeatured: boolean
  tieBreakMode: TieBreakMode
  /** Owner/admin switch (default true) gating whether competitors may enable trial/training runs. */
  allowTrialRuns: boolean
  createdAt: string
  updatedAt: string
  competitors: EventCompetitor[]
  games: EventGame[]
}

/**
 * The events list view's row shape: counts instead of the full
 * competitor/game/objective graph, which only the detail page
 * (EventResponse, from GET /events/{id}) needs.
 */
export interface EventListItem {
  id: string
  name: string
  urlAlias: string | null
  description: string
  createdById: string
  isArchived: boolean
  isStarted: boolean
  isFeatured: boolean
  tieBreakMode: TieBreakMode
  allowTrialRuns: boolean
  createdAt: string
  updatedAt: string
  competitorCount: number
  gameCount: number
}

export const TRIAL_RUN_STATES = ['NotStarted', 'Running', 'Paused', 'Completed'] as const
export type TrialRunState = (typeof TRIAL_RUN_STATES)[number]

export const TRIAL_RUN_STATE_LABELS: Record<TrialRunState, string> = {
  NotStarted: 'Not started',
  Running: 'Running',
  Paused: 'Paused',
  Completed: 'Completed',
}

export const TRIAL_RUN_STATE_COLORS: Record<TrialRunState, 'default' | 'warning' | 'success'> = {
  NotStarted: 'default',
  Running: 'warning',
  Paused: 'default',
  Completed: 'success',
}

/**
 * The one state in which the server attributes a new completion to the run,
 * so it is also the only state in which a UI may offer to tick objectives.
 */
export const TRIAL_RUN_RECORDING_STATE: TrialRunState = 'Running'

export interface TrialRun {
  id: string
  eventId: string
  eventGameId: string
  userId: string
  state: TrialRunState
  startedAt: string | null
  endedAt: string | null
}

export const MY_EVENT_STATUSES = ['live', 'upcoming', 'stopped', 'archived'] as const
export type MyEventStatus = (typeof MY_EVENT_STATUSES)[number]

export const MY_EVENT_STATUS_LABELS: Record<MyEventStatus, string> = {
  live: 'Live',
  upcoming: 'Upcoming',
  stopped: 'Stopped',
  archived: 'Archived',
}

export const MY_EVENT_ACTIVITY_LABELS = {
  objective_completed: 'completed objective',
  clip_added: 'added clip',
  note_added: 'added note',
} as const

export type MyEventActivityType = keyof typeof MY_EVENT_ACTIVITY_LABELS

export interface MyCompetitorEvent {
  eventId: string
  eventName: string
  urlAlias: string | null
  status: MyEventStatus
  score: number
  rank: number
  totalCompetitors: number
  incompleteObjectives: number
  totalObjectives: number
  lastActivity: string | null
  lastActivityType: MyEventActivityType | null
  failedObjectives: number
  completionStatus: ObjectiveOutcome
}

export interface MyDelegatedEvent extends MyCompetitorEvent {
  competitorId: string
  competitorName: string
}

export interface MyOwnedEvent {
  eventId: string
  eventName: string
  urlAlias: string | null
  status: MyEventStatus
  competitorCount: number
  lastActivity: string | null
  lastActivityType: MyEventActivityType | null
}

export interface MyEventsResponse {
  competitor: MyCompetitorEvent[]
  delegated: MyDelegatedEvent[]
  owned: MyOwnedEvent[]
  quickCompleteEnabled: boolean
}

export interface MyEventObjective {
  objectiveId: string
  name: string
  completed: boolean
  completedAt: string | null
  score: number
  failed: boolean
  failedAt: string | null
  /** Boss objectives carry their in-game location here; other objectives may use it for any grouping label. */
  category?: string | null
}

export interface MyEventGame {
  gameId: string
  gameName: string
  objectives: MyEventObjective[]
  /**
   * A trial is recording for this game, so anything ticked here would be
   * attributed to that run and never appear in this official-only list. Used
   * to tell the viewer where their ticks are going; it carries no trial
   * figures of its own.
   */
  isTrialActive: boolean
  /**
   * A trial slot exists for this game, recording or not. The server refuses
   * every official write while one does, so this — not `isTrialActive` — is
   * what makes these controls read-only.
   */
  hasTrialRun: boolean
}

export interface MyTrialRun {
  trialRunId: string
  eventId: string
  eventName: string
  urlAlias: string | null
  eventGameId: string
  gameName: string
  isGameEnabled: boolean
  competitorId: string
  competitorName: string
  isOwnTrial: boolean
  state: TrialRunState
  startedAt: string | null
  score: number
  completedCount: number
  failedCount: number
  totalObjectives: number
  lastCompletedAt: string | null
}

/**
 * What `POST .../objectives/fail-remaining` did. Mirrors the API's
 * `FailRemainingObjectivesResponse` record.
 */
export interface FailRemainingObjectivesResponse {
  failedCount: number
  alreadyCompletedCount: number
  alreadyFailedCount: number
  totalObjectives: number
}

export interface MyEventObjectivesResponse {
  competitorId: string
  competitorName: string
  games: MyEventGame[]
}

export interface FeatureFlag {
  key: string
  enabled: boolean
}

export interface EventModerator {
  userId: string
  displayName: string
  addedAt: string
}

export interface AllowlistEntry {
  id: string
  twitchLogin: string
  note?: string
  createdAt: string
  addedById?: string
  linkedUserId?: string
  linkedDisplayName?: string
}

export interface AdminUserSummary {
  id: string
  twitchLogin: string
  displayName: string
  role: UserRole
  isAllowlisted: boolean
  createdAt: string
}

export interface UserSearchResult {
  id: string
  twitchLogin: string
  displayName: string
  profileImageUrl?: string
  /** True when this user was pre-created as a placeholder and has never logged in. */
  isPending: boolean
}

export interface EventCompetitor {
  userId: string
  displayName: string
  joinedAt: string
  isStreamer: boolean
  isLive: boolean
  moderators: EventModerator[]
}

export interface EventGame {
  eventGameId: string
  knownGameId: number | null
  gameName: string
  knownGameName: string | null
  connectorSupported: boolean
  requiredConnectorVersion?: string
  isCustomGame: boolean
  isEnabled: boolean
  customGameDescription?: string
  objectives: Objective[]
}

export interface Objective {
  id: string
  name: string
  score: number
  /** Grouping label (e.g. Elden Ring area) shown wherever objectives are grouped by location/category. */
  category?: string | null
  metadata?: string
  rule?: string
  failRule?: string
  isPredefined: boolean
}

export interface PredefinedObjective {
  id: string
  gameId: number
  name: string
  score: number
  category?: string | null
  metadata?: string
  rule?: string
  failRule?: string
}

export interface GameResponse {
  id: number
  name: string
  description: string
  connectorSupported: boolean
  requiredConnectorVersion?: string
}

export interface GameDataPoint {
  id: string
  displayName: string
}

export interface ScoreEntry {
  userId: string
  displayName: string
  twitchLogin: string
  profileImageUrl?: string
  isLive: boolean
  totalScore: number
  completedCount: number
  isFinished: boolean
  lastCompletedAt: string | null
  totalInGameTimeMs: number | null
  rank: number
  failedCount: number
  status: ObjectiveOutcome
  /**
   * The competitor has a started trial, so the official figures above may be
   * frozen while they practise. Matches the display rule used everywhere
   * else (paused runs included), not the narrower "still recording" flag.
   * The trial's own score lives on the scoreboard's per-game `trial`.
   */
  isTrialing: boolean
}

export interface ScoreboardResponse {
  entries: ScoreboardEntry[]
  tieBreakMode: TieBreakMode
}

export interface ScoreboardEntry {
  userId: string
  displayName: string
  twitchLogin: string
  profileImageUrl?: string
  isLive: boolean
  totalScore: number
  completedCount: number
  isFinished: boolean
  lastCompletedAt: string | null
  totalInGameTimeMs: number | null
  rank: number
  games: GameBreakdown[]
  failedCount: number
  status: ObjectiveOutcome
}

export interface GameBreakdown {
  eventGameId: string
  gameName: string
  score: number
  completedCount: number
  totalObjectives: number
  objectives: ObjectiveDetail[]
  infos: CompetitorInfo[]
  hasDeathClip: boolean
  failedCount: number
  isEnabled: boolean
  /** `state === 'Running'` — narrower than `trial !== null`, which also covers a paused run. */
  isTrialActive: boolean
  /**
   * A trial slot exists for this game in any state, including `NotStarted`
   * (which reports no `trial` figures at all). Official writes are refused
   * for as long as it does, so this is the flag that gates official controls;
   * `isTrialActive` says where a tick *would* go, and `trial !== null` says
   * whether there are figures to display.
   */
  hasTrialRun: boolean
  /**
   * The competitor's own trial/training progress for this game, reported
   * beside the official figures above and never folded into them. Present
   * once the run has started (so also while paused), and present whether or
   * not `isEnabled`. There is deliberately no per-competitor equivalent on
   * `ScoreboardEntry`: sum this across the games you actually display, so the
   * total stays right when the visible game set is filtered.
   */
  trial: TrialProgress | null
}

export interface TrialProgress {
  trialRunId: string
  state: TrialRunState
  score: number
  completedCount: number
  failedCount: number
  lastCompletedAt: string | null
}

/**
 * One objective's state inside the enclosing game's `trial`, independent of
 * the official `isCompleted`/`isFailed` on the same objective — a trial is a
 * fresh attempt whose rows are discarded when it is reset or disabled.
 */
export interface TrialObjectiveState {
  isCompleted: boolean
  completedAt: string | null
  isFailed: boolean
  failedAt: string | null
  status: ObjectiveOutcome
}

export type CompetitorInfoType = 'DeathClip' | 'Link' | 'Other'

export interface CompetitorInfo {
  id: string
  eventGameId: string
  userId: string
  type: CompetitorInfoType
  url: string | null
  text: string | null
  createdById: string
  createdAt: string
  updatedAt: string
}

export interface ObjectiveDetail {
  objectiveId: string
  name: string
  score: number
  /** Grouping label (e.g. Elden Ring area) used to group objectives in the overlay. */
  category: string | null
  isCompleted: boolean
  completedAt: string | null
  isFailed: boolean
  failedAt: string | null
  status: ObjectiveOutcome
  trial: TrialObjectiveState | null
}

export interface OverlayToken {
  id: string
  name: string
  tokenPrefix: string
  createdById: string
  createdAt: string
  lastUsedAt: string | null
  /** Defaults to 90 days from creation; null means it never expires (a token minted before this existed). */
  expiresAt: string | null
  /** The saved look of the token's OBS source, or null when its URL alone drives it. */
  settings: OverlayTokenSettings | null
}

export interface OverlayTokenWithSecret extends OverlayToken {
  /** Raw token; only ever returned at creation time. */
  token: string
}

export interface UploadMediaResponse {
  assetId: string
  url: string
  width: number
  height: number
  byteSize: number
  contentType: string
}

export interface EventRules {
  content: string | null
  updatedAt: string | null
}

export const LEGAL_DOCUMENT_KINDS = ['Impressum', 'Datenschutz'] as const
export type LegalDocumentKind = (typeof LEGAL_DOCUMENT_KINDS)[number]

export interface LegalDocument {
  content: string | null
  updatedAt: string | null
}

export const BACKGROUND_TREATMENTS = ['Cover', 'Contain', 'Tile', 'None'] as const
export type BackgroundTreatment = (typeof BACKGROUND_TREATMENTS)[number]

export const SITE_FONTS = ['SystemSansSerif', 'SystemSerif', 'SystemMonospace'] as const
export type SiteFont = (typeof SITE_FONTS)[number]

/** The site theme's six named palette slots — the public vocabulary
 * `CalendarEntryColor` (`types/calendar.ts`) resolves against 1:1. */
export const SITE_THEME_PALETTE_SLOTS = [
  'Default',
  'Accent',
  'Danger',
  'Info',
  'Success',
  'Highlight',
] as const
export type SiteThemePaletteSlot = (typeof SITE_THEME_PALETTE_SLOTS)[number]

export interface SiteTheme {
  backgroundAssetId: string | null
  backgroundUrl: string | null
  backgroundTreatment: BackgroundTreatment
  font: SiteFont
  lightDefault: string
  lightAccent: string
  lightDanger: string
  lightInfo: string
  lightSuccess: string
  lightHighlight: string
  darkDefault: string
  darkAccent: string
  darkDanger: string
  darkInfo: string
  darkSuccess: string
  darkHighlight: string
  updatedAt: string
}

export interface UpdateSiteThemeRequest {
  backgroundAssetId: string | null
  backgroundTreatment: BackgroundTreatment
  font: SiteFont
  lightDefault: string
  lightAccent: string
  lightDanger: string
  lightInfo: string
  lightSuccess: string
  lightHighlight: string
  darkDefault: string
  darkAccent: string
  darkDanger: string
  darkInfo: string
  darkSuccess: string
  darkHighlight: string
}
