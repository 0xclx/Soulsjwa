# Authentication & Authorization

Soulsjwa uses a multi-layered authentication system combining Twitch OAuth2, JWT tokens, and API keys, paired with a granular ownership-based authorization model.

## Authentication Methods

### 1. Twitch OAuth2 (Primary)

Users authenticate via their Twitch account. The API acts as an OAuth2 client:

1. **Login initiation** — `GET /api/v1/auth/twitch/login` redirects the user to Twitch's authorization page
2. **Callback** — Twitch redirects back to `GET /api/v1/auth/twitch/callback` with an authorization code
3. **Token exchange** — The API exchanges the code for a Twitch access token
4. **User resolution** — The API fetches the user's Twitch profile and creates or updates a `User` record
5. **Refresh cookie issuance** — The API generates a refresh token and stores only its hash in the database
6. **Redirect** — The user is redirected to the frontend; the refresh token is set as an HttpOnly cookie
7. **Access token fetch** — The frontend calls `POST /api/v1/auth/refresh` with the cookie to receive the in-memory JWT access token

> **Allowlist gate:** only Twitch logins explicitly added to `AllowlistedTwitchLogins` can complete the sign-up flow. Non-allowlisted logins are redirected to `{Frontend:Url}/auth/callback?error=not_allowlisted&login=<login>`.

> **Twitch unavailable:** the HTTP client used for steps 3–4 has a timeout, retry and circuit breaker (see [system-overview.md](system-overview.md#security-architecture)). If the code exchange fails, or Twitch's user-info response can't be reached or parsed, the user is redirected to `{Frontend:Url}/auth/callback?error=twitch_unavailable` instead of seeing a bare 500.

### 2. JWT Bearer Tokens

After OAuth2 login, all authenticated API requests use JWT bearer tokens:

- **Header:** `Authorization: Bearer <token>`
- **Lifetime:** 15 minutes
- **Signing:** HMAC-SHA256
- **Claims:** `sub` (user ID), `jti`, `twitch_login`, `display_name`, `role`

### 3. API Keys

API keys provide long-lived authentication for the desktop connector and automation:

- **Header:** `X-Api-Key: sk_<32 URL-safe base64 characters>`
- **Format:** Prefixed with `sk_` followed by 32 characters from `Base64Url.EncodeToString` (BE-033) — `[A-Za-z0-9_-]`, not purely alphanumeric
- **Storage:** Only the SHA256 hash is stored in the database; the first 8 characters are stored as a prefix for efficient lookup
- **Lifetime:** Optional caller-provided `expiresAt`; keys without an expiry remain valid until revoked
- **LastUsedAt:** Throttled to update at most every 5 minutes to reduce DB write pressure
- **Cap:** At most 10 active (un-revoked, un-expired) keys per user; creating an 11th returns `409` (BE-035)

## Smart Auth Handler

A custom **"Smart" policy scheme** inspects incoming request headers and forwards to the correct handler:

| Header Present | Authentication Method |
|---------------|----------------------|
| `Authorization: Bearer <token>` | JWT Bearer handler |
| `X-Api-Key: sk_…` | `ApiKeyAuthHandler` |
| Neither | `401 Unauthorized` |

Both methods resolve to the same `ClaimsPrincipal` with a `role` claim (`User` or `Admin`). Downstream authorization code reads this via `EventOwnership.IsAdmin(principal)`, which handles both the raw `role` claim and ASP.NET's inbound mapping of `role` → `ClaimTypes.Role`.

**Source:** `Program.cs` — `AddPolicyScheme("Smart", …)`

## Token Refresh Flow

When a JWT expires, the frontend automatically refreshes it:

1. An API request returns `401 Unauthorized`
2. The Axios interceptor sends `POST /api/v1/auth/refresh` with the refresh token cookie
3. The API validates the refresh token hash against the database
4. A new JWT access token is returned
5. The original request is retried with the new token
6. If refresh fails, the user is redirected to login

### Refresh Token Details

- **Lifetime:** 30 days
- **Storage:** SHA256-hashed in database with a prefix for indexed lookup
- **Cookie:** `HttpOnly`, `Secure`, `SameSite=Strict`, path restricted to `/api/v1/auth`
- **Revocation:** Tokens can be explicitly revoked via `POST /api/v1/auth/revoke`

---

## Authorization Model

### Actors

| Actor | Description |
|-------|-------------|
| **Admin** | `User.Role == Admin`. Can do everything: create/manage every event, manage the allowlist, manage user roles, manage competitors, and complete objectives on behalf of any streamer competitor. |
| **Event owner** | The user whose `Id` matches `Event.CreatedById`. May edit the event's basic fields, archive/unarchive it, add/remove competitors, games, and objectives, and edit completion times. Event creation is admin-only, but ownership remains if roles change later. |
| **Competitor** | A user listed in `EventCompetitor`. Authenticated users may self-join via `POST /api/v1/events/{eventId:guid}/competitors/self`; owners/admins can also add competitors directly. Competitors may complete their own objectives. If `IsStreamer` is true, they may delegate moderators for that event. |
| **Streamer moderator** | A user listed in `EventCompetitorModerator` for a `(event, competitor)` pair. May mark objectives complete, set live status, and manage competitor infos on behalf of *that specific* streamer competitor for *that specific* event. |
| **Authenticated** | Any signed-in user. May use helper endpoints such as `GET /api/v1/users/search` for add-competitor/add-moderator pickers. |
| **Public** | No authentication required. |

### Key Invariants

- **Allowlist gate**: only Twitch logins in `AllowlistedTwitchLogins` can sign up.
- **Event creation is admin-only**: `POST /api/v1/events/` returns `403` for non-admins.
- **Delegation scope**: moderator delegation is per `(event, competitor)`, and only competitors marked as streamers can delegate. Removing a competitor or unmarking them as a streamer removes all their delegations for that event; delegation never carries across events.
- **Self-join**: any authenticated user can add themselves to an event as a competitor via `/api/v1/events/{eventId:guid}/competitors/self`; duplicate joins return `409`. Self-join is refused with `409` once the event has started (`Event.IsStarted`) — the owner can still add a late competitor directly via `POST /api/v1/events/{eventId:guid}/competitors` (BE-038).
- **Inviting an unknown Twitch handle is admin-only**: `POST /api/v1/events/{eventId:guid}/competitors` with a `twitchLogin` that matches no existing user creates a placeholder user *and* allowlists the handle — i.e. grants it the ability to sign in. A non-admin event owner may only invite by `twitchLogin` when the handle already belongs to an existing user, or by `userId`; inviting an unknown handle returns `403` with a message to ask an admin to allowlist it first. This prevents an owner who is later demoted from retaining the ability to grant sign-in rights through the invite flow.
- **Bootstrap admin**: see [First Admin Bootstrap](#first-admin-bootstrap) below.

## First Admin Bootstrap

The very first admin is created via configuration without any manual DB edits:

1. Set `Admin:BootstrapTwitchLogin` (env var: `Admin__BootstrapTwitchLogin`) to the Twitch login of the intended first admin **before** any admins exist.
2. That user signs in via Twitch.
3. `TwitchAuthService.UpsertUserAsync` detects no admin exists and, in one transaction, creates the user as `Admin` with `IsAllowlisted = true` (no `AllowlistedTwitchLogins` row is written; the flag on the user is what the allowlist gate checks). The unique index on `TwitchId` is what makes two simultaneous first sign-ins produce one admin.
4. Once any admin exists, the bootstrap setting has no further effect and can be removed.

This avoids ever shipping a default password or god-mode account.

## Endpoint Permission Matrix

### Events (`/api/v1/events`)

| Method | Route | Access |
|--------|-------|--------|
| `GET` | `/` | Public (archived rows restricted — see [Soft Delete & Query Filters](#soft-delete--query-filters)) |
| `GET` | `/{id}` | Public for a non-archived event; an archived one is visible only to its members (owner, competitors, delegated moderators, admins) — see below |
| `POST` | `/` | **Admin** |
| `PATCH` | `/{id}` | Owner or Admin |
| `POST` | `/{id}/archive` | Owner or Admin |
| `POST` | `/{id}/unarchive` | Owner or Admin |
| `POST` | `/{id}/start` | Owner or Admin |
| `POST` | `/{id}/stop` | Owner or Admin |
| `GET` | `/{eventId:guid}/scores` | Public |
| `GET` | `/{eventId:guid}/scoreboard` | Public |
| `POST` | `/{eventId:guid}/live` | Self competitor, Admin, or delegated moderator for `?onBehalfOfUserId=` |
| `GET` | `/{eventId:guid}/overlay-scoreboard` | Valid overlay token query parameter |

### Competitors (`/api/v1/events/{eventId}/competitors`)

| Method | Route | Access |
|--------|-------|--------|
| `POST` | `/` | Owner or Admin — inviting by `twitchLogin` for a handle with no existing user is Admin-only (see Key Invariants) |
| `POST` | `/self` | Authenticated user joins themselves |
| `PATCH` | `/{userId}` | Owner or Admin |
| `DELETE` | `/{userId}` | Owner or Admin |

### Competitor Moderators (`/api/v1/events/{eventId}/competitors/{competitorUserId}/moderators`)

| Method | Route | Access |
|--------|-------|--------|
| `GET` | `/` | Public |
| `POST` | `/` | That competitor or Admin |
| `DELETE` | `/{moderatorUserId}` | That competitor or Admin |

The moderator user must already be allowlisted and signed up (returns `409` otherwise).

### Users (`/api/v1/users`)

| Method | Route | Access |
|--------|-------|--------|
| `GET` | `/me` | Authenticated |
| `GET` | `/me/api-keys` | Authenticated |
| `POST` | `/me/api-keys` | Authenticated |
| `DELETE` | `/me/api-keys/{id}` | Authenticated key owner |
| `GET` | `/search` | Authenticated |

### Competitors, Event Games, Objectives

Owner or Admin only, except competitor self-join and public list/read endpoints. (Admins inherit owner privileges via `EventOwnership.RequireOwner`.)

### Completed Objectives (`/api/v1/events/{eventId}/games/{gameId}/objectives/{objectiveId}`)

| Method | Route | Access |
|--------|-------|--------|
| `POST` | `/complete` | Self (competitor) **or** Admin **or** a moderator delegated by the competitor named in `?onBehalfOfUserId=` |
| `DELETE` | `/complete` | Same rules as POST |
| `PATCH` | `/completions/{userId}` | Owner or Admin only; requires `{ completedAt, reason }` |

The on-behalf-of target must be a competitor marked as a streamer in the event; otherwise the request returns `403`. Completion-time edits are deliberately restricted to admins/event owners and always require an audit reason.

### Competitor Infos (`/api/v1/events/{eventId}/games/{eventGameId}/competitors/{userId}/infos`)

| Method | Route | Access |
|--------|-------|--------|
| `GET` | `/` | Public |
| `POST` | `/` | Admin, event owner, that competitor, or delegated moderator |
| `PATCH` | `/{infoId}` | Same as POST |
| `DELETE` | `/{infoId}` | Same as POST |

### Overlay Tokens (`/api/v1/events/{eventId}/overlay-tokens`)

| Method | Route | Access |
|--------|-------|--------|
| `GET` | `/` | Event owner/admin, or event competitor for their own tokens |
| `POST` | `/` | Event owner/admin or event competitor |
| `DELETE` | `/{tokenId}` | Event owner/admin, or token creator |

### Audits

| Method | Route | Access |
|--------|-------|--------|
| `GET` | `/api/v1/events/{eventId}/audits` | Admin, event owner, competitor, or delegated moderator in the event |
| `GET` | `/api/v1/admin/audits` | Admin |

### Admin (`/api/v1/admin`)

| Method | Route | Access |
|--------|-------|--------|
| `GET` | `/allowlist/` | Admin |
| `POST` | `/allowlist/` | Admin |
| `DELETE` | `/allowlist/{id}` | Admin (revokes the linked user's allowlist flag and refresh tokens) |
| `GET` | `/users/` | Admin |
| `PATCH` | `/users/{id}/role` | Admin (last-admin demotion blocked with `409`) |

## `EventOwnership` Helper

Located at `Features/Events/EventOwnership.cs`. Provides:

- `GetUserId(principal)` — reads `ClaimTypes.NameIdentifier`, throwing `FormatException` if it's missing or not a GUID (BE-039: should never happen behind `RequireAuthorization()` — the JWT scheme's `OnTokenValidated` hook already 401s a malformed subject, and the API-key scheme always sources the claim from a `Guid`-typed column).
- `TryGetUserId(principal, out userId)` — same extraction without throwing, for call sites that haven't already gone through one of those two schemes.
- `IsAdmin(principal)` — checks both `role` and `ClaimTypes.Role` claims.
- `RequireOwner(ev, principal, action)` — returns `null` if the caller is admin or the event creator, otherwise a 403 `ProblemDetails`.
- `RequireCanCompleteForStreamerAsync(ev, principal, targetStreamerId, db, ct)` — used by the on-behalf-of flow.

## Soft Delete & Query Filters

Archived events (`IsArchived = true`) are excluded globally by an EF Core query filter:

```csharp
modelBuilder.Entity<Event>().HasQueryFilter(x => !x.IsArchived);
```

Use `.IgnoreQueryFilters()` to access archived events.

> **Archived-event read visibility (BE-018):** the global filter only governs
> the *default* list — the public read endpoints bypass it deliberately, and
> narrow visibility themselves rather than being genuinely public.
> `GET /events/{identifier}` bypasses the filter and then, if the row is
> archived, requires `EventOwnership.IsEventMemberAsync` (admin, the owner, a
> competitor, or a delegated moderator); anyone else gets the same 404 a
> nonexistent id would produce, never a 403 that would confirm the event
> exists. `GET /events` bypasses the filter only when `includeArchived=true`
> or `status=archived` is requested: an anonymous caller's request for
> archived rows is silently ignored (falling back to the normal, filtered
> query), an authenticated non-admin sees archived rows only for events they
> own, compete in, or moderate, and an admin sees every archived row.
> `POST /events/{id}/unarchive` and the event-duplication endpoint have their
> own, separate `IgnoreQueryFilters()` calls (owner/admin-only already) and
> are unaffected.

## Rate Limiting

| Limiter | Limit | Window | Scope | Applied To |
|---------|-------|--------|-------|------------|
| **Global (authenticated)** | 100 requests | 1 minute | Per user ID | All authenticated endpoints (default) |
| **Global (anonymous)** | 60 requests | 1 minute | Per IP address | All anonymous endpoints (default) |
| `auth` | 20 requests | 1 minute | Per IP address | Auth endpoints only |
| `connector` | 120 requests | 1 minute | Per user ID | Connector submission endpoint |

The global limiter acts as a fallback; named policies override it for specific endpoint groups. The connector policy is intentionally more permissive (with a small queue) to avoid blocking frequent game-state submissions.

## Security Configuration

| Setting | Location | Description |
|---------|----------|-------------|
| `Jwt:Secret` | Environment / appsettings | HMAC-SHA256 signing key (required, min 32 chars) |
| `Jwt:Issuer` | appsettings.json | Token issuer identifier |
| `Jwt:Audience` | appsettings.json | Token audience identifier |
| `Jwt:AccessTokenMinutes` | appsettings.json | Access token lifetime (default: 15) |
| `Jwt:RefreshTokenDays` | appsettings.json | Refresh token lifetime (default: 30) |
| `Twitch:ClientId` | Environment / appsettings | Twitch app client ID |
| `Twitch:ClientSecret` | Environment / appsettings | Twitch app client secret |
| `Twitch:RedirectUri` | Environment / appsettings | OAuth2 callback URL |

## Security Hardening

The API applies the following measures:

- **CORS:** Only the configured frontend URL is allowed
- **Rate Limiting:** As described in the table above
- **Response Compression:** Gzip and Brotli compression enabled for HTTPS responses
- **Output Caching:** Score + scoreboard endpoints cached for 1 hour (`Scoreboard` tag, evicted on objective/live-status/info changes); overlay scoreboard cached for 5 seconds per token
- **Security Headers:** `X-Content-Type-Options: nosniff`, `X-Frame-Options: SAMEORIGIN` (same-origin framing only, for the overlay designer's preview), `Referrer-Policy: strict-origin-when-cross-origin`, `Permissions-Policy`
- **CSP:** `Content-Security-Policy` restricts script/style/connect sources to `'self'`
- **HSTS:** Enabled in production
- **Swagger:** Only available in development mode
- **OAuth state:** Short-lived (10 min) DataProtection-protected `oauth_state` cookie — no server-side session

## Key Design Decisions

1. **Two-role system (`User`/`Admin`)** — Most users do not need elevated privileges. Admin is a single bit that flips every "owner check" to a pass.
2. **Allowlist by Twitch login (not user id)** — Admins can pre-approve a future participant before they've ever signed in.
3. **Per-streamer per-event moderator delegation** — Avoids a single bag of "trusted users" that bleeds across events or streamers; each streamer controls their own.
4. **Bootstrap by config, not DB seed** — Server installers set `Admin:BootstrapTwitchLogin`; the first matching sign-in becomes admin. After that the setting is inert.
5. **De-allowlisting revokes refresh tokens** — Otherwise a kicked user could keep using their access token chain until expiry.
