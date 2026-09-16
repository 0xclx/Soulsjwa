# Database Design

## Provider & Version

- **Database:** PostgreSQL 16
- **ORM:** Entity Framework Core 10.0 (Npgsql provider)
- **Migrations:** Code-first with auto-apply on startup

## Connection

The connection string is configured in `appsettings.json` or via environment variable:

```
ConnectionStrings__DefaultConnection=Host=localhost;Database=soulsjwa;Username=postgres;Password=<password>
```

## Entity Relationship Diagram

```mermaid
erDiagram
    User ||--o{ RefreshToken : "has many"
    User ||--o{ ApiKey : "has many"
    User ||--o{ EventCompetitor : "competes in"
    User ||--o{ CompletedObjective : "completes"
    User ||--o{ FailedObjective : "fails"
    User ||--o{ Event : "creates"
    User ||--o{ EventCompetitorModerator : "moderates as"
    User ||--o{ AllowlistedTwitchLogin : "linked by login"
    User ||--o{ EventOverlayToken : "mints"
    User ||--o{ TwitchExtensionChannelSettings : "last saved"
    User ||--o{ TwitchExtensionSettings : "last saved (nullable)"
    Event ||--o{ TwitchExtensionChannelSettings : "shown on channels (nullable)"
    EventGame ||--o{ TwitchExtensionChannelSettings : "pinned on channels (nullable)"
    User ||--o{ EventGameCompetitorInfo : "attaches"
    User ||--o{ AuditLog : "acts in"
    User ||--o{ AuditLog : "subjects"
    User ||--o{ MediaAsset : "uploads"

    Event ||--o{ EventCompetitor : "has competitors"
    Event ||--o{ EventGame : "includes games"
    Event ||--o{ EventOverlayToken : "has overlay tokens"
    Event ||--o{ AuditLog : "audited by"
    Event ||--o| EventRules : "has rules"
    Event ||--o{ CalendarEntry : "schedules"
    Event ||--o{ PlannedRun : "has planned runs"
    EventGame ||--o{ PlannedRun : "played in"
    User ||--o{ PlannedRun : "plans"
    MediaAsset ||--o{ CalendarEntry : "illustrates (nullable)"

    EventCompetitor ||--o{ EventCompetitorModerator : "delegates"

    EventGame ||--o{ Objective : "has objectives"
    EventGame ||--o{ EventGameCompetitorInfo : "has infos"
    EventGame ||--o| TrialRun : "has at most one trial slot per user"
    EventGame }o--o| Game : "references (nullable)"

    Game ||--o{ Objective : "has predefined objectives"

    Objective ||--o{ CompletedObjective : "completed by"
    Objective ||--o{ FailedObjective : "failed by"

    TrialRun ||--o{ CompletedObjective : "recorded under (cascade delete)"
    TrialRun ||--o{ FailedObjective : "recorded under (cascade delete)"
    User ||--o{ TrialRun : "owns"

    MediaAsset ||--o| SiteTheme : "backgrounds (nullable)"

    SiteTheme {
        int Id PK "always 1 — singleton"
        uuid BackgroundAssetId FK "nullable, restrict-on-delete"
        int BackgroundTreatment "Cover|Contain|Tile|None enum"
        int Font "server-side allowlist enum"
        string LightDefault "app-validated #rrggbb, background reference"
        string LightAccent "app-validated #rrggbb, ≥4.5:1 vs LightDefault"
        string LightDanger "app-validated #rrggbb, ≥4.5:1 vs LightDefault"
        string LightInfo "app-validated #rrggbb, ≥4.5:1 vs LightDefault"
        string LightSuccess "app-validated #rrggbb, ≥4.5:1 vs LightDefault"
        string LightHighlight "app-validated #rrggbb, ≥4.5:1 vs LightDefault"
        string DarkDefault "app-validated #rrggbb, background reference"
        string DarkAccent "app-validated #rrggbb, ≥4.5:1 vs DarkDefault"
        string DarkDanger "app-validated #rrggbb, ≥4.5:1 vs DarkDefault"
        string DarkInfo "app-validated #rrggbb, ≥4.5:1 vs DarkDefault"
        string DarkSuccess "app-validated #rrggbb, ≥4.5:1 vs DarkDefault"
        string DarkHighlight "app-validated #rrggbb, ≥4.5:1 vs DarkDefault"
        datetime UpdatedAt
    }

    EventRules {
        uuid EventId PK "FK to Event"
        string Content "nullable, app-validated ≤ 64 KiB"
        datetime UpdatedAt
    }

    TrialRun {
        uuid Id PK
        uuid EventId FK
        uuid EventGameId FK "UK with UserId — at most one slot per competitor per game"
        uuid UserId FK
        int State "NotStarted|Running|Paused|Completed enum"
        datetime StartedAt "nullable"
        datetime EndedAt "nullable"
    }

    LegalDocument {
        int Kind PK "Impressum|Datenschutz enum, no FK — standalone"
        string Content "nullable, app-validated ≤ 64 KiB"
        datetime UpdatedAt
    }

    FeatureFlag {
        string Key PK "max 100"
        bool Enabled
    }

    MediaAsset {
        uuid Id PK
        string Sha256 UK "hex, max 64; also the on-disk filename"
        string ContentType "max 64, from verified magic bytes"
        int Width
        int Height
        long ByteSize
        uuid CreatedById FK
        datetime CreatedAt
    }

    User {
        uuid Id PK
        string TwitchId UK
        string TwitchLogin
        string DisplayName
        string Email "nullable"
        string ProfileImageUrl "nullable"
        int Role "User|Admin enum"
        bool IsAllowlisted
        datetime CreatedAt
        datetime UpdatedAt
    }

    AllowlistedTwitchLogin {
        uuid Id PK
        string TwitchLogin UK "lowercased, max 64"
        string Note "nullable, max 500"
        uuid AddedById FK "nullable"
        datetime CreatedAt
    }

    RefreshToken {
        uuid Id PK
        uuid UserId FK
        string TokenHash
        string TokenPrefix "indexed"
        datetime ExpiresAt
        datetime CreatedAt
        bool IsRevoked
        datetime RevokedAt "nullable"
    }

    ApiKey {
        uuid Id PK
        uuid UserId FK
        string Name
        string KeyHash
        string KeyPrefix "indexed"
        datetime CreatedAt
        datetime ExpiresAt "nullable"
        datetime LastUsedAt "nullable"
        bool IsRevoked
        string SettingsJson "jsonb, nullable"
    }

    Event {
        uuid Id PK
        string Name "max 200"
        string UrlAlias UK "nullable, lowercase, max 64"
        string Description "max 2000"
        uuid CreatedById FK
        bool IsStarted "default false"
        datetime StartedAt "nullable, last start"
        datetime StoppedAt "nullable, last stop; cleared on start"
        bool IsFeatured "default false, at most one event"
        bool IsArchived "query filter"
        int TieBreakMode "ByTime|SharedPlace enum"
        bool AllowTrialRuns "default true"
        datetime CreatedAt
        datetime UpdatedAt
    }

    CalendarEntry {
        uuid Id PK
        uuid EventId FK
        string Title "max 120"
        string DescriptionMarkdown "nullable, max 64 KiB"
        datetime StartsAt
        datetime EndsAt "> StartsAt"
        bool IsAllDay
        bool IsHighlighted
        int Color "CalendarEntryColor slot enum"
        uuid ImageAssetId FK "nullable"
        uuid CreatedById FK
        datetime CreatedAt
        datetime UpdatedAt
    }

    PlannedRun {
        uuid Id PK
        uuid EventId FK
        uuid EventGameId FK
        uuid UserId FK
        datetime StartsAt
        datetime EndsAt "> StartsAt"
        int Color "CalendarEntryColor slot enum"
        string Note "nullable"
        datetime CreatedAt
        datetime UpdatedAt
    }

    EventCompetitorModerator {
        uuid EventId PK_FK
        uuid CompetitorUserId PK_FK
        uuid ModeratorUserId PK_FK
        datetime AddedAt
    }

    EventCompetitor {
        uuid EventId PK_FK
        uuid UserId PK_FK
        datetime JoinedAt
        bool IsLive "default false"
        bool IsStreamer "default false"
    }

    EventGame {
        uuid Id PK
        uuid EventId FK
        int KnownGameId FK "nullable"
        string CustomGameName "nullable, max 200"
        string CustomGameDescription "nullable, max 1000"
        bool IsEnabled "default false"
        int SortOrder "default 0, display order within event"
    }

    Game {
        int Id PK
        string Name "max 200"
        string Description "max 1000"
        bool ConnectorSupported
        string RequiredConnectorVersion "nullable, max 20"
    }

    Objective {
        uuid Id PK
        uuid EventGameId FK "nullable"
        int GameId FK "nullable"
        string Name "max 200"
        int Score
        string Category "max 100, nullable"
        string Metadata "JSONB, nullable"
        string Rule "JSONB, nullable"
        string FailRule "JSONB, nullable"
        bool IsPredefined
        int SortOrder "default 0, display order within event game"
    }

    CompletedObjective {
        uuid Id PK
        uuid ObjectiveId FK
        uuid UserId FK
        uuid TrialRunId FK "nullable; null = official"
        datetime CompletedAt
        bigint InGameTimeMs "nullable, ms"
    }

    FailedObjective {
        uuid Id PK
        uuid ObjectiveId FK
        uuid UserId FK
        uuid TrialRunId FK "nullable; null = official"
        datetime FailedAt
        bigint InGameTimeMs "nullable, ms"
    }

    TwitchExtensionChannelSettings {
        string ChannelId PK "Twitch channel id, max 32; no FK"
        uuid EventId FK "nullable = follow the featured event, SET NULL"
        int DefaultScope "AllGames|ActiveGame|PinnedGame enum"
        uuid PinnedEventGameId FK "nullable, SET NULL"
        bool HighlightChannelCompetitor "default true"
        bool ShowTrialProgress "default true"
        uuid UpdatedById FK "linked Soulsjwa user, restrict"
        datetime UpdatedAt
    }

    TwitchExtensionSettings {
        int Id PK "always 1"
        bool AllowChannelEventChoice "default true"
        bool AllowViewerScopeSwitch "default true"
        int DefaultScope "AllGames|ActiveGame enum"
        bool DefaultHighlightChannelCompetitor "default true"
        bool DefaultShowTrialProgress "default true"
        uuid UpdatedById FK "nullable, SET NULL"
        datetime UpdatedAt
    }

    EventOverlayToken {
        uuid Id PK
        uuid EventId FK
        string Name "max 100"
        string TokenHash
        string TokenPrefix "max 16, indexed with EventId"
        uuid CreatedById FK
        datetime CreatedAt
        datetime ExpiresAt "nullable"
        datetime LastUsedAt "nullable"
        bool IsRevoked
    }

    EventGameCompetitorInfo {
        uuid Id PK
        uuid EventGameId FK
        uuid UserId FK
        int Type "DeathClip|Link|Other enum"
        string Url "nullable, max 2048"
        string Text "nullable, max 2000"
        uuid CreatedById FK
        datetime CreatedAt
        datetime UpdatedAt
    }

    AuditLog {
        uuid Id PK
        string Type "max 80"
        uuid EventId FK "nullable"
        uuid EventGameId "nullable, indexed, no FK"
        uuid ObjectiveId "nullable, indexed, no FK"
        uuid ActorUserId FK
        uuid SubjectUserId FK "nullable"
        jsonb BeforeJson "nullable"
        jsonb AfterJson "nullable"
        string Reason "nullable, max 2000"
        datetime CreatedAt
    }
```

## Table Details

### Users

| Column | Type | Constraints |
|--------|------|-------------|
| `Id` | `uuid` | PK, auto-generated |
| `TwitchId` | `text` | NOT NULL, **unique index** |
| `TwitchLogin` | `text` | NOT NULL |
| `DisplayName` | `text` | NOT NULL |
| `Email` | `text` | nullable |
| `ProfileImageUrl` | `text` | nullable |
| `Role` | `integer` | NOT NULL, default `0` (`User`); `Admin` = `1` |
| `IsAllowlisted` | `boolean` | NOT NULL, default `false` |
| `CreatedAt` | `timestamptz` | NOT NULL |
| `UpdatedAt` | `timestamptz` | NOT NULL |

### AllowlistedTwitchLogins

| Column | Type | Constraints |
|--------|------|-------------|
| `Id` | `uuid` | PK, auto-generated |
| `TwitchLogin` | `varchar(64)` | NOT NULL, **unique index**, stored lowercase for case-insensitive lookups |
| `Note` | `varchar(500)` | nullable |
| `AddedById` | `uuid` | nullable, FK → Users, ON DELETE SET NULL, indexed |
| `CreatedAt` | `timestamptz` | NOT NULL |

### RefreshTokens

| Column | Type | Constraints |
|--------|------|-------------|
| `Id` | `uuid` | PK, auto-generated |
| `UserId` | `uuid` | FK → Users, ON DELETE CASCADE, indexed |
| `TokenHash` | `text` | NOT NULL |
| `TokenPrefix` | `text` | NOT NULL, indexed |
| `ExpiresAt` | `timestamptz` | NOT NULL, indexed (drives the retention sweep — see below) |
| `CreatedAt` | `timestamptz` | NOT NULL |
| `IsRevoked` | `boolean` | NOT NULL |
| `RevokedAt` | `timestamptz` | nullable |

`RefreshTokens` and `AuditLogs` grow without bound otherwise; a background
`RetentionService` (see `docs/deployment.md`'s `Retention:*` settings) deletes
refresh tokens once they're past `ExpiresAt` plus a grace window (default 7
days) — a revoked-but-unexpired token is deliberately never deleted early,
since `JwtTokenService.ValidateRefreshTokenAsync`'s reuse-detection logic
needs it to still be present. `AuditLogs` retention is disabled by default
(kept forever) and is an explicit operator opt-in.

### ApiKeys

| Column | Type | Constraints |
|--------|------|-------------|
| `Id` | `uuid` | PK, auto-generated |
| `UserId` | `uuid` | FK → Users, ON DELETE CASCADE, indexed |
| `Name` | `text` | NOT NULL |
| `KeyHash` | `text` | NOT NULL |
| `KeyPrefix` | `text` | NOT NULL, indexed |
| `CreatedAt` | `timestamptz` | NOT NULL |
| `ExpiresAt` | `timestamptz` | nullable |
| `LastUsedAt` | `timestamptz` | nullable |
| `IsRevoked` | `boolean` | NOT NULL |

### Games

| Column | Type | Constraints |
|--------|------|-------------|
| `Id` | `integer` | PK, identity |
| `Name` | `varchar(200)` | NOT NULL |
| `Description` | `varchar(1000)` | NOT NULL |
| `ConnectorSupported` | `boolean` | NOT NULL |
| `RequiredConnectorVersion` | `varchar(20)` | nullable |

### Events

| Column | Type | Constraints |
|--------|------|-------------|
| `Id` | `uuid` | PK, auto-generated |
| `Name` | `varchar(200)` | NOT NULL |
| `Description` | `varchar(2000)` | NOT NULL |
| `CreatedById` | `uuid` | FK → Users, ON DELETE RESTRICT, indexed |
| `IsStarted` | `boolean` | NOT NULL, default `false`. Stopping is rejected while any event-game is still `IsEnabled = true`. |
| `IsFeatured` | `boolean` | NOT NULL, default `false`. At most one event is featured at a time — enforced at the database level (see below), not only by `FeatureEvent`'s own unfeature-the-previous logic. Surfaced on the public landing page for every visitor, authenticated or not. |
| `IsArchived` | `boolean` | NOT NULL, default `false` |
| `TieBreakMode` | `integer` | NOT NULL. Controls how tied scores are ranked: `SharedPlace` (`1`, tied competitors share the same rank) or `ByTime` (`0`, first to reach the score wins). New events default to `SharedPlace` via the entity initializer — the column itself has no database default, so events created before the change keep the mode they were stored with. |
| `AllowTrialRuns` | `boolean` | NOT NULL, default `true`. Owner/admin switch gating whether new `TrialRun`s can be created for this event; doesn't retroactively affect trial rows that already exist. |
| `StartedAt` | `timestamptz` | nullable. Set by `POST /events/{id}/start`; kept across a stop. |
| `StoppedAt` | `timestamptz` | nullable. Set by `POST /events/{id}/stop`, cleared by the next start. My Events derives "stopped" (set) versus "upcoming" (never set) from it — it used to read the audit log, which retention deletes. |
| `CreatedAt` | `timestamptz` | NOT NULL |
| `UpdatedAt` | `timestamptz` | NOT NULL |

> **Unique partial index `IX_Events_FeaturedEvent`:** `IsFeatured` with filter `"IsFeatured"` — at most one featured event site-wide, enforced at the database level (mirrors `IX_EventGames_EventId_ActiveGame` below). `FeatureEvent` unfeatures every other event and features the target in one transaction, ordering the clear before the set; a concurrent loser gets a 409, not a silent second featured row.
>
> **Global Query Filter:** `WHERE NOT "IsArchived"` automatically applied to all Event queries. Use `.IgnoreQueryFilters()` to access archived events.

### EventCompetitors (Join Table)

| Column | Type | Constraints |
|--------|------|-------------|
| `EventId` | `uuid` | **Composite PK**, FK → Events, ON DELETE CASCADE |
| `UserId` | `uuid` | **Composite PK**, FK → Users, ON DELETE CASCADE, indexed |
| `JoinedAt` | `timestamptz` | NOT NULL |
| `IsLive` | `boolean` | NOT NULL, default `false` — whether the competitor is currently streaming live |
| `IsStreamer` | `boolean` | NOT NULL, default `false` — marks the competitor as a streamer for moderator delegation and streamer-specific flows |

> **Streamer model:** there is no separate streamers table — a streamer is a competitor with `IsStreamer = true`, and streamer moderators target `EventCompetitorModerators`. (An earlier `MergeStreamersIntoCompetitors` migration made that change; it was folded into `InitialCreate` in the squash.)

### EventCompetitorModerators (Join Table)

| Column | Type | Constraints |
|--------|------|-------------|
| `EventId` | `uuid` | **Composite PK**, part of composite FK → EventCompetitors (`EventId`, `UserId`), ON DELETE CASCADE |
| `CompetitorUserId` | `uuid` | **Composite PK**, part of composite FK → EventCompetitors (`EventId`, `UserId`), ON DELETE CASCADE |
| `ModeratorUserId` | `uuid` | **Composite PK**, FK → Users, ON DELETE CASCADE, indexed |
| `AddedAt` | `timestamptz` | NOT NULL |

### EventGames

| Column | Type | Constraints |
|--------|------|-------------|
| `Id` | `uuid` | PK, auto-generated |
| `EventId` | `uuid` | FK → Events, ON DELETE CASCADE |
| `KnownGameId` | `integer` | nullable, FK → Games, ON DELETE CASCADE, indexed |
| `CustomGameName` | `varchar(200)` | nullable (required when KnownGameId is null) |
| `CustomGameDescription` | `varchar(1000)` | nullable |
| `IsEnabled` | `boolean` | NOT NULL, default `false` |
| `SortOrder` | `integer` | NOT NULL, default `0` — display order within the event, ascending; set via the games reorder endpoint |

> **Non-unique Index:** `(EventId, KnownGameId)` with filter `KnownGameId IS NOT NULL` — used for lookup performance. The same predefined game can appear multiple times per event with different custom names (e.g., "ER-1", "ER-2"). Custom games (KnownGameId = null) are identified by their UUID.
>
> **Unique partial index `IX_EventGames_EventId_ActiveGame`:** `EventId` with filter `"IsEnabled"` — at most one enabled ("active") game per event, enforced at the database level. Enabling a game (`POST .../enable`) clears the flag on every other game in the same event via `ExecuteUpdateAsync` (its own statement, ordered before the set — this partial index is checked per-statement, not at commit) then sets it on the target, both inside one transaction; this index is the backstop that makes the invariant unbreakable even by a future code path that forgets to, and a concurrent loser gets a 409 rather than an uncaught 500.
>
> **Lifecycle Guard:** Objective completion and connector submissions are only allowed when the parent event has `IsStarted = true` and the specific event-game has `IsEnabled = true`. Games can only be added/removed while the event is stopped, and a game can only be enabled while the event is running (it may be disabled at any time).

### Objectives

| Column | Type | Constraints |
|--------|------|-------------|
| `Id` | `uuid` | PK, auto-generated |
| `EventGameId` | `uuid` | nullable, FK → EventGames, ON DELETE CASCADE, indexed |
| `GameId` | `integer` | nullable, FK → Games, ON DELETE CASCADE, indexed |
| `Name` | `varchar(200)` | NOT NULL |
| `Score` | `integer` | NOT NULL |
| `Category` | `varchar(100)` | nullable (grouping label, e.g. Elden Ring area; used by the OBS overlay to group objectives) |
| `Metadata` | `jsonb` | nullable |
| `Rule` | `jsonb` | nullable (JsonLogic format) |
| `FailRule` | `jsonb` | nullable (JsonLogic format; evaluated after the completion rule) |
| `IsPredefined` | `boolean` | NOT NULL |
| `SortOrder` | `integer` | NOT NULL, default `0` — display order within the event game, ascending; only meaningful for event-scoped objectives (`EventGameId` non-null); set via the objectives reorder endpoint |

> **Dual FK Pattern:** Predefined objectives have `GameId` set and `EventGameId = null`. Event-specific objectives have `EventGameId` set and `GameId = null`.

### CompletedObjectives

| Column | Type | Constraints |
|--------|------|-------------|
| `Id` | `uuid` | PK, auto-generated |
| `ObjectiveId` | `uuid` | FK → Objectives, ON DELETE CASCADE |
| `UserId` | `uuid` | FK → Users, ON DELETE CASCADE, indexed |
| `CompletedAt` | `timestamptz` | NOT NULL |
| `InGameTimeMs` | `bigint` | nullable — in-game time in milliseconds at completion (connector-reported; null for manual completions) |
| `TrialRunId` | `uuid` | nullable FK → TrialRuns, ON DELETE CASCADE — null for an official completion, set for one recorded during that trial run |

> **Unique indexes (partial, not one plain index):** `(ObjectiveId, UserId) WHERE "TrialRunId" IS NULL` — at most one *official* completion per user per objective; `(ObjectiveId, UserId, TrialRunId) WHERE "TrialRunId" IS NOT NULL` — at most one completion per user per objective *within a given trial run*. Two partial indexes rather than one plain unique index on all three columns, because Postgres treats `NULL` as distinct in a unique index — a plain index would let a user rack up multiple official completions of the same objective.

### FailedObjectives

| Column | Type | Constraints |
|--------|------|-------------|
| `Id` | `uuid` | PK, auto-generated |
| `ObjectiveId` | `uuid` | FK → Objectives, ON DELETE CASCADE |
| `UserId` | `uuid` | FK → Users, ON DELETE CASCADE, indexed |
| `FailedAt` | `timestamptz` | NOT NULL |
| `InGameTimeMs` | `bigint` | nullable — in-game time in milliseconds at failure |
| `TrialRunId` | `uuid` | nullable FK → TrialRuns, ON DELETE CASCADE — same trial semantics as `CompletedObjectives.TrialRunId` |

> **Unique indexes:** same official/per-trial-run partial-index pair as `CompletedObjectives`, above. Completion and failure writers use a transaction-scoped PostgreSQL advisory lock per event game so concurrent requests cannot persist conflicting outcomes.

### TrialRuns

A competitor's trial/training "slot" for one (event, game) pair — at most one per (`EventGameId`, `UserId`). Enabling trial mode creates the row; disabling it deletes the row outright, cascading away every `CompletedObjectives`/`FailedObjectives` row that references it. Official scoring always filters `TrialRunId IS NULL`, so trial progress never reaches an official total, a competitor's ranking, or a cross-competitor fail-rule cascade, whether or not the game is the event's active game. The two are also mutually exclusive per (`EventGameId`, `UserId`): the row cannot be created while official completions or failures exist for that pair, and while the row exists in any state other than `Running` no official row can be written either (see `TrialRunLookup`) — so a pair is either official or trial, never both, and only disabling returns it to official. It *is* read back out separately — as `GameBreakdown.trial` on the scoreboard/overlay payload and via `GET /me/trial-runs` — using the FK index on `TrialRunId`, so the official queries keep their own partial `TrialRunId IS NULL` indexes to themselves.

| Column | Type | Constraints |
|--------|------|-------------|
| `Id` | `uuid` | PK, auto-generated |
| `EventId` | `uuid` | FK → Events, ON DELETE CASCADE |
| `EventGameId` | `uuid` | FK → EventGames, ON DELETE CASCADE |
| `UserId` | `uuid` | FK → Users, ON DELETE CASCADE |
| `State` | `int` | `NotStarted\|Running\|Paused\|Completed` enum |
| `StartedAt` | `timestamptz` | nullable |
| `EndedAt` | `timestamptz` | nullable |

> **Unique Index:** `(EventGameId, UserId)` — at most one trial slot per competitor per game.

### CalendarEntries

An admin/owner-authored calendar entry for one event — a scheduled milestone, announcement, or highlight shown on the global calendar. Readable by everyone; written by the event owner or an admin. Rendered through the shared Markdown pipeline. `Color` is a semantic slot (`CalendarEntryColor`), resolved client-side against the site theme palette — never a hex value — so entries restyle with the theme instead of drifting from it.

| Column | Type | Constraints |
|--------|------|-------------|
| `Id` | `uuid` | PK, auto-generated |
| `EventId` | `uuid` | FK → Events, ON DELETE CASCADE, indexed with `StartsAt` |
| `Title` | `varchar(120)` | NOT NULL |
| `DescriptionMarkdown` | `text` | nullable |
| `StartsAt` | `timestamptz` | NOT NULL |
| `EndsAt` | `timestamptz` | NOT NULL, app-validated `> StartsAt` |
| `IsAllDay` | `boolean` | NOT NULL |
| `IsHighlighted` | `boolean` | NOT NULL |
| `Color` | `integer` | NOT NULL — `CalendarEntryColor` enum: `Default`=0, `Accent`=1, `Danger`=2, `Info`=3, `Success`=4, `Highlight`=5 |
| `ImageAssetId` | `uuid` | nullable, FK → MediaAssets, ON DELETE RESTRICT, indexed |
| `CreatedById` | `uuid` | FK → Users, ON DELETE RESTRICT, indexed |
| `CreatedAt` | `timestamptz` | NOT NULL |
| `UpdatedAt` | `timestamptz` | NOT NULL |

### PlannedRuns

A competitor's scheduled play session for one of an event's games — several may exist per competitor. Edited from the competitor tile; write access mirrors `EventOwnership.RequireCanEditCompetitorInfoAsync` (the competitor themselves, the event owner, an admin, or a delegated moderator). Readable by everyone — the global calendar renders these as "Event name · game · competitor · time", colored by `Color`.

| Column | Type | Constraints |
|--------|------|-------------|
| `Id` | `uuid` | PK, auto-generated |
| `EventId` | `uuid` | FK → Events, ON DELETE CASCADE, indexed with `StartsAt` |
| `EventGameId` | `uuid` | FK → EventGames, ON DELETE CASCADE, indexed |
| `UserId` | `uuid` | FK → Users, ON DELETE CASCADE, indexed with `StartsAt` |
| `StartsAt` | `timestamptz` | NOT NULL |
| `EndsAt` | `timestamptz` | NOT NULL, app-validated `> StartsAt` |
| `Color` | `integer` | NOT NULL, default `Default` — same `CalendarEntryColor` enum as `CalendarEntry.Color`, chosen by the competitor/manager when creating the run |
| `CreatedAt` | `timestamptz` | NOT NULL |
| `UpdatedAt` | `timestamptz` | NOT NULL |

### EventRules

An event's rules document — 1:1 with Events, keyed by `EventId`. No row exists until an owner/admin first sets it (`GET` returns `content: null`). Rendered through the shared Markdown pipeline (`renderMarkdown`/`MarkdownView`).

| Column | Type | Constraints |
|--------|------|-------------|
| `EventId` | `uuid` | PK, FK → Events, ON DELETE CASCADE |
| `Content` | `text` | nullable, app-validated ≤ 64 KiB (not a DB constraint) |
| `UpdatedAt` | `timestamptz` | NOT NULL |

### EventOverlayTokens

OBS browser-source tokens for the streamer overlay. The raw token is only shown once at creation; only the SHA-256 hash is stored.

| Column | Type | Constraints |
|--------|------|-------------|
| `Id` | `uuid` | PK, auto-generated |
| `EventId` | `uuid` | FK → Events, ON DELETE CASCADE |
| `Name` | `varchar(100)` | NOT NULL |
| `TokenHash` | `text` | NOT NULL (SHA-256 of raw token, base64-encoded) |
| `TokenPrefix` | `varchar(16)` | NOT NULL, indexed with `EventId` |
| `CreatedById` | `uuid` | FK → Users, ON DELETE RESTRICT, indexed |
| `CreatedAt` | `timestamptz` | NOT NULL |
| `LastUsedAt` | `timestamptz` | nullable |
| `IsRevoked` | `boolean` | NOT NULL |
| `ExpiresAt` | `timestamptz` | nullable. Null means the token never expires — only true for rows minted before expiry was introduced (BE-005); tokens minted since default to 90 days from `CreatedAt`. Checked alongside `IsRevoked` in `GetOverlayScoreboard`. |
| `SettingsJson` | `jsonb` | nullable. The token's saved look (`OverlayTokenSettings`: view, theme, pins, timings, toggles, panel opacity, title) as the same camelCase JSON the API sends and receives; read on every overlay poll so a change reaches an OBS source already on screen. Null for a token whose URL parameters alone drive the overlay (every token minted before looks existed). Validated in the endpoint, not the database. |

### TwitchExtensionChannelSettings

What one Twitch channel's installed extension shows ([twitch-extension.md](twitch-extension.md)). At most one row per channel, keyed on the channel's Twitch user id — the `channel_id` claim of the tokens Twitch issues for it, and the same value as `Users.TwitchId` for the broadcaster's own account, but deliberately without an FK to Users: the id names a channel whether or not its broadcaster ever signed in here. No row is the common case and means "follow the featured event with defaults". A row exists only once a broadcaster with a linked Soulsjwa account saved settings, so `UpdatedById` is always a real user and the write is audited.

| Column | Type | Constraints |
|--------|------|-------------|
| `ChannelId` | `varchar(32)` | PK |
| `EventId` | `uuid` | nullable, FK → Events, ON DELETE SET NULL, indexed. Null = follow the featured event. An archived pick is kept but resolves to the featured event. |
| `DefaultScope` | `integer` | NOT NULL — `TwitchExtensionScope` enum: `AllGames`=0, `ActiveGame`=1, `PinnedGame`=2 |
| `PinnedEventGameId` | `uuid` | nullable, FK → EventGames, ON DELETE SET NULL, indexed; app-validated to belong to the shown event |
| `HighlightChannelCompetitor` | `boolean` | NOT NULL, default `true` |
| `ShowTrialProgress` | `boolean` | NOT NULL, default `true` |
| `UpdatedById` | `uuid` | FK → Users, ON DELETE RESTRICT, indexed |
| `UpdatedAt` | `timestamptz` | NOT NULL |

Mapped with the `xmin` concurrency token like the other mutable entities; a concurrent save returns `409`.

### TwitchExtensionSettings

The admin-set rules that apply to every channel's extension ([twitch-extension.md](twitch-extension.md#extension-wide-rules)). A singleton like `SiteThemes`: `Id` is always `1` (`TwitchExtensionSettings.SingletonId`, `ValueGeneratedNever`), and no row means the code defaults (`TwitchExtensionSettings.Defaults()`), so a fresh database behaves exactly as before the table existed. The row is created by the first admin save and is never deleted.

| Column | Type | Constraints |
|--------|------|-------------|
| `Id` | `integer` | PK, always `1` |
| `AllowChannelEventChoice` | `boolean` | NOT NULL, default `true`. `false` makes every channel follow the featured event regardless of its own `EventId`. |
| `AllowViewerScopeSwitch` | `boolean` | NOT NULL, default `true` |
| `DefaultScope` | `integer` | NOT NULL — `TwitchExtensionScope` enum, app-validated to `AllGames`=0 or `ActiveGame`=1 (a pinned game needs an event) |
| `DefaultHighlightChannelCompetitor` | `boolean` | NOT NULL, default `true` |
| `DefaultShowTrialProgress` | `boolean` | NOT NULL, default `true` |
| `UpdatedById` | `uuid` | nullable, FK → Users, ON DELETE SET NULL, indexed |
| `UpdatedAt` | `timestamptz` | NOT NULL |

The three `Default*` columns are only read for channels without a `TwitchExtensionChannelSettings` row. Mapped with the `xmin` concurrency token; the admin endpoint honours `If-Match` and returns `409` on a concurrent save.

### EventGameCompetitorInfos

Per-competitor per-game supplementary info (death clips, links, notes).

| Column | Type | Constraints |
|--------|------|-------------|
| `Id` | `uuid` | PK, auto-generated |
| `EventGameId` | `uuid` | FK → EventGames, ON DELETE CASCADE, indexed with `UserId` |
| `UserId` | `uuid` | FK → Users, ON DELETE CASCADE, indexed |
| `Type` | `integer` | NOT NULL — `DeathClip` (0), `Link` (1), `Other` (2) |
| `Url` | `varchar(2048)` | nullable — required for `DeathClip`/`Link` |
| `Text` | `varchar(2000)` | nullable — required for `Other`; optional caption for others |
| `CreatedById` | `uuid` | FK → Users, ON DELETE RESTRICT, indexed |
| `CreatedAt` | `timestamptz` | NOT NULL |
| `UpdatedAt` | `timestamptz` | NOT NULL |

### AuditLogs

Append-only audit records for user-driven mutations. Audit rows outlive the rows they reference where possible, so event and subject references are nulled instead of cascading. `EventGameId` and `ObjectiveId` are indexed scope identifiers only; they are not configured as foreign keys.

| Column | Type | Constraints |
|--------|------|-------------|
| `Id` | `uuid` | PK, auto-generated |
| `Type` | `varchar(80)` | NOT NULL, indexed; string key from `AuditEventTypes` |
| `EventId` | `uuid` | nullable, FK → Events, ON DELETE SET NULL, indexed with `CreatedAt` |
| `EventGameId` | `uuid` | nullable, indexed, no FK |
| `ObjectiveId` | `uuid` | nullable, indexed, no FK |
| `ActorUserId` | `uuid` | FK → Users, ON DELETE RESTRICT, indexed |
| `SubjectUserId` | `uuid` | nullable, FK → Users, ON DELETE SET NULL, indexed |
| `BeforeJson` | `jsonb` | nullable (snapshot before the change; null for creates) |
| `AfterJson` | `jsonb` | nullable (snapshot after the change; null for deletes) |
| `Reason` | `varchar(2000)` | nullable |
| `CreatedAt` | `timestamptz` | NOT NULL, indexed |

### FeatureFlags

Runtime product switches keyed by a stable string. `myevents.quick_complete.enabled` is seeded disabled and can be changed by admins without redeploying; updates are recorded in `AuditLogs`.

| Column | Type | Constraints |
|--------|------|-------------|
| `Key` | `varchar(100)` | PK |
| `Enabled` | `boolean` | NOT NULL |

### MediaAssets

Metadata for uploaded images, content-addressed by the SHA-256 of their (post metadata-stripping) bytes. The file itself lives on disk under the `Media:RootPath`-configured media root, named by `Sha256` — the uploaded filename never reaches the filesystem. Two uploads of identical bytes share one row and one file (`MediaStore`, `Features/Media/Services/MediaStore.cs`); `Width`/`Height`/`ContentType` come from `ImageValidator`'s magic-byte parsing, never from the caller's declared `Content-Type` or filename.

| Column | Type | Constraints |
|--------|------|-------------|
| `Id` | `uuid` | PK, auto-generated |
| `Sha256` | `varchar(64)` | NOT NULL, unique, indexed |
| `ContentType` | `varchar(64)` | NOT NULL |
| `Width` | `integer` | NOT NULL |
| `Height` | `integer` | NOT NULL |
| `ByteSize` | `bigint` | NOT NULL |
| `CreatedById` | `uuid` | FK → Users, ON DELETE RESTRICT, indexed |
| `CreatedAt` | `timestamptz` | NOT NULL |

### LegalDocuments

Two singleton legal documents (`Impressum`, `Datenschutz`), keyed by the `LegalDocumentKind` enum. No FK to any other table. No row exists until an admin first sets it (`GET` returns `content: null`), same lazy-creation pattern as `EventRules`. Rendered through the shared Markdown pipeline. Per SPEC assumption 9, legal completeness is the operator's responsibility — no field validation, no compliance checklist.

| Column | Type | Constraints |
|--------|------|-------------|
| `Kind` | `integer` | PK (`LegalDocumentKind` enum: `Impressum`=0, `Datenschutz`=1) |
| `Content` | `text` | nullable, app-validated ≤ 64 KiB (not a DB constraint) |
| `UpdatedAt` | `timestamptz` | NOT NULL |

### SiteThemes

Singleton row (`Id` is always `1`, seeded via `HasData` so it exists on a fresh database — unlike `EventRules`/`LegalDocuments`, a site always has *some* rendered theme, so there's no "unset" state to model). Applied to every page. `BackgroundAssetId` is a nullable FK to `MediaAssets` — never a URL, so there is no SSRF surface. `BackgroundTreatment`/`Font` are `int`-converted enums (`BackgroundTreatment`: `Cover`=0, `Contain`=1, `Tile`=2, `None`=3; `SiteFont`: a fixed self-hosted/system-stack allowlist, no arbitrary font name and no remote font URL).

The 12 palette columns are two modes (`Light`/`Dark`) of six named slots — `Default`, `Accent`, `Danger`, `Info`, `Success`, `Highlight` — validated app-side (not a DB constraint) by `SiteThemeValidator`: every value must be `#rrggbb`, and every slot but `Default` must reach WCAG AA (4.5:1) contrast against its mode's `Default`, since `Default` is the page-background reference colour the other five render as text/accents on top of. `event-calendar`'s `CalendarEntryColor` (a later module) resolves 1:1 against these same six slot names, so adding or removing one is a breaking change for that module.

| Column | Type | Constraints |
|--------|------|-------------|
| `Id` | `integer` | PK, identity — application logic only ever reads/writes the row where `Id = 1` |
| `BackgroundAssetId` | `uuid` | nullable, FK → MediaAssets, ON DELETE RESTRICT, indexed |
| `BackgroundTreatment` | `integer` | NOT NULL |
| `Font` | `integer` | NOT NULL |
| `LightDefault` / `LightAccent` / `LightDanger` / `LightInfo` / `LightSuccess` / `LightHighlight` | `text` | NOT NULL, app-validated `#rrggbb` + contrast |
| `DarkDefault` / `DarkAccent` / `DarkDanger` / `DarkInfo` / `DarkSuccess` / `DarkHighlight` | `text` | NOT NULL, app-validated `#rrggbb` + contrast |
| `UpdatedAt` | `timestamptz` | NOT NULL |

## Optimistic Concurrency

Eleven mutable entities are mapped with `e.Property<uint>("xmin").IsRowVersion()` in
`OnModelCreating`, which makes Postgres's free `xmin` system column (already present on
every table, bumped by Postgres on every update to a row) a shadow concurrency-token
property: `Event`, `Objective`, `EventGame`, `SiteTheme`, `EventRules`, `LegalDocument`,
`CalendarEntry`, `PlannedRun`, `EventGameCompetitorInfo`, `TwitchExtensionChannelSettings`, `TwitchExtensionSettings`. No schema change is required —
`UPDATE`s against these tables get an implicit `AND xmin = @original_xmin` clause, and
zero affected rows raises `DbUpdateConcurrencyException`, which the relevant endpoints
translate to `409 Conflict`.

Deliberately **not** mapped, because the token would buy nothing:
- `AuditLog` — insert-only, never updated.
- `CompletedObjective` / `FailedObjective` — already guarded by partial unique indexes and the per-event-game advisory lock (`ObjectiveOutcomeLock`).
- `RefreshToken` — rotation already uses a conditional `ExecuteUpdateAsync` (see `JwtTokenService.TryRevokeForRotationAsync`) instead.
- `ApiKey`, `EventCompetitor`, `EventCompetitorModerator` — no full-replace write path exists for these; last-write-wins on their few individual flags is harmless.

`SiteTheme`, `EventRules` and `LegalDocument` — the three full-replace document
endpoints, where a lost update is most damaging — additionally surface the token over
HTTP: `GET` sets the response `ETag` header to the current `xmin`; `PUT` accepts an
optional `If-Match` request header and, when present, checks the write against that
exact version instead of whatever this request just (re)loaded. A stale `If-Match`
returns `409`. **A missing `If-Match` is accepted and behaves exactly as it did before
this token existed** — surfacing it is additive, not a breaking requirement. Because
`xmin` changes on *any* update to a row, including ones this application didn't make
(e.g. a manual `UPDATE` or a future code path), a client holding a token that's stale
for an unrelated reason also gets `409` — that's expected, not a bug.

`CalendarEntry` doesn't have a single-item `GET` to hang an `ETag` off of (only a list
endpoint), so it surfaces the token as an ordinary `Version` field on
`CalendarEntryResponse` instead, optionally accepted back on `UpdateCalendarEntryRequest`
— same semantics, just carried in the body rather than an HTTP header.

`Event`, `Objective` and `EventGame` carry the token in the model (so a same-request
race is still caught) but their `PATCH` endpoints don't surface it over HTTP: those
patches only ever touch the fields present in the request body, so two admins editing
different fields concurrently don't lose either other's change the way a full-replace
`PUT` would (see BE-014 in [`history/backend-review.md`](history/backend-review.md) for the full reasoning).

## Index Summary

| Table | Index Columns | Type |
|-------|--------------|------|
| Users | `TwitchId` | Unique |
| AllowlistedTwitchLogins | `TwitchLogin` | Unique |
| AllowlistedTwitchLogins | `AddedById` | Non-unique |
| RefreshTokens | `TokenPrefix` | Non-unique |
| RefreshTokens | `UserId` | Non-unique |
| RefreshTokens | `ExpiresAt` | Non-unique |
| ApiKeys | `KeyPrefix` | Non-unique |
| ApiKeys | `UserId` | Non-unique |
| Events | `CreatedById` | Non-unique |
| Events | `CreatedAt` | Non-unique (list ordering) |
| Events | `IsFeatured` | Unique (filtered: `"IsFeatured"`). At most one featured event. |
| Events | `UrlAlias` | Unique (filtered: `"UrlAlias" IS NOT NULL`) |
| Events | `Name`, `Description` | GIN trigram (`gin_trgm_ops`) — `ListEvents` search |
| Users | `LOWER("TwitchLogin")` | Functional, non-unique (raw SQL in the migration) |
| EventCompetitors | `(EventId, UserId)` | Composite PK |
| EventCompetitors | `UserId` | Non-unique |
| EventCompetitors | `(EventId, IsStreamer)` | Non-unique |
| EventCompetitorModerators | `(EventId, CompetitorUserId, ModeratorUserId)` | Composite PK |
| EventCompetitorModerators | `ModeratorUserId` | Non-unique |
| EventCompetitorModerators | `(EventId, ModeratorUserId)` | Non-unique |
| EventGames | `KnownGameId` | Non-unique |
| EventGames | `(EventId, KnownGameId)` | Non-unique (filtered: `KnownGameId IS NOT NULL`). Same game can appear multiple times per event. |
| EventGames | `EventId` | Unique (filtered: `"IsEnabled"`). At most one enabled game per event. |
| Objectives | `EventGameId` | Non-unique |
| Objectives | `GameId` | Non-unique |
| CompletedObjectives | `(ObjectiveId, UserId)` | Unique (filtered: `"TrialRunId" IS NULL`). At most one official completion per user per objective. |
| CompletedObjectives | `(ObjectiveId, UserId, TrialRunId)` | Unique (filtered: `"TrialRunId" IS NOT NULL`). At most one completion per user per objective per trial run. |
| CompletedObjectives | `UserId` | Non-unique |
| CompletedObjectives | `TrialRunId` | Non-unique |
| FailedObjectives | `(ObjectiveId, UserId)` | Unique (filtered: `"TrialRunId" IS NULL`) |
| FailedObjectives | `(ObjectiveId, UserId, TrialRunId)` | Unique (filtered: `"TrialRunId" IS NOT NULL`) |
| FailedObjectives | `UserId` | Non-unique |
| FailedObjectives | `TrialRunId` | Non-unique |
| TrialRuns | `(EventGameId, UserId)` | Unique. At most one trial slot per competitor per game. |
| TrialRuns | `EventId` | Non-unique |
| TrialRuns | `UserId` | Non-unique |
| CalendarEntries | `(EventId, StartsAt)` | Non-unique |
| CalendarEntries | `StartsAt` | Non-unique |
| CalendarEntries | `CreatedById` | Non-unique |
| CalendarEntries | `ImageAssetId` | Non-unique |
| PlannedRuns | `(EventId, StartsAt)` | Non-unique |
| PlannedRuns | `StartsAt` | Non-unique |
| PlannedRuns | `EventGameId` | Non-unique |
| PlannedRuns | `(UserId, StartsAt)` | Non-unique |
| EventOverlayTokens | `CreatedById` | Non-unique |
| EventOverlayTokens | `(EventId, TokenPrefix)` | Non-unique |
| TwitchExtensionChannelSettings | `EventId` | Non-unique (the push notifier and the config views look channels up by event) |
| TwitchExtensionChannelSettings | `PinnedEventGameId` | Non-unique |
| TwitchExtensionChannelSettings | `UpdatedById` | Non-unique |
| TwitchExtensionSettings | `UpdatedById` | Non-unique |
| EventGameCompetitorInfos | `CreatedById` | Non-unique |
| EventGameCompetitorInfos | `UserId` | Non-unique |
| EventGameCompetitorInfos | `(EventGameId, UserId)` | Non-unique |
| AuditLogs | `ActorUserId` | Non-unique |
| AuditLogs | `CreatedAt` | Non-unique |
| AuditLogs | `EventGameId` | Non-unique |
| AuditLogs | `ObjectiveId` | Non-unique |
| AuditLogs | `SubjectUserId` | Non-unique |
| AuditLogs | `Type` | Non-unique |
| AuditLogs | `(EventId, CreatedAt)` | Non-unique |
| MediaAssets | `Sha256` | Unique |
| MediaAssets | `CreatedById` | Non-unique |
| SiteThemes | `BackgroundAssetId` | Non-unique |

## Seed Data

### Games (5 seeded from `games.json`, all connector-supported)

| ID | Name | Connector Supported | Required Version |
|----|------|:-------------------:|:----------------:|
| 1 | Dark Souls: Remastered | ✅ | 3.2.0 |
| 2 | Dark Souls II: Scholar of the First Sin | ✅ | 3.2.0 |
| 3 | Dark Souls III | ✅ | 3.2.0 |
| 6 | Sekiro: Shadows Die Twice | ✅ | 3.2.0 |
| 9 | Elden Ring | ✅ | 3.3.0 |

### Predefined Objectives

Predefined objectives are defined in `PredefinedObjectives.cs` for all connector-supported games: boss kills, and — where the SoulMemory catalogs provide them — bonfires/graces/idols, known progression flags and item pickups. Each uses a JsonLogic rule (`{">":[{"var":"<flagId>"},0]}` for event-flag games) to detect completion. Counts below are what the seeder inserts at the time of writing (`SELECT "GameId", count(*) FROM "Objectives" WHERE "IsPredefined" AND "EventGameId" IS NULL GROUP BY 1`); the score column is what boss objectives get, every other kind scores 1.

| Game | Objectives (total) | of which score 10 (main bosses) | Score per regular boss |
|------|:------------------:|:-------------------------------:|:----------------------:|
| Elden Ring — Game 9 | 677 | 26 | 1 |
| Dark Souls: Remastered — Game 1 | 121 | 9 | 1 |
| Dark Souls II: SOTFS — Game 2 | 41 | 10 | 1 |
| Dark Souls III — Game 3 | 102 | 8 | 1 |
| Sekiro: Shadows Die Twice — Game 6 | 70 | 8 | 1 |

The seeder keys the catalog on `(GameId, Rule)` — compared in canonical JSON form, since `jsonb` normalises what it stores — never on the display name, because the same boss can appear at several locations under one name. Importing into an event (`POST .../import-predefined`) keys the same way.

The Elden Ring boss list (211 entries, deduped to 208 unique defeat flags — a
few duo-fight bosses share a flag) comes from
[`SoulMemoryCatalogData.EldenRingBosses`](../src/Soulsjwa.Api/Features/Games/Definitions/SoulMemoryCatalogData.cs),
generated from `FrankvdStam/SoulSplitter`'s `SoulMemory` source by
`tools/generate_soulmemory_catalog.py` (CI fails if the committed file
differs from what the generator emits). Elden Ring previously had its own curated
`EldenRingBossData.cs` table ported from the (now-unused, save-file-reading)
`Hapfel1/er-save-manager` project; that table and its generator were removed
once the connector moved fully to live-memory reading via SoulMemory, and a
handful of `Group` values known to be wrong in the SoulMemory catalog are
corrected in `PredefinedObjectives.EldenRingBossCategoryOverrides`.

Every boss objective's `Category` is the boss's in-game location (e.g. "Anor Londo", "Ashina Castle") — DS1R, DS2 SOTFS, DS3 and Sekiro used to fall back to a placeholder `"main"`/`"regular"` split; that split now only decides `Score` (`RegularBossScore` vs `RemembranceBossScore`), never `Category`. Boss locations for these four games are generated from a hand-curated `LOCATIONS` table in `tools/generate_soulmemory_boss_data.py` (not from a third-party project, unlike Elden Ring) — the generator fails loudly if any boss is missing a location, and locations the curator wasn't fully confident in are both emitted normally and listed in `tools/uncertain-boss-locations.md` for later review.

## Migration History

The project is pre-release (no production database to preserve compatibility
with), so its migration history was condensed to a single baseline rather
than carrying 40+ incremental migrations forward indefinitely. The schema
below is the result; nothing about the schema itself changed in the squash.

| Migration | Date | Description |
|-----------|------|-------------|
| `20260915142021_AddOverlayTokenSettings` | 2026-09-15 | Adds the nullable `EventOverlayTokens.SettingsJson` jsonb column holding a token's saved overlay look. |
| `20260915055747_AddTwitchExtensionSettings` | 2026-09-15 | Adds the `TwitchExtensionSettings` singleton table (extension-wide rules set by admins) with its nullable `UpdatedById` FK and index. |
| `20260914184451_AddTwitchExtensionChannelSettings` | 2026-09-14 | Adds the `TwitchExtensionChannelSettings` table (one row per Twitch channel showing the extension) with its three FKs and indexes. |
| `20260913171833_AddEventLifecycleTimestamps` | 2026-09-13 | Adds `Events.StartedAt`/`StoppedAt`, backfilled from `event.started`/`event.stopped` audit rows where those still exist. |
| `20260913011520_InitialCreate` | 2026-09-13 | The full current schema — every table, index, and constraint described throughout this document — in one migration. Two raw-SQL statements have no corresponding EF model change and are called out in the migration's own comments: `CREATE EXTENSION IF NOT EXISTS pg_trgm` (required by the `IX_Events_Name_Trgm`/`IX_Events_Description_Trgm` GIN trigram indexes, BE-019) and `CREATE INDEX "IX_Users_TwitchLogin_Lower" ON "Users" (LOWER("TwitchLogin"))` (a functional index EF's fluent API cannot express declaratively, BE-031). The `xmin` shadow concurrency-token property on nine entities (see "Optimistic Concurrency" above) generates `AddColumn`/`DropColumn` calls for a column named `xmin` that Npgsql's migrations SQL generator recognizes as a Postgres system column and silently no-ops at apply time — confirmed against a real database: no such column is ever actually created. |

## Creating a New Migration

```bash
dotnet ef migrations add <MigrationName> \
  --project src/Soulsjwa.Api \
  --output-dir Infrastructure/Data/Migrations
```

## Applying Migrations

**Automatic (Development/Docker):**
Migrations are applied automatically on startup when:
- The environment is `Development`, OR
- The `APPLY_MIGRATIONS` environment variable is `true`

**Manual:**

```bash
dotnet ef database update --project src/Soulsjwa.Api
```
