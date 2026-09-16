# Feature Matrix — What's Done & What's Missing

## Legend

| Symbol | Meaning |
|:------:|---------|
| ✅ | Fully implemented |
| 🟡 | Partially implemented |
| ❌ | Not implemented |
| 🔧 | API exists, no frontend/connector UI |

---

## Authentication & Authorization

| Feature | API | Web | Connector | Notes |
|---------|:---:|:---:|:---------:|-------|
| Twitch OAuth2 login | ✅ | ✅ | — | Full code flow with state validation |
| JWT access tokens (15 min) | ✅ | ✅ | — | In-memory storage, auto-refresh |
| Refresh token rotation | ✅ | ✅ | — | httpOnly cookie, SHA256 hashed |
| Logout / token revocation | ✅ | ✅ | — | Clears cookie + revokes in DB |
| API key authentication | ✅ | — | ✅ | `X-Api-Key` header, `sk_` prefix |
| API key creation | ✅ | ✅ | — | Name + optional expiry |
| API key listing | ✅ | ✅ | — | Shows prefix, not full key |
| API key revocation | ✅ | ✅ | — | Soft-delete |
| Rate limiting (auth) | ✅ | — | — | 20 req/min fixed window |
| Rate limiting (global) | ✅ | — | — | 100 req/min (auth'd) / 60 req/min (anon by IP) |
| Rate limiting (connector) | ✅ | — | — | 120 req/min per user with queue |
| Event owner authorization | ✅ | 🟡 | — | API enforces; UI shows owner-only buttons |
| Competitor authorization | ✅ | ❌ | — | API enforces for objective completion |
| **Admin role (`User.Role`)** | ✅ | ✅ | — | Admin nav link, admin-only buttons across UI |
| **First-admin bootstrap** | ✅ | — | — | `Admin:BootstrapTwitchLogin` config; first matching Twitch login becomes admin |
| **Twitch login allowlist** | ✅ | ✅ | — | Non-allowlisted sign-ins rejected with friendly callback error |
| **Admin allowlist mgmt UI** | ✅ | ✅ | — | `/admin` page → Allowlist tab |
| **User role management** | ✅ | ✅ | — | `/admin` page → Users tab; last-admin demotion blocked |
| **Per-event competitor list** | ✅ | ✅ | — | Owner/admin adds/removes competitors; streamer flag controls delegation |
| **Streamer competitor moderator delegation** | ✅ | ✅ | — | Streamer competitor or admin manages; per-event, never carries over |
| **On-behalf-of objective completion** | ✅ | ✅ | — | Authorized target picker controls whose objective checkboxes are updated |

## Audits & Activity

| Feature | API | Web | Connector | Notes |
|---------|:---:|:---:|:---------:|-------|
| Per-event activity log | ✅ | ✅ | — | Event detail Activity tab shows event-scoped audit entries |
| Admin audit log | ✅ | ✅ | — | `/admin` Audits tab lists cross-event activity for admins |
| Audit persistence | ✅ | — | — | Auth, event, game, objective, competitor, overlay-token, trial-run, media, theme, and admin actions are written through `AuditService`; the SPA's `auditEventTypes.ts` mirrors the backend's type list |

## Events

| Feature | API | Web | Connector | Notes |
|---------|:---:|:---:|:---------:|-------|
| List events (paginated) | ✅ | ✅ | — | 20/page default, max 100 |
| View event detail | ✅ | ✅ | — | With competitors, games, objectives |
| Create event | ✅ | ✅ | — | Admin-only (403 for regular users) |
| Edit event | ✅ | ✅ | — | Owner/admin can edit name, description, tie-break mode, and a globally unique URL alias |
| Archive event (soft-delete) | ✅ | ✅ | — | Owner archive button hides events from default listings |
| Event start/stop lifecycle | ✅ | ✅ | — | Owner can toggle `Started` / `Stopped`; objective completion and connector submissions require started events |
| Unarchive event | ✅ | ✅ | — | Owner/admin can restore archived events |
| View archived events | ✅ | ✅ | — | Admin event list can include archived events |
| Delete event (hard) | ❌ | ❌ | — | Not implemented |
| Event search/filter | ✅ | ✅ | — | Event list supports name/description search and status filters |
| My Events assignment/objective summary | ✅ | ✅ | — | Authenticated role-grouped dashboard with live polling, lazy objectives, and feature-flagged optimistic quick completion; its own route (`/my-events`, `authOnly` nav entry) — the home page no longer redirects signed-in visitors here, so everyone sees the featured scoreboard |
| Feature an event | ✅ | ✅ | — | Admin-only; featuring one event unfeatures the previous one. Basic info + scoreboard surfaced on the public landing page for every visitor, authenticated or not |
| Public scoreboard deep link | ✅ | ✅ | — | `/scoreboard/:eventIdentifier` — chrome-free (no nav/tabs/footer), resolves by event id or `UrlAlias`, one shareable link per event |
| Duplicate event | ✅ | ✅ | — | Admin-only; copies games + objectives only (no competitors, rules, calendar entries, or history); works on archived sources. Duplicate button on both the events list and the event detail header, navigating to the copy |

## Site & Content

| Feature | API | Web | Connector | Notes |
|---------|:---:|:---:|:---------:|-------|
| Site theme | ✅ | ✅ | — | Singleton admin-defined palette (six slots × light/dark, WCAG AA checked), font allowlist, uploaded background image; delivered as MUI palette values |
| Legal documents (Impressum / Datenschutz) | ✅ | ✅ | — | Admin Markdown editor with ETag/If-Match; public pages 404 while empty; baseline templates under `templates/legal/` |
| Media uploads | ✅ | ✅ | — | Admin-only, content-addressed by SHA-256, magic-byte sniffed, metadata stripped, atomic write |
| Event rules | ✅ | ✅ | — | Per-event Markdown rules page; the featured event's rules are linked from the nav |
| Feature flags | ✅ | ✅ | — | Runtime flags (`/admin/feature-flags`), e.g. My Events quick complete |

## Event Calendar

| Feature | API | Web | Connector | Notes |
|---------|:---:|:---:|:---------:|-------|
| Admin calendar entries (per event) | ✅ | ✅ | — | `CalendarEntry` CRUD, owner/admin write via `RequireOwner`, public read; `EndsAt > StartsAt` validated server-side; `Color` is a semantic `CalendarEntryColor` slot resolved against the site theme, never a hex value; editor on the event's Calendar tab (title, `MarkdownEditor` description, from/to with all-day toggle, highlight, colour-slot selector, image upload) |
| Competitor planned runs | ✅ | ✅ | — | `PlannedRun` CRUD, write access mirrors `EventOwnership.RequireCanEditCompetitorInfoAsync` (competitor, owner, admin, or delegated moderator); public read; editor on the competitor tile |
| Global `/calendar` page | ✅ | ✅ | — | `GET /api/v1/calendar` aggregates every non-archived event's entries and planned runs; rendered with FullCalendar (month view, list view on narrow screens), inside an `overflow-x: auto` container |

## Competitors

| Feature | API | Web | Connector | Notes |
|---------|:---:|:---:|:---------:|-------|
| Add competitor to event | ✅ | ✅ | — | Owner/admin picker supports existing users, Twitch handle pre-invites, and optional streamer flag |
| Remove competitor | ✅ | ✅ | — | Owner/admin can remove competitors from event detail |
| View competitors | ✅ | ✅ | — | Chip list on event detail |
| User search (for adding) | ✅ | ✅ | — | Authenticated `/users/search` powers user picker |
| Self-join event | ✅ | ✅ | — | Signed-in users can join from event detail |
| Invite link / code | ❌ | ❌ | — | No invite system |

## Games

| Feature | API | Web | Connector | Notes |
|---------|:---:|:---:|:---------:|-------|
| List all games | ✅ | ✅ | — | 5 seeded games, all read live via SoulMemory |
| Add game to event | ✅ | ✅ | — | Owner dialog assigns predefined games with optional display overrides |
| Add custom game to event | ✅ | ✅ | — | Per-event custom games with name & description |
| Remove game from event | ✅ | ✅ | — | Unified UUID-based delete for predefined and custom |
| Enable/disable event game | ✅ | ✅ | — | Owner can toggle `Enabled` / `Disabled`; completion and connector submissions require enabled games |
| View game data definitions | ✅ | ✅ | — | Used by rule builder |
| Create new game | ✅ | ✅ | — | Admin Catalog tab creates global game definitions |
| Edit game metadata | ✅ | ✅ | — | Admin Catalog tab edits name and description |

## Objectives

| Feature | API | Web | Connector | Notes |
|---------|:---:|:---:|:---------:|-------|
| List predefined objectives | ✅ | ✅ | — | Admin Catalog tab and per-event import dialog |
| Create predefined objective | ✅ | ✅ | — | Admin Catalog tab creates game-specific templates |
| Assign predefined to event | ✅ | ✅ | — | Owner import dialog supports all or selected objectives |
| Create custom objective | ✅ | ✅ | — | With optional Blockly rule builder |
| Edit objective | ✅ | ✅ | — | Owner can edit objective name, score, completion rule, and failure rule |
| Delete objective | ✅ | ✅ | — | Owner can delete objectives; completions cascade |
| View event objectives | ✅ | ✅ | — | Listed under each game in event detail |
| Import predefined objectives | ✅ | ✅ | — | Owner/admin dialog imports all or a selected subset for a game |
| Visual rule builder (Blockly) | — | ✅ | — | Converts to JsonLogic |

## Scoring & Completion

| Feature | API | Web | Connector | Notes |
|---------|:---:|:---:|:---------:|-------|
| Manual objective completion | ✅ | ✅ | — | Competitors, admins, and delegated moderators can tick objectives in event detail |
| Manual objective uncompletion | ✅ | ✅ | — | Same controls can clear completed objectives |
| Completion-time editing | ✅ | ✅ | — | Owner/admin dialog adjusts objective completion timestamps for corrections |
| Auto-completion via connector | ✅ | — | ✅ | API evaluates rules; connector reads live process memory + submits |
| Manual objective failure/reset | ✅ | ✅ | — | Uses the same self/delegated authorization model as completion |
| Fail all remaining objectives | ✅ | ✅ | — | One call per game (`objectives/fail-remaining`) fails every objective still pending for a competitor — the end of a no-death run — leaving completed and already-failed ones alone; offered behind a confirmation naming the game, count and competitor wherever single objectives can be failed (event Games tab, My Events, Delegated, Trial tab) |
| Auto-failure via connector | ✅ | ✅ | ✅ | JsonLogic fail rules support game state and `competitorCompletions`; completion wins when both match |
| Terminal objective outcome | ✅ | ✅ | — | Competitor is terminal when every objective is completed or failed |
| In-game time tiebreaker | ✅ | ✅ | ✅ | `InGameTimeMs` stored per completion; scoreboard uses it to rank ties fairly across play sessions |
| Tie break mode | ✅ | ✅ | — | `SharedPlace` (default — equal scores share rank regardless of completion time) or `ByTime` (first to score); configurable per event |
| Scoreboard (scores) | ✅ | ✅ | — | Ranked table on event detail; finished status, tiebreaker by in-game time then completion time |
| Per-user score breakdown | ✅ | ✅ | — | Full scoreboard page with per-game and per-objective details; expandable rows |
| Competitor live status | ✅ | ✅ | — | Red/green indicator on scoreboard; toggled by competitor, streamer moderator, or admin |
| Twitch profile link | — | ✅ | — | Click Twitch icon on scoreboard to visit streamer's channel |
| Death clip / info attachments | ✅ | ✅ | — | Competitor info editor manages DeathClip, Link, and Other attachments |
| OBS streamer overlay | ✅ | ✅ | — | Token-gated `/events/:id/overlay` route; objectives grouped Game→Category→Objective, plus scores/games views. The look is designed against a live in-app preview (real or sample data) and saved on the token, so a source already in OBS follows edits within its refresh interval; see [streamer-overlay.md](streamer-overlay.md) |
| Twitch extension (viewer panel + video component) | ✅ | ✅ | — | Twitch-hosted bundle (built in the Docker image, downloaded from the admin tab) backed by `/twitch-extension/*`; Twitch-issued viewer JWTs, per-channel 5s cache with ETag, all-games / active-game / any-game scopes, YOU row, on-demand objectives, PubSub push when `TwitchExtension:OwnerUserId` is set; see [twitch-extension.md](twitch-extension.md) |
| Twitch extension channel settings | ✅ | ✅ | — | Broadcaster picks the event (or follows the featured one), default scope, pinned game and toggles from the extension's config/live-config views or the Broadcast tab card; requires a Soulsjwa account signed in with the channel's Twitch account; audited |
| Twitch extension admin tab | ✅ | ✅ | — | `/admin/twitch-extension`: server status, extension-wide rules (`TwitchExtensionSettings` singleton: lock channels to the featured event, hide the viewer scope switcher, defaults for unsaved channels; audited, evicts every cached extension response), Twitch console setup steps, and the extension zip download with this deployment's origin written into `extension-config.js` |
| Score history / timeline | 🟡 | 🟡 | — | CompletedAt + InGameTimeMs stored per objective; last completion shown on scoreboard; full timeline UI not yet built |
| Trial/training runs | ✅ | ✅ | ✅ | Per (event, game, competitor); owner/admin `AllowTrialRuns` switch; controls on the competitor tile and on My Events' Trial tab (enable, start/stop/reset); playable — including manual ticking — whether or not the game is the event's active one; trial completions never reach official scoring, ranking, or cross-competitor fail-rule cascades; official progress and a trial are mutually exclusive per (game, competitor) — a trial cannot start once official rows exist, and a non-recording slot refuses official writes rather than absorbing them |
| Trial progress display | ✅ | ✅ | — | Trial score/completions reported per game beside the official figures and rendered in amber on the scoreboard row/card and the OBS overlay, with a "Trial" badge naming the game when it isn't the active one; objective marks use the ordinary icons since a trial's rows are discarded when it ends. Managed on My Events' Trial tab, which is the only place on that page trial progress appears |
| Trial run destructive disable | ✅ | ✅ | — | Typed confirmation naming the game before disabling; hard-deletes exactly that competitor's trial rows for that event+game, audit-logged with the row count |

## Connector

| Feature | API | Web | Connector | Notes |
|---------|:---:|:---:|:---------:|-------|
| Configuration persistence | — | — | ✅ | JSON file under LocalAppData; the API key is DPAPI-protected for the Windows user (plain-text files from older builds still load) |
| API key masking | — | — | ✅ | The API key field is masked in the connector window; "👁 Hold to show" reveals it only while the button is held down (mouse or Space), so it stays hidden while streaming or screen sharing |
| Version checking | ✅ | — | ✅ | Connector blocks `Connect` when its build is older than the server's required version |
| SoulMemory process reading (DS1R/DS2/DS3/Sekiro/Elden Ring) | ✅ | — | ✅ | Complete upstream read-only state/catalog support at pinned commit; 2 s polling |
| Named locations, progression, and pickups | ✅ | ✅ | ✅ | Bonfires, idols, graces, endings, Great Runes, known events, and complete DS3/ER pickup catalogs are rule variables |
| Live inventory rules | ✅ | ✅ | ✅ | DS1 quantities and ER presence (upstream exposes no ER stack quantity) |
| Game state submission | ✅ | — | ✅ | Initial sync on Start; afterwards a tick POSTs only when the payload changed, plus a heartbeat at least every 30 s; the memory read runs off the UI thread |
| Event/game selection | ✅ | — | ✅ | `GET /connector/events` (events the key's owner competes in, with games); combo boxes filtered to connector-supported games |
| Multi-game support | ✅ | — | 🟡 | One event+game watched at a time |
| In-game time submission | ✅ | — | ✅ | `InGameTimeMs` stored per `CompletedObjective`; used as scoreboard tiebreaker |
| Debug mode | — | — | ✅ | Reads live process memory once and prints JSON to status panel without submitting; disabled while a session is running |
| Loaded data viewer | — | — | ✅ | Searchable/filterable/paginated browser over the loaded data points with the value each last read back, refreshed every Debug read and poll tick; Start snapshots the values and a "Changed since session start" filter hides everything that has not moved |

## Infrastructure & DevOps

| Feature | Status | Notes |
|---------|:------:|-------|
| Docker Compose deployment | ✅ | Postgres + migration job + API |
| Multi-stage Dockerfile | ✅ | Node frontend build → .NET connector publish → .NET API publish → Runtime |
| Health checks | ✅ | `/health`, `/health/live`, `/health/ready` |
| EF Core migrations | ✅ | Docker Compose `migrate` service; `--migrate`/`APPLY_MIGRATIONS=true` for other deployments |
| Game data seeding | ✅ | 5 games; ~1,000 predefined boss/location/progression/pickup objectives from the SoulMemory catalogs (`tools/generate_soulmemory_catalog.py`), seeded in one query per start |
| Security headers | ✅ | CSP, X-Frame-Options, etc. |
| Response compression | ✅ | Gzip + Brotli |
| Output caching (scoreboard) | ✅ | 1h TTL, evicted on objective changes |
| CORS configuration | ✅ | Configurable frontend URL |
| Unit tests | ✅ | Soulsjwa.UnitTests |
| API integration tests | ✅ | Testcontainers + WebApplicationFactory |
| Frontend tests | ✅ | Vitest |
| Connector tests | ✅ | Windows-only xUnit |
| CI/CD pipeline | ✅ | GitHub Actions (six jobs); Dependabot for NuGet/npm/Actions/Docker |
| Production-parity tests | ✅ | Every connector route checked on a Production host; `--migrate` run as a child process with only a connection string |

---

## Priority Gaps (Suggested Next Steps)

### High Priority — Core User Flow Gaps

1. **Invite link / code** — Optional structured invite system beyond direct self-join and Twitch-handle pre-invites
2. **Full score timeline** — Score history exists as completion timestamps, but no dedicated timeline view yet
3. **Hard delete of events** — Only archive (soft delete) exists

### Lower Priority — Polish & Advanced Features

4. **Real-time transport** — Live event pages poll periodically; WebSocket/SSE could reduce latency and network usage (the Twitch extension already gets push via Extension PubSub)
5. **Multi-instance caches** — Output cache and DataProtection key ring are single-instance today (see [deployment.md](deployment.md#scaling-considerations))
6. **Connector auto-update** — The connector refuses to run when too old but cannot update itself
