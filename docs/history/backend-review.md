# Backend Review TODO

> **Status: completed.** Every item below was implemented (PRs #200, #203 and
> follow-ups; commit subjects carry the `BE-nnn` ids). This file is kept as
> the record of *why* those changes were made, because docs and code comments
> cite the ids. It is not a backlog. See [README.md](README.md).


> Implementation backlog for `src/Soulsjwa.Api` (ASP.NET minimal API, net10.0, EF Core 10 +
> Npgsql, PostgreSQL). Work the tasks in the order they appear. Every task is
> independently verifiable; `Depends on:` is called out where it is not.
>
> Conventions from [`AGENTS.md`](../../AGENTS.md) apply to every task: smallest complete
> change, no magic strings, update the docs listed in the §6 sync table in the same
> commit, run `dotnet build Soulsjwa.slnx`, `dotnet test Soulsjwa.slnx` and
> `dotnet format Soulsjwa.slnx --verify-no-changes` before pushing.

---

## Summary

### Overall assessment

This is a well-built codebase. The architecture is appropriate for its size and
problem: vertical feature folders under `Features/`, minimal-API endpoints that take
`AppDbContext` directly, no repository layer, no MediatR, no speculative
abstractions. Authorization is centralised in `EventOwnership` and applied
consistently — a scripted sweep of every write endpoint found **zero** missing
`RequireAuthorization()`. Several invariants are already enforced in the database
rather than in application code (partial unique indexes on active game, on official
vs. trial completions, the `UrlAlias` lowercase check constraint), refresh-token
reuse detection follows the OAuth Security BCP, and `ImageValidator` parses image
headers with spans instead of handing attacker bytes to a decoder. That is a
better starting point than most.

The problems are concentrated in three places: **rate limiting and credential
lifecycle**, **the "is the event running?" gate**, and **read-path scalability**. Two
findings are genuine production-breaking issues rather than hygiene. There is no
structural rewrite recommended anywhere in this document.

### 5-axis scores

| Axis | Score | Note |
|---|---|---|
| Architecture & Domain Design | 8 / 10 | Right-sized. No over-abstraction to remove. A handful of endpoint files have grown long and repeat the same load-event/authorize/validate preamble. |
| Correctness, Data Integrity & Concurrency | 6 / 10 | Good use of advisory locks and partial unique indexes, but the event-state gate is silently skippable, refresh-token rotation is not atomic, no concurrency tokens, and `DateTime` kinds are unnormalised at the boundary. |
| Security & API Safety | 6 / 10 | No IDOR found, no SQLi, no mass assignment, uploads are sound. Lost points on credential lifecycle (API keys outlive de-allowlisting), a rate-limit control that does not do what its comment claims, and secrets in URLs. |
| Performance, Scalability & Reliability | 5 / 10 | Two unbounded anonymous read endpoints, a dashboard that materialises full scoreboards, a cache tag that evicts globally, and no timeouts on the one external dependency. |
| Maintainability, Testing & Simplification | 8 / 10 | 61 test files across 4 projects, real integration tests, heavily commented intent. Two dead NuGet packages; error-response shape is inconsistent. |

### Task counts

| Priority | Count |
|---|---|
| P0 | 2 |
| P1 | 9 |
| P2 | 17 |
| P3 | 11 |
| **Total** | **39** |

### The four headline risks

- **Biggest security risk — BE-002.** An API key keeps working forever after its owner
  is removed from the allowlist. `ApiKeyAuthHandler` validates the key row but never
  re-reads `User.IsAllowlisted`, and `AllowlistEndpoint.Remove` revokes refresh tokens
  but not API keys. Deprovisioning a user does not actually deprovision them. Because
  API keys are long-lived credentials that live on competitors' desktop machines (the
  WPF connector), and because a key carries the owner's full rights including admin,
  this is the one finding that fails an audit.

- **Biggest correctness / data-integrity risk — BE-003.** Five sites use the pattern
  `if (ev is not null && !ev.IsStarted) return 403;`. `db.Events` carries a global
  query filter on `IsArchived`, so for an archived event the lookup returns `null` and
  the guard evaluates to `false` — the check is skipped rather than failed. Objective
  completions and connector submissions are accepted against archived events.

- **Biggest scalability risk — BE-006 and BE-007.** `GET /api/v1/calendar` is
  anonymous, unpaginated, has no date-range filter, no cache, and `Include`s the event
  graph — it materialises every calendar entry (each up to 64 KiB of markdown) and
  every planned run in the database on each call. `GET /api/v1/me/events` builds a
  complete scoreboard — the full competitor × game × objective matrix — for every event
  the caller is involved in, just to read a handful of aggregates off the top. These
  two are what fall over first at 10x.

- **Biggest simplification opportunity — BE-028 + BE-029 + the cross-cutting endpoint
  preamble (XC-1).** About 30 endpoint handlers open with the same four steps: load the
  event, null-check to 404, `RequireOwner`, load the event-game, null-check to 404.
  Collapsing that into one helper removes roughly 200 lines and — more importantly —
  makes BE-003 structurally impossible to reintroduce. Two NuGet packages
  (`BCrypt.Net-Next`, `System.IdentityModel.Tokens.Jwt`) are referenced and never used.

### A note on what was verified and what was not

The `dotnet` SDK is not installed in the review environment, so nothing here was
compiled or executed. Findings were derived by reading the code end to end. Three
tasks (**BE-001**, **BE-005**, **BE-013**) rest on framework behaviour that the
implementing agent must confirm with the regression test named in the task *before*
changing code — the test is written first, must fail, and then the fix makes it pass.
Each of those tasks says so explicitly. Do not skip that step; if the test passes
before the fix, close the task as invalid and say so.

---

## Scorecard

| # | ID | Priority | Axis | Area | One-line |
|---|---|---|---|---|---|
| 1 | BE-001 | P0 | Security | Rate limiting | `"auth"` policy is one shared bucket — 20 req/min locks out every user |
| 2 | BE-002 | P0 | Security | Credential lifecycle | API keys survive de-allowlisting and demotion |
| 3 | BE-003 | P1 | Correctness | Event state gate | `ev is not null && !ev.IsStarted` skips the check for archived events |
| 4 | BE-004 | P1 | Correctness | Auth | Refresh-token rotation is not atomic |
| 5 | BE-005 | P1 | Security | Overlay tokens | Credential in the query string reaches logs |
| 6 | BE-006 | P1 | Performance | Global calendar | Unbounded anonymous full-table read |
| 7 | BE-007 | P1 | Performance | My events | Full scoreboards built to read aggregates |
| 8 | BE-008 | P1 | Correctness | Objectives | Unvalidated strings written to `jsonb` → 500 |
| 9 | BE-009 | P1 | Performance | Caching | One global scoreboard cache tag |
| 10 | BE-010 | P1 | Correctness | Events | Concurrent `feature` can leave two featured events |
| 11 | BE-011 | P1 | Security | Connector | No request-body size limit |
| 12 | BE-012 | P2 | Correctness | Event games | Enable/disable ordering vs. partial unique index |
| 13 | BE-013 | P2 | Correctness | Date handling | `DateTimeKind` unnormalised at the API boundary |
| 14 | BE-014 | P2 | Correctness | Concurrency | No optimistic concurrency token |
| 15 | BE-015 | P2 | Reliability | Retention | `RefreshTokens` / `AuditLogs` grow without bound |
| 16 | BE-016 | P2 | Performance | Connector | Submission JSON re-parsed per objective |
| 17 | BE-017 | P2 | Reliability | Twitch | No timeout, retry or circuit breaker |
| 18 | BE-018 | P2 | Security | Events | Archived events remain publicly readable |
| 19 | BE-019 | P2 | Performance | Events list | Unindexed `LIKE` + full graph per page |
| 20 | BE-020 | P2 | Maintainability | Routing | SPA fallback swallows unmatched `/api/*` |
| 21 | BE-021 | P2 | Security | Middleware | Unvalidated `X-Correlation-Id` echoed and logged |
| 22 | BE-022 | P2 | Security | Config | No minimum length on `Jwt:Secret` |
| 23 | BE-023 | P2 | Performance | Media | Whole files buffered; no conditional response |
| 24 | BE-024 | P2 | Correctness | Competitors | Placeholder user created outside the transaction |
| 25 | BE-025 | P2 | Reliability | Rate limiting | Global 100/min caps the 120/min connector policy |
| 26 | BE-026 | P2 | Performance | Audits | OFFSET pagination + COUNT on a growing table |
| 27 | BE-027 | P2 | Performance | Fail rules | `SaveChanges` inside a loop |
| 28 | BE-028 | P2 | Maintainability | Errors | Inconsistent error-response shape |
| 29 | BE-029 | P3 | Maintainability | Dependencies | Two unused NuGet packages |
| 30 | BE-030 | P3 | Performance | Objectives | `ListPredefined` unpaginated and anonymous |
| 31 | BE-031 | P3 | Performance | Users | Non-sargable `TwitchLogin.ToLower()` in 5 places |
| 32 | BE-032 | P3 | Maintainability | Objectives | Delete logs `ObjectiveUpdated` |
| 33 | BE-033 | P3 | Maintainability | Tokens | Hand-rolled base64 substitution |
| 34 | BE-034 | P3 | Correctness | Duplication | Copy carries `IsEnabled` into a stopped event |
| 35 | BE-035 | P3 | Reliability | API keys | No cap on keys per user |
| 36 | BE-036 | P3 | Maintainability | Audits | Full 64 KiB documents stored twice per edit |
| 37 | BE-037 | P3 | Security | Config | `AllowedHosts: "*"` |
| 38 | BE-038 | P3 | Correctness | Competitors | Self-join into an already-started event |
| 39 | BE-039 | P3 | Maintainability | Auth | `GetUserId` throws → 500 instead of 401 |

---

## P0 — Critical

### [BE-001] Replace the `"auth"` rate-limit policy with a per-IP partitioned limiter

- **Priority:** P0
- **Axis:** Security
- **Location:** `src/Soulsjwa.Api/Program.cs` — the `options.AddFixedWindowLimiter("auth", …)` block (rate-limiting configuration)
- **Problem:** The policy is declared as

  ```csharp
  // Auth endpoints: strict limit to prevent brute-force (20 req/min per IP).
  options.AddFixedWindowLimiter("auth", limiter =>
  {
      limiter.PermitLimit = 20;
      limiter.Window = TimeSpan.FromMinutes(1);
      limiter.QueueLimit = 0;
  });
  ```

  `RateLimiterOptions.AddFixedWindowLimiter(policyName, configure)` creates **one
  unpartitioned limiter shared by every request that uses the policy** — unlike
  `AddPolicy(name, ctx => RateLimitPartition.GetFixedWindowLimiter(key, …))`, which is
  what the neighbouring `"connector"` policy correctly uses to partition per user. The
  comment says "per IP"; the code is "per process". The policy is attached to
  `/api/v1/auth/twitch/login`, `/api/v1/auth/twitch/callback`, `/api/v1/auth/refresh`
  and `/api/v1/auth/revoke`.
- **Failure/Attack scenario:** Access tokens live 15 minutes, so every active browser
  tab calls `POST /api/v1/auth/refresh` roughly four times an hour; a page reload calls
  it immediately. With 20 permits per minute across the whole process, ~20 concurrent
  users are enough to start 429-ing each other during a normal event evening. And an
  attacker needs no account at all: an unauthenticated loop issuing 20
  `POST /api/v1/auth/revoke` requests per minute from a single host consumes the entire
  bucket, so **every** user's refresh and every new Twitch login fails for as long as
  the loop runs. Because the global fallback limiter partitions anonymous traffic by IP
  at 60/min, the attacker stays comfortably inside it while denying login to everyone.
- **Why:** This is a security control that appears to exist and does not. It converts a
  brute-force defence into a one-host, zero-cost, total-login denial of service against
  the whole deployment, and it degrades the service under ordinary load.
- **Change:**
  1. **Write the regression test first and watch it fail.** In
     `tests/Soulsjwa.ApiTests/` add `AuthRateLimitPolicyTests` that issues 21
     `POST /api/v1/auth/revoke` requests with `X-Forwarded-For: 203.0.113.1`, then one
     request with `X-Forwarded-For: 203.0.113.2`, and asserts the second IP's request is
     **not** 429. Against today's code that must fail. If it passes, this finding is
     wrong for the framework version in use — stop, close the task as invalid, and say
     so in the PR description rather than changing `Program.cs`.
  2. Replace the `AddFixedWindowLimiter("auth", …)` call with an `AddPolicy` that
     partitions on `ctx.Connection.RemoteIpAddress` (already the post-proxy client IP:
     `UseForwardedHeaders` runs first with `ForwardLimit = 1`), mirroring the shape of
     the existing `"connector"` policy:

     ```csharp
     options.AddPolicy(RateLimitPolicies.Auth, ctx =>
     {
         var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? UnknownPartition;
         return RateLimitPartition.GetFixedWindowLimiter(
             $"auth_{ip}",
             _ => new FixedWindowRateLimiterOptions
             {
                 PermitLimit = AuthPermitLimit,
                 Window = TimeSpan.FromMinutes(1),
                 QueueLimit = 0,
             });
     });
     ```
  3. Per `AGENTS.md` §4, introduce a `RateLimitPolicies` static class holding
     `Auth = "auth"` and `Connector = "connector"`, plus named constants for the
     permit limits and the `"unknown"` / `"anon"` partition fallbacks. Replace the
     string literals at every `RequireRateLimiting(...)` call site.
  4. Fix the comment so it describes what the code does.
- **Acceptance criteria:**
  - 21 auth requests from one IP: the 21st is 429.
  - A request from a second IP immediately afterwards is not 429.
  - No string literal `"auth"` or `"connector"` remains at any `RequireRateLimiting` call site.
  - The per-IP partition key is derived from `RemoteIpAddress`, i.e. it honours `X-Forwarded-For` only through the already-configured `ForwardLimit = 1` (a client-supplied `X-Forwarded-For` with two entries must not let one host occupy two buckets).
- **Validation:** The new `AuthRateLimitPolicyTests` (both assertions above, plus a
  third asserting a forged two-hop `X-Forwarded-For` still lands in the same bucket as
  the immediate peer). Full `dotnet test Soulsjwa.slnx`.

---

### [BE-002] Re-validate the user on every API-key authentication, and revoke API keys when a user is de-allowlisted

- **Priority:** P0
- **Axis:** Security
- **Location:** `src/Soulsjwa.Api/Infrastructure/Auth/ApiKeyAuthHandler.cs:ApiKeyAuthHandler.HandleAuthenticateAsync`; `src/Soulsjwa.Api/Features/Admin/Endpoints/AllowlistEndpoint.cs:AllowlistEndpoint.Remove`
- **Problem:** Two gaps that compound:
  1. `HandleAuthenticateAsync` loads `ApiKeys.Include(k => k.User)` and checks
     `!k.IsRevoked` and `ExpiresAt`. It never checks `storedKey.User.IsAllowlisted`. It
     then mints claims — including `new Claim("role", storedKey.User.Role.ToString())`
     — for that user.
  2. `AllowlistEndpoint.Remove` sets `linked.IsAllowlisted = false` and revokes every
     row in `RefreshTokens`. It does not touch `ApiKeys`. The code comment explicitly
     claims the goal is that "any active refresh token chain can't outlive the
     de-allowlisting" — API keys were simply missed.

  `IsAllowlisted` is consulted in exactly one place in the whole codebase —
  `TwitchAuthService.UpsertUserAsync`, during OAuth login. Nothing on the request path
  consults it.
- **Attack vector:** Offline use of a retained long-lived credential after
  deprovisioning.
- **Required attacker capabilities:** Possession of one `sk_…` API key that was valid
  at any point in the past. These keys are created by `POST /api/v1/users/me/api-keys`
  specifically so the WPF connector can run on a competitor's own desktop, so the key
  is sitting in a config file on a machine the operator does not control. No network
  position, no interaction with the victim, no admin access needed.
- **Affected resource:** The full API surface as that user. If the user was an admin at
  any point, that includes `POST /api/v1/events`, `PATCH /api/v1/admin/users/{id}/role`
  (re-promote themselves or promote a confederate),
  `POST /api/v1/admin/allowlist` (re-allowlist themselves), `PUT /api/v1/theme`,
  `PUT /api/v1/legal/{kind}` and `POST /api/v1/uploads`.
- **Current vulnerable path:**
  `X-Api-Key: sk_…` → `Program.cs` `"Smart"` policy scheme forwards to
  `ApiKeyAuthHandler` (the header is present) → key row found, `IsRevoked` false,
  `ExpiresAt` null → claims minted from `storedKey.User` → `EventOwnership.IsAdmin`
  reads the raw `"role"` claim → admin action succeeds. An admin's removal of the user
  from the allowlist, and any demotion via `AdminSetUserRole`, has no effect on this
  path — the `role` claim is read fresh from the DB on each request so demotion *is*
  picked up, but allowlist removal is not, and revocation of the key never happens.
- **Concrete remediation:**
  1. In `HandleAuthenticateAsync`, after the expiry check, add:

     ```csharp
     if (!storedKey.User.IsAllowlisted)
         return AuthenticateResult.Fail("API key owner is no longer permitted to sign in.");
     ```

     Return `Fail`, not `NoResult`, so the request is rejected rather than falling
     through to anonymous. Keep the failure message identical in shape to the existing
     `"Invalid API key"` responses so a caller cannot distinguish "revoked key" from
     "de-allowlisted owner" from "wrong key" — it must not become an oracle for whether
     a given key ever existed.
  2. In `AllowlistEndpoint.Remove`, alongside the existing refresh-token revocation,
     revoke the linked user's API keys in the same `SaveChangesAsync`:

     ```csharp
     var keys = await db.ApiKeys
         .Where(k => k.UserId == linked.Id && !k.IsRevoked)
         .ToListAsync(ct);
     foreach (var k in keys) k.IsRevoked = true;
     ```

     Include the revoked key count in the existing `audit.Log(..., before: …)` payload
     so the audit trail records what access was withdrawn.
  3. Extend the same `IsAllowlisted` gate to JWT-authenticated requests. Access tokens
     live 15 minutes, so a de-allowlisted user retains up to 15 minutes of access via a
     bearer token even after this change. Add a `JwtBearerEvents.OnTokenValidated`
     handler that resolves `AppDbContext` from
     `context.HttpContext.RequestServices` and calls `context.Fail(...)` when the `sub`
     user is missing or `IsAllowlisted == false`. Note in a comment that this adds one
     indexed primary-key lookup per authenticated request — that is the accepted cost of
     making deprovisioning immediate, and it is the same lookup the API-key path already
     performs.
- **Acceptance criteria:**
  - A request carrying a valid `X-Api-Key` whose owner has `IsAllowlisted = false` returns 401, not 200.
  - `DELETE /api/v1/admin/allowlist/{id}` sets `IsRevoked = true` on every previously-active `ApiKey` row for the linked user, in the same transaction as the `IsAllowlisted = false` write.
  - The emitted `AllowlistRemoved` audit row records the number of API keys revoked.
  - A bearer token minted before de-allowlisting returns 401 on the next request rather than working until expiry.
  - 401 responses for "revoked key", "expired key", "de-allowlisted owner" and "unknown key" are byte-identical.
- **Validation:** Extend `tests/Soulsjwa.ApiTests/ApiKeyAuthHandlerAuthenticationTests.cs`
  with a de-allowlisted-owner case; extend
  `tests/Soulsjwa.UnitTests/ApiKeyAuthHandlerTests.cs` for the handler-level `Fail`.
  Add an integration test in `tests/Soulsjwa.IntegrationTests/` that creates a user +
  API key + allowlist entry, calls `DELETE /api/v1/admin/allowlist/{id}`, and asserts
  both that the key row is revoked and that a subsequent `X-Api-Key` request is 401.
  Add a JWT case asserting a pre-existing bearer token is rejected after de-allowlisting.

---

## P1 — High Priority

### [BE-003] Fix the event-state gate that silently passes for archived events

- **Priority:** P1
- **Axis:** Correctness
- **Location:** `src/Soulsjwa.Api/Features/Events/Endpoints/CompletedObjectivesEndpoint.cs:110` and `:218`; `src/Soulsjwa.Api/Features/Events/Endpoints/FailedObjectivesEndpoint.cs:83` and `:178`; `src/Soulsjwa.Api/Features/Connector/Endpoints/ConnectorEndpoint.cs:111`
- **Problem:** All five sites are variations of:

  ```csharp
  var ev2 = await db.Events.FirstOrDefaultAsync(e => e.Id == eventId, ct);
  if (ev2 is not null && !ev2.IsStarted)
      return Results.Problem(detail: "Event is not started.", statusCode: 403);
  ```

  `Event` carries `e.HasQueryFilter(x => !x.IsArchived)` in `AppDbContext`. For an
  archived event the query returns `null`, the left operand of `&&` is `false`, and the
  guard is **skipped entirely**. The null-safe operator that was presumably added to
  avoid a `NullReferenceException` converted a hard gate into a no-op for exactly the
  rows that should be most locked down.

  The surrounding lookups do not save it: `EventGame`, `Objective`, `EventCompetitor`
  and `CompletedObjective` have no query filter of their own, so
  `db.EventGames.FirstOrDefaultAsync(eg => eg.EventId == eventId && …)` happily returns
  the archived event's game.
- **Failure scenario:** An owner archives a finished tournament. A competitor (or their
  delegated moderator, or their still-running connector) posts
  `POST /api/v1/events/{archivedId}/games/{gameId}/objectives/{objId}/complete`. The
  event lookup returns null, the "not started" gate is skipped, `eventGame.IsEnabled` is
  still `true` from before the archive, the competitor row still exists — the completion
  is written, `FailRuleCascadeEvaluator` may mark other competitors failed, and the
  scoreboard cache for every event is evicted. Archived history is mutated after the
  fact, and the audit log will show completions timestamped weeks after the event ended.
- **Why:** Archiving is the product's "this is over and immutable" operation. Right now
  it stops the event appearing in lists while leaving it fully writable. It is also a
  latent 500: any future refactor that drops the `is not null` guard turns these into
  `NullReferenceException`s.
- **Change:**
  1. Introduce one helper — put it next to the other shared event helpers, in
     `src/Soulsjwa.Api/Features/Events/EventStateGate.cs`:

     ```csharp
     /// Loads the event for a write that requires a live, running event.
     /// Returns the event, or the IResult to return directly.
     /// Archived events resolve to 404 (they are soft-deleted, so they must
     /// look absent to every write path), not-started events to 403.
     public static async Task<(Event? Event, IResult? Error)> RequireRunningEventAsync(
         Guid eventId, AppDbContext db, CancellationToken ct)
     ```

     It must query `db.Events` **without** `IgnoreQueryFilters()`, return
     `Results.Problem(detail: "Event not found.", statusCode: 404)` when the row is
     `null`, and `Results.Problem(detail: "Event is not started.", statusCode: 403)`
     when `!IsStarted`.
  2. Replace all five sites with it. Each of those five handlers currently loads the
     event twice in some paths (once as `ev` for the on-behalf-of authorization branch,
     once as `ev2` for the state check) — collapse to a single load and pass the loaded
     entity into `EventOwnership.RequireCanCompleteForStreamerAsync`. This removes a
     redundant round trip per request as a side effect.
  3. Grep for `is not null &&` across `src/Soulsjwa.Api` afterwards and confirm no
     remaining occurrence guards an authorization or state check.
- **Acceptance criteria:**
  - `POST …/complete`, `DELETE …/complete`, the two `FailedObjectives` equivalents, and `POST /api/v1/connector/events/{id}/games/{id}/submit` all return 404 for an archived event.
  - The same five return 403 `"Event is not started."` for an existing, non-archived, not-started event (unchanged behaviour).
  - No handler among the five loads the `Event` row more than once.
  - `grep -rn "is not null && !" src/Soulsjwa.Api --include=*.cs` returns nothing.
- **Validation:** New test class `tests/Soulsjwa.ApiTests/ArchivedEventWriteGateTests.cs`
  with one case per endpoint: archive the event, attempt the write, assert 404 and
  assert no `CompletedObjective` / `FailedObjective` row was created. Existing
  `CompletedObjectivesEndpointTests` and `ConnectorEndpointTests` must still pass
  unchanged.

---

### [BE-004] Make refresh-token rotation atomic

- **Priority:** P1
- **Axis:** Correctness
- **Location:** `src/Soulsjwa.Api/Features/Auth/Endpoints/RefreshTokenEndpoint.cs:Handle`; `src/Soulsjwa.Api/Infrastructure/Auth/JwtTokenService.cs:ValidateRefreshTokenAsync` / `RevokeRefreshTokenAsync` / `GenerateRefreshTokenAsync`
- **Problem:** Rotation is three sequential operations with three separate
  `SaveChangesAsync` calls and no enclosing transaction:

  ```csharp
  var (user, oldRefreshToken) = await jwtService.ValidateRefreshTokenAsync(refreshTokenValue, ct);
  if (user is null || oldRefreshToken is null) return Results.Unauthorized();
  await jwtService.RevokeRefreshTokenAsync(refreshTokenValue, ct);      // SaveChanges #1
  var newAccessToken = jwtService.GenerateAccessToken(user);
  var (rawToken, newRefreshToken) = await jwtService.GenerateRefreshTokenAsync(user.Id, ct); // SaveChanges #2
  ```

  There is no read-modify-write guard on the revoke: `RevokeRefreshTokenAsync`
  re-queries by prefix + hash, sets `IsRevoked = true`, and saves regardless of the
  row's current state.
- **Failure scenarios:**
  - *Two requests at once.* A browser with two tabs, or a client retrying, sends the
    same cookie twice. Both `ValidateRefreshTokenAsync` calls run before either
    `RevokeRefreshTokenAsync` commits, so both see `IsActive == true`. Both revoke (the
    second is a no-op write over an already-revoked row) and both mint a fresh token.
    Two independent, simultaneously-valid refresh chains now exist for one login. The
    reuse-detection logic in `ValidateRefreshTokenAsync` — the whole point of which is
    to catch exactly this shape — never fires, because neither request replays a token
    that was already revoked *at the time it was read*.
  - *Crash halfway.* The process dies between `SaveChanges #1` and `#2`. The old token is
    revoked, no new token was issued, the cookie still holds the old value. The user's
    next refresh presents a revoked-but-unexpired token, which trips reuse detection and
    calls `RevokeAllForUserAsync` — a full forced logout caused by the server's own crash.
  - *Response lost.* `#2` commits, then the network drops the response. The client keeps
    the old cookie. Same outcome as above: the next refresh looks like theft and logs the
    user out everywhere.
- **Why:** The reuse-detection mitigation is load-bearing security code and it has a
  hole precisely in the concurrent case it was written for. The crash and lost-response
  paths turn a transient failure into a forced re-authentication, which trains users and
  operators to treat "logged out for no reason" as normal — the exact signal that should
  mean "your token was stolen".
- **Change:**
  1. Wrap the whole handler in
     `await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);`
     and commit once at the end. Follow the pattern already used in
     `ConnectorEndpoint.SubmitGameData`. Guard with `db.Database.IsRelational()` exactly
     as `TwitchAuthService.UpsertUserAsync` does, so the InMemory provider used by unit
     tests still works.
  2. Make the revoke a **conditional** update and treat "zero rows affected" as "someone
     else won the race". Add to `JwtTokenService`:

     ```csharp
     /// Revokes the token only if it is currently un-revoked. Returns false when
     /// another concurrent rotation already claimed it, in which case the caller
     /// must abort with 401 rather than issuing a second token chain.
     public async Task<bool> TryRevokeForRotationAsync(Guid tokenId, CancellationToken ct)
     {
         var affected = await dbContext.RefreshTokens
             .Where(t => t.Id == tokenId && !t.IsRevoked)
             .ExecuteUpdateAsync(s => s
                 .SetProperty(t => t.IsRevoked, true)
                 .SetProperty(t => t.RevokedAt, DateTime.UtcNow), ct);
         return affected == 1;
     }
     ```

     `ExecuteUpdateAsync` issues a single `UPDATE … WHERE "Id" = @id AND NOT "IsRevoked"`,
     which under `READ COMMITTED` blocks on the concurrent writer's row lock and then
     re-evaluates the predicate — so exactly one of two racers gets `affected == 1`.
     The loser returns `Results.Unauthorized()` and mints nothing.
  3. Only after a successful `TryRevokeForRotationAsync` do
     `GenerateAccessToken` + `GenerateRefreshTokenAsync`, then set the cookie, then
     `tx.CommitAsync(ct)`.
  4. Leave `RevokeRefreshTokenAsync` (used by `RevokeTokenEndpoint` for logout) as it
     is — an unconditional revoke is correct there.
- **Database requirements:**
  - Affected table: `RefreshTokens`. No schema change and **no migration** — the fix is a
    conditional `UPDATE` plus a transaction, not new structure.
  - Query: `UPDATE "RefreshTokens" SET "IsRevoked" = true, "RevokedAt" = @now WHERE "Id" = @id AND NOT "IsRevoked"`.
  - Transaction requirement: the revoke, the insert of the replacement row, and the cookie write commit together.
  - Concurrency strategy: conditional update on the existing primary key; the `WHERE NOT "IsRevoked"` predicate is the concurrency token. No `xmin`/`RowVersion` needed here (see BE-014 for the entities that do want one).
  - Isolation: `READ COMMITTED` suffices; do not escalate to `SERIALIZABLE`.
  - Expected behaviour: two concurrent rotations of the same cookie → exactly one 200 with a new cookie, one 401, and exactly one new `RefreshTokens` row.
- **API requirements:**
  - Endpoint: `POST /api/v1/auth/refresh`. Request shape unchanged (cookie-only).
  - Response: 200 `{ accessToken }` + rotated `refresh_token` cookie on success; 401 with no body on a lost race, an unknown token, or a revoked/expired token. No new status codes, so no client change and no compatibility implications.
- **Acceptance criteria:**
  - Two concurrent `POST /api/v1/auth/refresh` calls with the same cookie produce exactly one 200 and one 401.
  - Exactly one new `RefreshTokens` row exists afterwards for that user beyond the revoked original.
  - The 401 path does **not** call `RevokeAllForUserAsync` — a lost race must not log the user out of their other sessions.
  - A forced exception between revoke and issue leaves the original token active (transaction rolled back), so the client's existing cookie still works.
- **Validation:** Add `tests/Soulsjwa.IntegrationTests/RefreshTokenRotationConcurrencyTests.cs`
  (needs the real Postgres provider — the InMemory provider cannot express the race)
  firing two refreshes on the same cookie via `Task.WhenAll` and asserting one 200 / one
  401 and the row counts above. Add a rollback test that throws after the revoke and
  asserts the original token still validates. Existing `TokenRefreshTests.cs` and
  `RefreshAndRevokeTokenEndpointTests.cs` must pass unchanged.

---

### [BE-005] Stop overlay tokens travelling in the query string

- **Priority:** P1
- **Axis:** Security
- **Location:** `src/Soulsjwa.Api/Features/Events/Endpoints/OverlayTokensEndpoint.cs:GetOverlayScoreboard`; `src/Soulsjwa.Api/Program.cs` (`UseSerilogRequestLogging`, the `"OverlayScoreboard"` output-cache policy)
- **Problem:** `GET /api/v1/events/{eventId}/overlay-scoreboard?token=ot_…` carries a
  bearer-equivalent credential as a URL query parameter. `Program.cs` then calls
  `app.UseSerilogRequestLogging(...)`, and Serilog.AspNetCore's request-logging
  middleware records the request's raw target — which includes the query string — as the
  `RequestPath` property of every completed-request log event. Those events are written
  to stdout as JSON (`RenderedCompactJsonFormatter`) and, when
  `OpenTelemetry:Enabled` is true, exported over OTLP to a collector.
- **Attack vector:** Credential harvesting from logs and from every intermediary that
  records URLs.
- **Required attacker capabilities:** Read access to application logs or the telemetry
  backend — a support engineer, anyone with access to the log aggregator, a
  misconfigured collector, or a leaked log export. Separately, and outside the
  application's control: reverse-proxy and CDN access logs, the streamer's browser
  history, and any `Referer` header an embedded page sends.
- **Affected resource:** The full scoreboard of the event the token is scoped to, for as
  long as the token is un-revoked. Overlay tokens are minted by any competitor in the
  event (`CreateToken` allows owner, admin, **or** competitor), they never expire, and
  `OverlayToken.Generate()` produces no expiry field at all.
- **Current vulnerable path:** OBS browser source polls the URL → Kestrel → Serilog
  request logging writes `RequestPath` including `?token=ot_…` → stdout/OTLP → log store.
  Nothing in the pipeline redacts it.
- **Concrete remediation:**
  1. **Confirm the behaviour first.** Add `tests/Soulsjwa.ApiTests/OverlayTokenLoggingTests.cs`
     which installs an in-memory Serilog sink, issues one overlay-scoreboard request
     with a known token, and asserts no emitted log event's rendered text contains the
     token. Against today's code this must fail. If it passes, narrow this task to items
     3–5 below (the URL-exposure problems that hold regardless of logging) and record
     that the logging half did not reproduce.
  2. Accept the token in an `X-Overlay-Token` request header as the primary mechanism.
     Add the constant to a shared `OverlayToken.HeaderName`. Read the header first and
     fall back to the query parameter.
  3. Keep the query parameter working — an OBS browser source is a URL and cannot set
     headers — but stop it reaching the logs: configure
     `options.GetMessageTemplate` or set
     `diagnosticContext.Set("RequestPath", <path without query>)` inside the existing
     `EnrichDiagnosticContext` callback so the query string is never part of the logged
     path for this route. Simplest robust form: redact any `token` query value globally
     in the enricher rather than special-casing one route.
  4. Give overlay tokens an expiry. Add `ExpiresAt` (nullable `timestamptz`) to
     `EventOverlayToken`, default it to 90 days at mint time, surface it on
     `OverlayTokenResponse`, and reject expired tokens in `GetOverlayScoreboard`
     alongside the existing `!t.IsRevoked` predicate.
  5. Set `Referrer-Policy: no-referrer` on the overlay-scoreboard response specifically
     (the global header is `strict-origin-when-cross-origin`, which still leaks the
     origin+path; for a URL that contains a secret, send nothing).
- **Database requirements:**
  - Affected entity/table: `EventOverlayToken` / `EventOverlayTokens`.
  - Required change: add nullable `ExpiresAt timestamp with time zone`.
  - Migration: `dotnet ef migrations add AddOverlayTokenExpiry` — additive and nullable, so existing rows are unaffected and keep behaving as non-expiring.
  - Index: none needed; the existing `(EventId, TokenPrefix)` index still drives the lookup, and `ExpiresAt` is an additional predicate on an already-narrow result.
  - Expected query behaviour: `WHERE "EventId" = @e AND "TokenPrefix" = @p AND "TokenHash" = @h AND NOT "IsRevoked" AND ("ExpiresAt" IS NULL OR "ExpiresAt" > now())`.
- **API requirements:**
  - Endpoint: `GET /api/v1/events/{eventId}/overlay-scoreboard`.
  - Request: accepts `X-Overlay-Token` header **or** `?token=` query parameter. Header wins when both are present.
  - Response: 200 `ScoreboardResponse`; 401 for missing, malformed, revoked or expired tokens — all four with the identical body, so the endpoint is not an oracle.
  - The `"OverlayScoreboard"` output-cache policy currently does `SetVaryByQuery("token")`. It must additionally vary by the header, or the header path must bypass the cache — otherwise two different tokens share one cache entry. Prefer varying by both.
  - Compatibility: existing OBS sources using `?token=` keep working; no breaking change. `CreateOverlayTokenResponse` gains an `expiresAt` field (additive).
- **Acceptance criteria:**
  - No emitted log event contains the raw token value for any request to the endpoint.
  - A request authenticating via `X-Overlay-Token` returns the same body as the equivalent `?token=` request.
  - Two distinct valid tokens for the same event do not serve each other's cached response.
  - A token past `ExpiresAt` returns 401 with a body identical to the unknown-token 401.
  - `docs/streamer-overlay.md` and `docs/api-reference.md` document the header, the expiry, and the fact that the query form is a compatibility fallback.
- **Validation:** The new `OverlayTokenLoggingTests`; new cases in
  `tests/Soulsjwa.ApiTests/OverlayTokensEndpointTests.cs` for header auth, expired
  token, and cache isolation between two tokens.

---

### [BE-006] Bound `GET /api/v1/calendar`

- **Priority:** P1
- **Axis:** Performance
- **Location:** `src/Soulsjwa.Api/Features/Calendar/Endpoints/GlobalCalendarEndpoint.cs:GetGlobalCalendar`
- **Problem:**

  ```csharp
  var entries = await db.CalendarEntries.Include(e => e.Event).ToListAsync(ct);
  var plannedRuns = await db.PlannedRuns
      .Include(r => r.Event)
      .Include(r => r.EventGame).ThenInclude(eg => eg.KnownGame)
      .Include(r => r.User)
      .ToListAsync(ct);
  ```

  No `Where`, no `Skip`/`Take`, no date range, no projection, no cache, and
  `.AllowAnonymous()`. Every call materialises every calendar entry across every
  non-archived event as tracked entities, each carrying `DescriptionMarkdown` — which
  `CalendarEntriesEndpoint.MaxDescriptionBytes` permits to be 64 KiB — plus every
  planned run with three joined navigations.
- **Failure scenario:** 500 calendar entries averaging 8 KiB of markdown is a ~4 MB
  JSON response built by fully materialising and change-tracking ~500 entities plus
  their events. An anonymous client requesting it in a loop is bounded only by the
  global 60/min-per-IP limiter, so one host can pull ~240 MB/minute of
  serialise-from-scratch work; several hosts trivially saturate the DB connection pool
  and the LOH. There is no cache to absorb any of it. The response is also unusable to
  the frontend as it grows — a calendar page needs one month, not all history.
- **Why:** This is the single cheapest way for an unauthenticated caller to consume
  disproportionate server resources, and it gets monotonically worse because calendar
  entries are never deleted on a schedule.
- **Change:**
  1. Require a bounded window. Add `from` and `to` `DateTimeOffset` query parameters.
     When omitted, default to `[today − 7 days, today + 60 days]`. Reject a span wider
     than a named `MaxWindowDays = 400` constant with 400 `ValidationProblem`.
  2. Replace both `Include` chains with `Select` projections straight into
     `GlobalCalendarEntryResponse` / `GlobalPlannedRunResponse` so EF emits exactly the
     columns needed and tracks nothing. The existing `ToResponse` helpers become
     expression-friendly projections or are inlined.
  3. Drop `DescriptionMarkdown` from the **list** response entirely. The global calendar
     renders titles and time ranges; the body is read from
     `GET /api/v1/events/{eventId}/calendar-entries/{entryId}`. This alone removes the
     bulk of the payload. If the frontend genuinely renders markdown inline, return a
     truncated excerpt with a named `ExcerptLength` constant instead of the full document.
  4. Add `.CacheOutput(...)` with a 60-second policy and a new `CacheTags.Calendar` tag;
     evict that tag from `CalendarEntriesEndpoint.Create/Update/Delete` and
     `PlannedRunsEndpoint.Create/Update/Delete`.
  5. Cap the result at a named `MaxEntries`/`MaxPlannedRuns` (e.g. 2000 each) as a
     backstop and return a `truncated: true` flag when the cap is hit.
- **Database requirements:**
  - Affected entities/tables: `CalendarEntry` / `CalendarEntries`, `PlannedRun` / `PlannedRuns`.
  - Relevant query after the change: `WHERE "StartsAt" < @to AND "EndsAt" > @from` (overlap, not containment — an entry spanning the window must be included) on both tables, joined to `Events` for the name.
  - Required index: `CalendarEntries` currently has `(EventId, StartsAt)` and `PlannedRuns` has `(EventId, StartsAt)` and `(UserId, StartsAt)`. None of those lead with `StartsAt`, so a cross-event window scan cannot use them. Add `IX_CalendarEntries_StartsAt` on `("StartsAt")` and `IX_PlannedRuns_StartsAt` on `("StartsAt")`.
  - Migration: `dotnet ef migrations add AddCalendarWindowIndexes`. Index-only, no data change, safe to apply online at this table size.
  - Transaction requirement: none (read-only).
  - Expected query behaviour: an index scan bounded by the window instead of a sequential scan of both tables. Verify with `EXPLAIN ANALYZE` on a seeded dataset that the plan is `Index Scan` / `Bitmap Heap Scan` and not `Seq Scan`.
- **API requirements:**
  - Endpoint: `GET /api/v1/calendar`.
  - Request: new optional `from` / `to` (ISO-8601). Validation as above.
  - Response: same `GlobalCalendarResponse` shape minus `descriptionMarkdown` (or with an excerpt), plus `truncated`. 200 on success, 400 `ValidationProblem` for an over-wide or inverted window.
  - Compatibility: **this is a breaking change** for any client that relied on receiving all history in one call and on `descriptionMarkdown` being present. Migration required: update `src/Soulsjwa.Web/src/features/calendar/api/` and its hooks to pass the visible month's range and to fetch the body on demand. Ship both halves in the same PR — per `AGENTS.md` §2, touching a shared contract means running both backend and frontend suites.
- **Acceptance criteria:**
  - A request with no parameters returns only entries overlapping the default window.
  - `from`/`to` more than `MaxWindowDays` apart returns 400.
  - The generated SQL contains no `LEFT JOIN` fan-out beyond what the projection needs, and EF tracks zero entities (assert via `db.ChangeTracker.Entries().Count == 0` in a test, or use `AsNoTracking` explicitly).
  - Two identical requests within 60 seconds hit the output cache (assert one DB round trip via a command interceptor, or assert the `Age`/timing).
  - Creating a calendar entry evicts the cache.
- **Validation:** Extend `tests/Soulsjwa.ApiTests/GlobalCalendarEndpointTests.cs` with
  window filtering, the overlap boundary cases (entry starting before `from` and ending
  inside it), the over-wide-window 400, and cache eviction on write. Add a seeded
  benchmark or an `EXPLAIN` assertion in `tests/Soulsjwa.IntegrationTests/`.

---

### [BE-007] Stop building full scoreboards to render the "my events" dashboard

- **Priority:** P1
- **Axis:** Performance
- **Location:** `src/Soulsjwa.Api/Features/Events/Endpoints/MyEventsEndpoint.cs:List`
- **Problem:** `List` calls
  `ScoreboardEndpoint.BuildBatchAsync(eventIds, db, ct, includeArchived: true, enabledGamesOnly: true)`
  for **every** event the caller owns, competes in, or moderates — no pagination, no
  limit. `BuildBatchAsync` loads all event games with `.Include(eg => eg.Objectives)`
  and `.Include(eg => eg.CompetitorInfos)`, all competitors with `.Include(ec => ec.User)`,
  and every official `CompletedObjective` and `FailedObjective` row for all of them.
  `BuildEntries` then constructs, per event, a `ScoreboardEntry` per competitor, each
  containing a `GameBreakdown` per game, each containing an `ObjectiveDetail` per
  objective — the complete matrix — and `List` reads `TotalScore`, `Rank`,
  `CompletedCount`, `FailedCount` and `Entries.Count` off the top of it and discards the
  rest.

  `BuildEntries` also contains a nested linear scan: for each competitor it evaluates
  `completedObjectives.Where(co => co.UserId == competitor.UserId).ToList()`, so the
  in-memory cost is O(competitors × completions) on top of the allocation.
- **Failure scenario:** An admin who created 40 events, each with 15 competitors and
  180 objectives, loads their dashboard. That is 40 × 15 × 180 = 108,000
  `ObjectiveDetail` records allocated, plus 600 `GameBreakdown`s and their nested
  `CompetitorInfoResponse` lists, to produce 40 rows of five integers each. The endpoint
  is authenticated and uncached, so every dashboard poll repeats it. At 10x the current
  event count this is the first endpoint to exhaust gen-2 heap and stall the request
  pipeline.
- **Why:** The data volume is three orders of magnitude larger than the response. This
  is the clearest single scalability defect in the codebase, and unlike BE-006 it is on
  the path every signed-in user hits on their landing page.
- **Change:**
  1. Add a dedicated aggregate query rather than reusing the scoreboard builder. Put it
     next to the existing builders as
     `ScoreboardEndpoint.BuildSummaryBatchAsync(IReadOnlyList<Guid> eventIds, AppDbContext db, CancellationToken ct)`
     returning
     `Dictionary<Guid, IReadOnlyList<CompetitorSummary>>` where
     `CompetitorSummary(Guid UserId, string DisplayName, int TotalScore, int CompletedCount, int FailedCount, int TotalObjectives, long? TotalInGameTimeMs, DateTime? LastCompletedAt)`.
     Compute it with grouped projections that execute **in the database**:
     - one query grouping `CompletedObjectives` joined to `Objectives` → `EventGames` by `(EventId, UserId)` for `SUM(Score)`, `COUNT(*)`, `MAX(CompletedAt)`, and a conditional `SUM(InGameTimeMs)` that yields `NULL` when any row's `InGameTimeMs` is null (preserve the existing all-or-nothing semantics in `BuildEntries` exactly — a partially-timed competitor must still report `null`);
     - one query grouping `FailedObjectives` the same way for `COUNT(*)`;
     - one query for `COUNT(*)` of objectives per event;
     - one query for the competitor roster per event.
     All filtered on `TrialRunId IS NULL` and, when `enabledGamesOnly`, on `EventGames.IsEnabled`, matching today's filters.
  2. Reuse `ScoreboardRanking.Sort` + `ScoreboardRanking.AssignRanks` on the summaries so
     rank semantics stay identical — make `CompetitorSummary` implement
     `IScoreboardSortable`. Do **not** duplicate the ranking rules.
  3. Replace the `BuildBatchAsync` call in `MyEventsEndpoint.List` with the new method.
     `GetLastActivity` currently reads `entry.Games.SelectMany(g => g.Infos)` to find the
     most recent clip/note — add a fourth grouped query returning
     `MAX(CreatedAt)` and the corresponding `Type` per `(EventId, UserId)` from
     `EventGameCompetitorInfos`, instead of materialising every info row.
  4. Paginate. Add `page`/`pageSize` (default 20, max 100, clamped like
     `EventsEndpoint.ListEvents`) so the endpoint is bounded even as a user's event count
     grows. Return `PaginatedResponse<…>` for each of the three groups, or paginate the
     union and keep the grouping client-side — pick whichever the existing frontend
     consumes with the smaller change and say which in the PR.
  5. Fix the lifecycle-status query while here: it currently pulls **all**
     `EventStarted`/`EventStopped` audit rows for the events and calls `DistinctBy` in
     memory. Replace with a per-event `MAX(CreatedAt)` correlated subquery, or a
     `DISTINCT ON ("EventId") … ORDER BY "EventId", "CreatedAt" DESC` raw query.
  6. Leave `BuildAsync` / `BuildBatchAsync` alone — the real scoreboard endpoints need
     the full matrix and are output-cached. Only the dashboard stops using them.
- **Database requirements:**
  - Affected entities/tables: `CompletedObjectives`, `FailedObjectives`, `Objectives`, `EventCompetitors`, `EventGameCompetitorInfos`, `AuditLogs`.
  - Relevant queries: the four grouped aggregates above, each joined from `Objectives.EventGameId` → `EventGames.EventId`.
  - Required indexes: `Objectives` already has `IX_Objectives_EventGameId`; `CompletedObjectives` and `FailedObjectives` each already have an index on `UserId` and the two partial unique indexes on `(ObjectiveId, UserId)`. The grouped aggregate joins on `ObjectiveId`, which is the leading column of `IX_CompletedObjectives_ObjectiveId_UserId_Official` — usable. Add nothing speculatively; run `EXPLAIN ANALYZE` on the new queries first and only add an index if the plan shows a sequential scan.
  - Migration: none expected. If step 6's `EXPLAIN` proves an index is needed, add it in its own migration with the plan output quoted in the PR body.
  - Transaction requirement: none (read-only).
  - Expected behaviour: a fixed number of queries (five to seven) regardless of event count, each returning one row per (event, competitor) rather than one row per (event, competitor, objective).
- **API requirements:**
  - Endpoint: `GET /api/v1/me/events`.
  - Response: the same `MyEventsResponse` field-for-field. This is a pure internal rewrite — the contract does not change except for the added pagination envelope.
  - Compatibility: pagination is the only client-visible change; update `src/Soulsjwa.Web/src/features/myEvents/` in the same PR.
- **Acceptance criteria:**
  - For a fixture with 3 events × 5 competitors × 10 objectives, `GET /api/v1/me/events` returns byte-identical JSON (modulo the pagination envelope) before and after the change.
  - The number of SQL commands executed is constant as the event count grows — assert with a `DbCommandInterceptor` counting commands for a 2-event and a 10-event fixture and requiring the same count.
  - No `ObjectiveDetail` instance is allocated on this path.
  - Rank values match `ScoreboardEndpoint.BuildAsync` for the same data, including `SharedPlace` ties.
  - `TotalInGameTimeMs` is `null` whenever any of the competitor's completions lacks a time.
- **Validation:** Extend `tests/Soulsjwa.ApiTests/MyEventsEndpointTests.cs` with a
  golden-output comparison against `BuildAsync` for a fixed fixture, the tie-break
  parity case, and the partial-in-game-time null case. Add the command-count assertion.
  `Depends on: BE-003` — do that first so the archived-event semantics this endpoint
  relies on (`includeArchived: true`) are settled before the rewrite.

---

### [BE-008] Validate `Rule`, `FailRule` and `Metadata` before they reach `jsonb`

- **Priority:** P1
- **Axis:** Correctness
- **Location:** `src/Soulsjwa.Api/Features/Events/Endpoints/ObjectivesEndpoint.cs:CreateObjective` / `CreatePredefined` / `PatchObjective`; column mapping in `src/Soulsjwa.Api/Infrastructure/Data/AppDbContext.cs` (`Objective` → `HasColumnType("jsonb")` for `Metadata`, `Rule`, `FailRule`)
- **Problem:** `Objective.Rule`, `Objective.FailRule` and `Objective.Metadata` are `string?`
  properties mapped to Postgres `jsonb`. The endpoints assign the request value straight
  through — `Rule = request.Rule` — with no parse and no shape check.

  Two distinct defects:
  1. **Malformed JSON → 500.** Npgsql sends the string as `jsonb`; Postgres rejects it
     with SQL state `22P02`, surfacing as a `DbUpdateException` that nothing catches.
     `UseExceptionHandler` turns it into a 500 `application/problem+json`. A client
     mistake produces a server error instead of a 400.
  2. **Well-formed but meaningless JsonLogic → silent no-op.** `RuleEvaluator.Evaluate`
     catches `JsonException`, `InvalidOperationException` and `ArgumentException` and
     returns `false`. An objective whose rule is `{"=="` — sorry, `{"nonsense": 1}` —
     parses fine, stores fine, and then never completes for anyone. The event owner gets
     no feedback at any point; competitors just find an objective that cannot be
     achieved. This is the worse of the two because it fails silently during a live
     competition.
- **Failure scenario:** An owner uses the Blockly rule builder, a serialisation bug
  emits `{"and":[{"var":"100"},{">=":[{"var":"death_count"}]}]}` (a `>=` with one
  operand). `POST …/objectives` returns 201. During the event the objective never
  auto-completes; the owner blames the connector; the scoreboard is wrong for everyone
  and there is no error anywhere to point at.
- **Why:** Silent rule failure during a live scored event is unrecoverable after the
  fact — you cannot retroactively decide who would have completed what. Validation at
  write time is the only point where it is cheap to catch.
- **Change:**
  1. Add `src/Soulsjwa.Api/Features/Games/Services/ObjectiveRuleValidator.cs` with

     ```csharp
     /// Returns null when the value is acceptable for a jsonb rule column,
     /// or a human-readable error for a 400 ValidationProblem.
     public static string? ValidateRule(string? json);
     public static string? ValidateMetadata(string? json);
     ```

     `ValidateMetadata` checks only that the value is `null`, empty, or parses as JSON
     (`JsonDocument.Parse`). `ValidateRule` additionally requires the root to be a JSON
     **object** and performs a smoke evaluation: `RuleEvaluator.Evaluate(json, "{}")`
     inside a try/catch that distinguishes "threw" from "returned false". A rule that
     throws on an empty data object is structurally invalid and must be rejected; one
     that merely returns `false` is fine (most rules do, against empty data).
  2. Call both from `CreateObjective`, `CreatePredefined` and `PatchObjective`, merging
     into the existing `Dictionary<string, string[]> errors` alongside the current
     `Name`/`Score` checks, and return `Results.ValidationProblem(errors)`.
  3. Add a defensive `catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "22P02" })` →
     400 on the three write paths, following the pattern `EventsEndpoint.PatchEvent`
     already uses for `UniqueViolation`. Put `22P02` in a named constant — `PostgresErrorCodes`
     (already imported in `EventsEndpoint`) exposes `InvalidTextRepresentation`; use that,
     not a literal.
  4. Enforce a size bound. Add a named `MaxRuleBytes` (suggest 32 KiB, matching the
     spirit of the 64 KiB markdown cap) and reject larger payloads — `jsonb` has no
     practical limit and `RuleEvaluator` walks the tree per objective per submission.
  5. `EventDuplicationEndpoint` copies `Rule`/`FailRule`/`Metadata` verbatim from an
     existing event. Leave it alone: those values already passed validation on the way in,
     and re-validating a copy would fail an entire duplication because of one legacy row.
- **Database requirements:**
  - Affected entity/table: `Objective` / `Objectives`. Columns `Metadata`, `Rule`, `FailRule` (all `jsonb`).
  - No schema change and **no migration**. The `jsonb` column type is correct and is what makes the malformed case detectable at all — do not weaken it to `text`.
  - Existing rows: any already-stored value is by definition valid `jsonb` (Postgres accepted it), so no backfill is required. Rows that are valid JSON but broken JsonLogic may exist; they are out of scope for this task — note it in the PR and, if the owner wants them found, that is a one-off script, not application code.
- **API requirements:**
  - Endpoints: `POST /api/v1/events/{eventId}/games/{eventGameId}/objectives`, `PATCH …/objectives/{objectiveId}`, `POST /api/v1/objectives/predefined`.
  - Request behaviour: unchanged field names; `rule`, `failRule` and `metadata` are now validated.
  - Response: 400 `ValidationProblem` with keys `Rule` / `FailRule` / `Metadata` replaces the previous 500. 201/200 on the success paths unchanged.
  - Compatibility: strictly tightening. A client that was sending broken JSON was already getting a 500, so no working integration breaks.
- **Acceptance criteria:**
  - `{"rule": "not json"}` returns 400 with a `Rule` key, not 500.
  - `{"rule": "[1,2,3]"}` (valid JSON, wrong root type) returns 400.
  - `{"rule": "{\"==\":[1,1]}"}` returns 201.
  - `{"metadata": "{\"area\":\"Undead Burg\"}"}` returns 201; `{"metadata": "{"}` returns 400.
  - A rule larger than `MaxRuleBytes` returns 400.
  - No code path can write a value to a `jsonb` column without having parsed it first.
- **Validation:** New `tests/Soulsjwa.UnitTests/ObjectiveRuleValidatorTests.cs` covering
  each case above; extend `tests/Soulsjwa.ApiTests/ObjectivesEndpointTests.cs` for the
  400 responses on all three endpoints. `tests/Soulsjwa.UnitTests/RuleEvaluatorEdgeCaseTests.cs`
  must still pass — the validator must not reject anything that file asserts is
  evaluable.

---

### [BE-009] Scope the scoreboard cache tag per event

- **Priority:** P1
- **Axis:** Performance
- **Location:** `src/Soulsjwa.Api/Common/CacheTags.cs`; every `cache.EvictByTagAsync(CacheTags.Scoreboard, ct)` call site (`CompletedObjectivesEndpoint`, `FailedObjectivesEndpoint`, `ObjectivesEndpoint`, `EventGamesEndpoint`, `ScoreboardEndpoint.SetLive`, `TrialRunsEndpoint.ResetTrialRun`, `ConnectorEndpoint.SubmitGameData`); the `"Scoreboard"` and `"OverlayScoreboard"` policies in `src/Soulsjwa.Api/Program.cs`
- **Problem:** There is exactly one tag:

  ```csharp
  public static class CacheTags { public const string Scoreboard = "scoreboard"; }
  ```

  Every scoreboard response for every event is tagged with it, and every write in any
  event evicts all of them. During a live event, connector submissions arrive at up to
  120/min per competitor; each one that records a completion or failure evicts the
  cached scoreboard of **every event in the system**. The 1-hour `Expire` on the
  `"Scoreboard"` policy is therefore fiction — in practice the cache is cold, and
  `BuildAsync` (the full competitor × game × objective matrix, see BE-007) runs on
  every public scoreboard request.

  Second, related problem: `AddOutputCache` uses the default in-memory store. In a
  multi-instance deployment `EvictByTagAsync` only clears the local node, so the other
  nodes serve stale scoreboards for up to an hour. The codebase already acknowledges
  single-instance assumptions for DataProtection in `Program.cs`; this one is
  undocumented and much more visible to users.
- **Failure scenario:** Two events run the same evening. Event A has 12 competitors
  submitting connector data. Event B's public scoreboard page — the one an audience of
  viewers is refreshing — never serves a cache hit, because A's traffic evicts B's entry
  several times a second. Both events pay full `BuildAsync` cost per viewer request.
- **Why:** The cache is the only thing standing between a public, anonymous,
  expensive-to-compute endpoint and the audience of a live stream. Right now it provides
  close to zero protection under exactly the conditions it was added for.
- **Change:**
  1. Replace the constant with a factory, keeping a named prefix per `AGENTS.md` §4:

     ```csharp
     public static class CacheTags
     {
         private const string ScoreboardPrefix = "scoreboard";
         /// Tag for one event's cached scoreboard/score/overlay responses.
         public static string Scoreboard(Guid eventId) => $"{ScoreboardPrefix}:{eventId:N}";
     }
     ```
  2. Tag responses per event. The `"Scoreboard"` and `"OverlayScoreboard"` policies
     currently call `.Tag(CacheTags.Scoreboard)` at configuration time, which cannot see
     the route value. Replace the static `.Tag(...)` with a policy that reads
     `eventId` from `context.HttpContext.Request.RouteValues` and adds the per-event tag
     at request time — implement `IOutputCachePolicy` (or use the
     `AddPolicy(name, builder => builder.Tag(...))` overload that accepts a per-request
     tag provider, whichever the installed ASP.NET Core version exposes; check before
     writing).
  3. Change every `EvictByTagAsync(CacheTags.Scoreboard, ct)` call to pass the event id
     in scope. Every one of those call sites already has an `eventId` in hand — verify
     each, and for `ObjectivesEndpoint.DeleteObjective` / `PatchObjective` use the
     `eventId` route value, not the objective's.
  4. Document the multi-instance limitation. Add a comment next to `AddOutputCache`
     mirroring the existing DataProtection note, stating that tag eviction is
     node-local and that a shared output-cache store is a prerequisite for running more
     than one instance. Add the same line to `docs/deployment.md`. Do **not** add Redis
     now — see Deferred.
- **Acceptance criteria:**
  - A write in event A does not evict the cached scoreboard of event B.
  - A completion in event A does evict `/api/v1/events/{A}/scoreboard`, `/api/v1/events/{A}/scores` and `/api/v1/events/{A}/overlay-scoreboard`.
  - The overlay policy still varies by token (see BE-005) *and* carries the per-event tag.
  - `docs/deployment.md` states the single-instance constraint for output caching.
- **Validation:** New `tests/Soulsjwa.ApiTests/ScoreboardCacheEvictionTests.cs`: seed two
  events, warm both scoreboards, write a completion in one, assert the other still
  serves the cached body (compare against a value mutated directly in the DB so a cache
  hit is observable) while the first reflects the new completion.

---

### [BE-010] Enforce "at most one featured event" in the database

- **Priority:** P1
- **Axis:** Correctness
- **Location:** `src/Soulsjwa.Api/Features/Events/Endpoints/EventsEndpoint.cs:FeatureEvent`; `src/Soulsjwa.Api/Infrastructure/Data/AppDbContext.cs` (`Event` configuration)
- **Problem:** `FeatureEvent` implements the invariant purely in application code:

  ```csharp
  var previouslyFeatured = await db.Events.Where(e => e.IsFeatured && e.Id != id).ToListAsync(ct);
  foreach (var other in previouslyFeatured) { other.IsFeatured = false; … }
  ev.IsFeatured = true;
  await db.SaveChangesAsync(ct);
  ```

  Read-then-write with no transaction, no lock, and no database constraint. The
  codebase already solves the identical problem correctly one entity over — `EventGame`
  has a partial unique index `IX_EventGames_EventId_ActiveGame` on `(EventId) WHERE "IsEnabled"`
  with a comment explaining that the invariant must not depend on one code path being
  the only writer. `Event.IsFeatured` got the code half and not the database half.
- **Failure scenario:** Two admins feature different events within the same moment (or
  one admin double-clicks). Both transactions read `previouslyFeatured` before either
  commits, so neither sees the other's event; both set their own `IsFeatured = true`.
  Two rows now have `IsFeatured = true`. `GetFeaturedEvent` uses `FirstOrDefaultAsync(e => e.IsFeatured)`
  with no ordering, so the public landing page shows a non-deterministic one of the two
  and can flip between requests.
- **Why:** Same reasoning the `EventGame` comment already states: an invariant defended
  only by the current shape of one method is not an invariant. The fix is four lines and
  a migration, and it makes the two "single active X" rules in the schema consistent.
- **Change:**
  1. Add the partial unique index in `AppDbContext.OnModelCreating`, inside the existing
     `modelBuilder.Entity<Event>(e => { … })` block, mirroring the `EventGame` one:

     ```csharp
     // At most one featured event across the whole site — DB-enforced so the
     // invariant can't be violated by a future code path, not just by
     // FeatureEvent's own unfeature-the-others logic.
     e.HasIndex(x => x.IsFeatured)
         .IsUnique()
         .HasFilter("\"IsFeatured\"")
         .HasDatabaseName("IX_Events_FeaturedEvent");
     ```

     A unique index on a single column filtered to `WHERE "IsFeatured"` permits at most
     one `true` row site-wide, which is exactly the rule.
  2. Wrap `FeatureEvent`'s unfeature + feature in an explicit transaction, and order the
     writes so the unfeature lands before the feature — the same ordering hazard as
     BE-012. The safest form is two statements: an `ExecuteUpdateAsync` clearing the flag
     on all other rows, then the `SaveChangesAsync` that sets it, both inside one
     `BeginTransactionAsync`.
  3. Catch the unique violation and translate it. Follow `EventsEndpoint.PatchEvent`'s
     existing pattern:

     ```csharp
     catch (DbUpdateException exception)
         when (exception.InnerException is PostgresException
               { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: FeaturedIndexName })
     {
         return Results.Problem(
             detail: "Another event was featured concurrently. Retry.",
             statusCode: StatusCodes.Status409Conflict);
     }
     ```

     Add `FeaturedIndexName` as a `private const string` beside the existing
     `UrlAliasIndexName`.
- **Database requirements:**
  - Affected entity/table: `Event` / `Events`. Column `IsFeatured` (`boolean`).
  - Required constraint: partial unique index `IX_Events_FeaturedEvent` on `("IsFeatured") WHERE "IsFeatured"`.
  - Migration: `dotnet ef migrations add AddFeaturedEventUniqueIndex`. **The migration will fail on any database that already has two or more featured rows** — that is the point, but it must not break a deploy. Precede the `CreateIndex` with a data-fix statement in the same migration: `UPDATE "Events" SET "IsFeatured" = false WHERE "Id" <> (SELECT "Id" FROM "Events" WHERE "IsFeatured" ORDER BY "UpdatedAt" DESC LIMIT 1) AND "IsFeatured";` — keep the most recently updated one. Say so in the migration's comment.
  - Transaction requirement: `FeatureEvent` runs the unfeature and the feature in one transaction at `READ COMMITTED`.
  - Concurrency strategy: the unique index is the arbiter; the loser of a race gets 23505 and is translated to 409.
  - Expected query behaviour: `SELECT … WHERE "IsFeatured"` on `GetFeaturedEvent` becomes an index scan over a one-row partial index.
- **API requirements:**
  - Endpoints: `POST /api/v1/events/{id}/feature`, `POST /api/v1/events/{id}/unfeature`, `GET /api/v1/events/featured`.
  - `feature` gains a 409 response for the concurrent case; it is already declared in the OpenAPI metadata for other endpoints, so add `.ProducesProblem(StatusCodes.Status409Conflict)` to the `feature` mapping.
  - Compatibility: additive. A client that never races never sees the 409.
- **Acceptance criteria:**
  - Two concurrent `POST /api/v1/events/{differentId}/feature` calls result in exactly one featured row; the loser gets 409, not 500 and not a silent success.
  - Featuring event B while A is featured leaves only B featured (unchanged behaviour).
  - Featuring an already-featured event is idempotent and returns 204.
  - The migration applies cleanly against a database seeded with two featured events, leaving exactly one.
- **Validation:** New integration test in `tests/Soulsjwa.IntegrationTests/` (needs real
  Postgres for the partial index) driving two concurrent feature calls via
  `Task.WhenAll`. Add a migration test that seeds two featured rows and asserts the
  migration succeeds with one survivor. Extend
  `tests/Soulsjwa.ApiTests/EventsEndpointTests.cs` for the idempotent case.

---

### [BE-011] Put a request-body size limit on the connector submit endpoint

- **Priority:** P1
- **Axis:** Security
- **Location:** `src/Soulsjwa.Api/Features/Connector/Endpoints/ConnectorEndpoint.cs:SubmitGameData`; `src/Soulsjwa.Shared/ConnectorSubmissionPayload.cs`; `src/Soulsjwa.Api/Program.cs` (Kestrel configuration — currently absent)
- **Problem:** `ConnectorSubmissionPayload` is `record ConnectorSubmissionPayload(string Data)`
  where `Data` is a JSON document carried as a string, with no length bound anywhere.
  A repository-wide grep for `MaxRequestBodySize`, `RequestSizeLimit` and
  `DisableRequestSizeLimit` returns nothing, so Kestrel's default 30 MB applies.
  `ConnectorSubmissionValidator.Validate` calls `JsonDocument.Parse(submissionData)` on
  whatever arrives, and `ConnectorIngameTime.ExtractMilliseconds` parses it a second time
  (see BE-016).
- **Failure scenario:** An authenticated competitor — or anyone holding a leaked
  connector API key, which is a realistic threat given those keys sit on desktop
  machines — posts a 30 MB `Data` string. The framework buffers 30 MB, model binding
  materialises a 30 MB `string` (LOH), `JsonDocument.Parse` builds a parse tree over it,
  and validation walks every property. At the `"connector"` policy's 120 requests/minute
  that is nominally 3.6 GB/minute of allocation per key; even throttled to the global
  100/min-per-user ceiling (BE-025) it is enough to drive sustained gen-2 pressure and
  starve the thread pool. The legitimate payload is a flat object of a few dozen
  integers — well under 4 KiB.
- **Why:** The gap between the legitimate payload size (kilobytes) and the permitted
  size (30 MB) is four orders of magnitude, on the highest-frequency write endpoint in
  the system, reachable with a credential that is expected to live on untrusted hardware.
- **Change:**
  1. Add a named constant `ConnectorSubmissionValidator.MaxDataBytes = 64 * 1024` — generous
     relative to the real payload, small enough to be harmless. Reject longer input in
     `Validate` with a 400 `ValidationProblem` keyed `Data`, before `JsonDocument.Parse`
     is reached, using `Encoding.UTF8.GetByteCount` (the same technique
     `LegalDocumentsEndpoint` and `EventRulesEndpoint` already use for their 64 KiB caps —
     reuse the idiom, do not invent a second one).
  2. Belt and braces at the transport layer: attach a per-endpoint limit so the body is
     never fully buffered in the first place. In `MapEndpoints`, add
     `.WithMetadata(new RequestSizeLimitAttribute(MaxRequestBytes))` to the submit
     mapping (or the minimal-API `.WithRequestSizeLimit(...)` extension if the installed
     version exposes it — check before writing). Size it slightly above `MaxDataBytes`
     to leave room for the JSON envelope.
  3. Set a conservative global default in `Program.cs`:
     `builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = GlobalMaxRequestBytes);`
     with `GlobalMaxRequestBytes` at 1 MiB. **`POST /api/v1/uploads` must keep its own
     larger limit** — `MediaEndpoint.MaxUploadBytes` is 5 MiB and `ReadCappedAsync`
     already enforces it while streaming — so attach an explicit per-endpoint override of
     `MediaEndpoint.MaxUploadBytes + envelope` to the uploads mapping. Verify the upload
     tests still pass; if the global limit breaks them, the override is wrong.
  4. Add a bound on the number of properties too. `Validate` already iterates
     `document.RootElement.EnumerateObject()`; reject beyond a named `MaxDataPoints`
     (e.g. 512). For a known game the catalog check already constrains the key set, but
     custom games skip the catalog entirely, which is where an unbounded object can slip
     through.
- **API requirements:**
  - Endpoint: `POST /api/v1/connector/events/{eventId}/games/{eventGameId}/submit`.
  - Response: 400 `ValidationProblem` with a `Data` key for an oversized or over-wide payload; 413 from the framework when the transport limit trips first. Declare both with `.ProducesProblem(StatusCodes.Status413PayloadTooLarge)`.
  - Compatibility: no legitimate connector payload approaches 64 KiB. Confirm against `src/Soulsjwa.Connector/` before merging — if any adapter can emit more, raise the constant rather than dropping the check, and say what the real ceiling is.
- **Acceptance criteria:**
  - A submission with `Data` larger than `MaxDataBytes` returns 400 (or 413) without ever calling `JsonDocument.Parse`.
  - A submission with more than `MaxDataPoints` properties returns 400.
  - A realistic connector payload from `tests/Soulsjwa.ConnectorTests` fixtures still returns 200.
  - `POST /api/v1/uploads` still accepts a 5 MiB image and still returns 413 above it — `tests/Soulsjwa.ApiTests/MediaEndpointTests.cs` passes unchanged.
  - No endpoint in the app accepts a body larger than 1 MiB except `/api/v1/uploads`.
- **Validation:** Extend `tests/Soulsjwa.ApiTests/ConnectorEndpointTests.cs` with the
  oversized and over-wide cases and a realistic-payload regression; re-run
  `MediaEndpointTests` to prove the upload override works.

---

## P2 — Normal

### [BE-012] Make `EnableEventGame` safe against the partial unique index

- **Priority:** P2
- **Axis:** Correctness
- **Location:** `src/Soulsjwa.Api/Features/Events/Endpoints/EventGamesEndpoint.cs:EnableEventGame`
- **Problem:** The handler mutates the outgoing and incoming rows and persists both in a
  single `SaveChangesAsync`:

  ```csharp
  var previouslyActive = ev.EventGames.Where(eg => eg.IsEnabled && eg.Id != eventGameId).ToList();
  foreach (var other in previouslyActive) other.IsEnabled = false;
  eventGame.IsEnabled = true;
  await db.SaveChangesAsync(ct);
  ```

  `IX_EventGames_EventId_ActiveGame` is a non-deferrable partial unique index on
  `(EventId) WHERE "IsEnabled"`. EF emits the updates as separate statements in one
  batch and does not guarantee they are ordered so the clear precedes the set. If the
  `IsEnabled = true` statement executes first, the index sees two enabled rows for the
  event at that instant and raises `23505` immediately — Postgres evaluates unique
  indexes per statement, not at commit, unless the constraint is declared
  `DEFERRABLE INITIALLY DEFERRED` (which an *index* cannot be; only a constraint can).
  The exception is uncaught, so it surfaces as a 500.
- **Failure scenario:** Owner switches the active game mid-event from game A to game B.
  Whether this 500s depends on EF's internal update ordering for the change-tracked
  entities, which is not part of its contract and can shift between EF versions or with
  the order entities were materialised. It will work in testing and fail in production
  after an upgrade.
- **Why:** The index is correct and valuable (it is what makes BE-010's sibling
  invariant trustworthy). The write path just needs to respect it. Today the code
  depends on undocumented ordering.
- **Change:**
  1. Wrap the operation in `await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);`
     (guarded by `db.Database.IsRelational()` for the InMemory test provider).
  2. Clear first, in its own statement, via `ExecuteUpdateAsync` so the ordering is
     explicit and not left to the change tracker:

     ```csharp
     await db.EventGames
         .Where(eg => eg.EventId == eventId && eg.IsEnabled && eg.Id != eventGameId)
         .ExecuteUpdateAsync(s => s.SetProperty(g => g.IsEnabled, false), ct);
     ```

     Note that `ExecuteUpdateAsync` bypasses the change tracker, so the in-memory
     `ev.EventGames` graph used to build the response is now stale — reset the flags on
     the tracked entities in memory as well, or re-read before mapping. The existing
     `audit.Log(... PreviouslyActiveEventGameIds ...)` payload needs the ids, so capture
     them before the update.
  3. Then set `eventGame.IsEnabled = true` and `SaveChangesAsync`, then `CommitAsync`.
  4. Add the same 23505 translation used elsewhere, keyed on the index name, returning
     409 with `"Another game was enabled concurrently. Retry."` and declare
     `.ProducesProblem(StatusCodes.Status409Conflict)` (already declared on this mapping).
  5. Apply the identical treatment to `BE-010`'s `FeatureEvent` — the two fixes share a
     shape and should land together or reference each other.
- **Database requirements:**
  - Affected entity/table: `EventGame` / `EventGames`. Existing index `IX_EventGames_EventId_ActiveGame` on `("EventId") WHERE "IsEnabled"`.
  - No schema change, **no migration**. Do not drop or weaken the index.
  - Transaction requirement: clear + set commit atomically.
  - Concurrency strategy: the index arbitrates; concurrent enables produce one winner and a 409 for the loser.
  - Expected behaviour: two statements in a deterministic order — `UPDATE … SET "IsEnabled" = false WHERE …` then `UPDATE … SET "IsEnabled" = true WHERE "Id" = @id`.
- **Acceptance criteria:**
  - Enabling game B while game A is enabled returns 200 and the response body lists exactly one enabled game.
  - The operation never returns 500 for the switch case, under any EF update ordering.
  - Two concurrent enables of different games in the same event yield one 200 and one 409.
  - The `EventGameEnabled` audit row still records `PreviouslyActiveEventGameIds` correctly.
  - `tests/Soulsjwa.IntegrationTests/ActiveGameConstraintTests.cs` passes unchanged.
- **Validation:** Extend `ActiveGameConstraintTests` (it already targets this invariant
  against real Postgres) with an A→B switch asserting 200 and a concurrent-enable case
  asserting 200/409. Extend `tests/Soulsjwa.ApiTests/EventGamesEndpointTests.cs` for the
  response body.

---

### [BE-013] Normalise `DateTimeKind` at the API boundary

- **Priority:** P2
- **Axis:** Correctness
- **Location:** `src/Soulsjwa.Api/Features/Calendar/Endpoints/CalendarEntriesEndpoint.cs` (`Create`, `Update`, `ValidateAsync`); `src/Soulsjwa.Api/Features/Calendar/Endpoints/PlannedRunsEndpoint.cs` (`Create`, `Update`); `src/Soulsjwa.Api/Features/Events/Endpoints/CompletedObjectivesEndpoint.cs:EditCompletionTime`
- **Problem:** Inbound `DateTime` values are assigned straight to entities whose
  properties map to Postgres `timestamp with time zone`:

  ```csharp
  entry.StartsAt = request.StartsAt;   // CalendarEntriesEndpoint.Create/Update
  run.StartsAt   = request.StartsAt;   // PlannedRunsEndpoint.Create/Update
  ```

  Since Npgsql 6, writing a `DateTime` whose `Kind` is `Unspecified` or `Local` to a
  `timestamptz` column throws `ArgumentException` — an uncaught 500. `"2026-01-01T10:00:00"`
  (no zone) binds as `Unspecified`; `"2026-01-01T10:00:00+02:00"` binds as `Local`.
  Only a trailing `Z` produces `Utc`.

  `EditCompletionTime` has the opposite bug — it avoids the throw but corrupts the value:

  ```csharp
  var newCompletedAt = DateTime.SpecifyKind(request.CompletedAt, DateTimeKind.Utc);
  ```

  `SpecifyKind` **relabels without converting**. A client sending `10:00:00+02:00` has
  it bound to local time and then stamped as UTC, storing an instant that is off by the
  offset. Because this endpoint exists specifically to correct completion times for
  scoring, a silent hour-scale shift changes who wins.
- **Failure scenario:** Any non-JavaScript API client — the WPF connector, a curl
  script, a future mobile client — sends an offset-bearing or zone-less timestamp. The
  calendar endpoints 500; the completion-time edit silently records the wrong instant
  and re-ranks the scoreboard on it. The browser frontend happens to be safe because
  `Date.prototype.toISOString()` always emits `Z`, which is why this has not been
  noticed.
- **Why:** A 500 where a 400 belongs is a bug; a silently shifted timestamp on the
  endpoint that adjudicates scoring is worse. Both come from the same missing
  normalisation step.
- **Change:**
  1. **Confirm first.** Add a test posting `{"startsAt": "2026-01-01T10:00:00", …}` to
     `POST /api/v1/events/{id}/calendar-entries` and assert it is not a 500. It must fail
     today. If it does not, the provider is configured differently than assumed — narrow
     this task to the `EditCompletionTime` half, which is a plain logic bug and holds
     regardless.
  2. Change the three request DTOs to use `DateTimeOffset` instead of `DateTime`:
     `CreateCalendarEntryRequest`, `UpdateCalendarEntryRequest`, `CreatePlannedRunRequest`,
     `UpdatePlannedRunRequest`, `EditCompletionTimeRequest`. `DateTimeOffset` cannot be
     ambiguous — a zone-less value binds with the server's offset, which is explicit
     rather than silently wrong, and an offset-bearing value round-trips exactly.
  3. Convert once, at the boundary: `entry.StartsAt = request.StartsAt.UtcDateTime;`
     (`UtcDateTime` both converts and yields `Kind == Utc`). Replace the `SpecifyKind`
     call in `EditCompletionTime` with `request.CompletedAt.UtcDateTime` — this is the
     line that changes behaviour, and the PR body must call it out explicitly as a
     behaviour fix, not a refactor.
  4. Add a shared guard so this cannot regress elsewhere: a small
     `src/Soulsjwa.Api/Common/UtcTime.cs` with
     `public static DateTime ToStorage(DateTimeOffset value) => value.UtcDateTime;` and
     `public static DateTime ToStorage(DateTime value) => value.Kind switch { DateTimeKind.Utc => value, DateTimeKind.Local => value.ToUniversalTime(), _ => throw … };`
     Use the `DateTimeOffset` overload at every boundary.
  5. Leave entity properties as `DateTime` (UTC by construction). Converting the whole
     schema to `DateTimeOffset` is a much larger migration for no benefit — see Deferred.
  6. The audit `before`/`after` payloads in these handlers serialise the same values;
     they inherit the fix.
- **Database requirements:**
  - Affected entities/tables: `CalendarEntry` / `CalendarEntries` (`StartsAt`, `EndsAt`), `PlannedRun` / `PlannedRuns` (`StartsAt`, `EndsAt`), `CompletedObjective` / `CompletedObjectives` (`CompletedAt`).
  - No schema change and **no migration** — columns are already `timestamp with time zone` and already store UTC instants correctly for values written through the browser.
  - Existing rows: values written via the browser are correct. Values written by any non-`Z` client would have thrown (calendar) or been shifted (`EditCompletionTime`). Shifted completion times cannot be identified retroactively from the data alone — note this in the PR; if the operator knows of specific edits, correcting them is a manual step, not part of this task.
  - Expected behaviour: every write to a `timestamptz` column passes a `DateTime` with `Kind == Utc`.
- **API requirements:**
  - Endpoints: `POST`/`PUT /api/v1/events/{eventId}/calendar-entries[/{entryId}]`, `POST`/`PUT /api/v1/events/{eventId}/competitors/{userId}/planned-runs[/{id}]`, `PATCH /api/v1/events/{eventId}/games/{eventGameId}/objectives/{objectiveId}/completions/{userId}`.
  - Request: fields accept full ISO-8601 including an offset. A zone-less value is now accepted and interpreted as server-local rather than 500-ing.
  - Response: unchanged shapes. Timestamps continue to serialise as UTC.
  - Compatibility: strictly widening — every payload that worked before still works and means the same thing. Clients sending offsets now get correct behaviour instead of an error or a shift.
- **Acceptance criteria:**
  - `"2026-01-01T10:00:00"`, `"2026-01-01T10:00:00Z"` and `"2026-01-01T12:00:00+02:00"` are all accepted by the calendar and planned-run endpoints.
  - `"2026-01-01T12:00:00+02:00"` posted to `EditCompletionTime` stores `10:00:00Z`, not `12:00:00Z`.
  - The `EndsAt > StartsAt` validation compares the two in the same frame of reference (compare the `DateTimeOffset`s directly, before conversion).
  - The `CompletedAt` bounds check against `ev.CreatedAt` and `now + 1 minute` still behaves identically for `Z` input.
  - No `DateTime.SpecifyKind` call remains in a write path.
- **Validation:** New `tests/Soulsjwa.UnitTests/UtcTimeTests.cs` for the conversion
  helper including the offset case. Extend
  `tests/Soulsjwa.ApiTests/CalendarEntriesEndpointTests.cs`,
  `PlannedRunsEndpointTests.cs` and `CompletedObjectivesEndpointTests.cs` with all three
  input formats, and add an explicit assertion that the offset form stores the correct
  instant.

---

### [BE-014] Add an optimistic concurrency token to the mutable aggregate roots

- **Priority:** P2
- **Axis:** Correctness
- **Location:** `src/Soulsjwa.Api/Infrastructure/Data/AppDbContext.cs` (entity configuration); `EventsEndpoint.PatchEvent`, `ObjectivesEndpoint.PatchObjective`, `EventGamesEndpoint.PatchEventGame`, `SiteThemeEndpoint.UpdateTheme`, `EventRulesEndpoint.UpdateRules`, `LegalDocumentsEndpoint.UpdateDocument`, `CalendarEntriesEndpoint.Update`
- **Problem:** No entity in the model declares `IsRowVersion()`, `IsConcurrencyToken()`
  or an `xmin` mapping. A repository-wide grep confirms it. Every `PATCH`/`PUT` is
  read-modify-write with last-write-wins.
- **Failure scenario:** Two admins open the same event's settings. A changes the name; B
  changes `TieBreakMode` from `ByTime` to `SharedPlace`. B saves, then A saves. A's
  `PatchEvent` only sets `Name` (the others are `null` in the patch and skipped), so
  `TieBreakMode` survives — that case is fine. But `SiteThemeEndpoint.UpdateTheme` is a
  full `PUT` that writes all sixteen palette fields unconditionally; the second writer
  silently reverts the first writer's entire theme. `EventRulesEndpoint.UpdateRules` and
  `LegalDocumentsEndpoint.UpdateDocument` are the same shape with 64 KiB markdown
  documents: two admins editing the rules concurrently, and one's work vanishes with no
  error and no indication in the response. The audit log records both writes, so the
  loss is *recoverable* — which is the only reason this is P2 and not P1.
- **Why:** The `PUT` endpoints that replace an entire document are exactly where lost
  updates hurt most, and they are the ones with no protection at all. Postgres exposes a
  free concurrency token (`xmin`), so the cost of fixing this is a mapping line per
  entity and a `catch`.
- **Change:**
  1. Map Postgres's system column as the token. Npgsql supports this natively; in each
     entity's configuration block in `OnModelCreating` add:

     ```csharp
     e.UseXminAsConcurrencyToken();
     ```

     Apply it to: `Event`, `Objective`, `EventGame`, `SiteTheme`, `EventRules`,
     `LegalDocument`, `CalendarEntry`, `PlannedRun`, `EventGameCompetitorInfo`. Do
     **not** apply it to append-mostly or single-flag tables where it buys nothing:
     `AuditLog` (insert-only), `CompletedObjective` / `FailedObjective` (guarded by the
     partial unique indexes and the advisory lock), `RefreshToken` (BE-004 gives it a
     conditional update instead), `ApiKey`, `EventCompetitor`, `EventCompetitorModerator`.
  2. Surface the token. Add a `Version` (or `ETag`) field to the response records for
     the `PUT`-shaped endpoints — `SiteThemeResponse`, `EventRulesResponse`,
     `LegalDocumentResponse`, `CalendarEntryResponse` — and accept it back on the request.
     For a first cut, a simpler and fully acceptable alternative is the HTTP-native form:
     emit `ETag` on `GET` and require `If-Match` on `PUT`. Pick one; do not do both.
     Prefer `If-Match` for the three document endpoints since they are already
     REST-shaped, and note the choice in `docs/api-reference.md`.
  3. Translate the failure. Wrap each save in

     ```csharp
     catch (DbUpdateConcurrencyException)
     {
         return Results.Problem(
             detail: "This record was modified by someone else. Reload and reapply your change.",
             statusCode: StatusCodes.Status409Conflict);
     }
     ```

     and declare `.ProducesProblem(StatusCodes.Status409Conflict)` on each mapping.
  4. `xmin` changes on **every** update to a row, including ones this application did
     not make. That is correct behaviour, but it means a client holding a stale token
     after an unrelated write gets a 409. Document that in the API reference.
- **Database requirements:**
  - Affected entities/tables: the nine listed above.
  - Required change: none to the schema — `xmin` is a Postgres system column that already exists on every table. `UseXminAsConcurrencyToken()` only adds a shadow property to the EF model.
  - Migration: `dotnet ef migrations add AddXminConcurrencyTokens` will still be generated (EF records the model change) but the `Up`/`Down` bodies should be empty or contain only an annotation change. **Inspect the generated migration and confirm it contains no `AlterColumn`/`AddColumn`.** If EF tries to add a real column, the mapping is wrong — fix the mapping, do not accept the column.
  - Concurrency strategy: optimistic, via `xmin`, translated to HTTP 409.
  - Expected query behaviour: `UPDATE … WHERE "Id" = @id AND xmin = @xmin`; zero rows affected raises `DbUpdateConcurrencyException`.
- **API requirements:**
  - Endpoints: the seven update handlers listed in **Location**.
  - Request: `If-Match` header (or a `version` body field) carrying the token last read.
  - Response: 409 `ProblemDetails` when the token is stale; 428 `Precondition Required` is **not** used — a missing `If-Match` is accepted and behaves as today, so the change is non-breaking.
  - Compatibility: additive and opt-in. Existing clients that send no `If-Match` keep last-write-wins semantics; the frontend is updated to send it in the same PR for the theme, rules and legal editors, which is where the loss actually hurts.
- **Acceptance criteria:**
  - `PUT /api/v1/theme` with a stale `If-Match` returns 409 and writes nothing.
  - The same call with a current `If-Match` returns 200.
  - The same call with no `If-Match` behaves exactly as it does today.
  - The generated migration contains no column additions.
  - `docs/database-design.md` documents `xmin` as the concurrency token and lists which entities carry it and which deliberately do not, with the reason.
- **Validation:** New `tests/Soulsjwa.IntegrationTests/OptimisticConcurrencyTests.cs`
  (real Postgres — `xmin` does not exist in the InMemory provider) reading a theme,
  updating it out of band, then replaying the stale `If-Match` and asserting 409 plus an
  unchanged row. Add the no-`If-Match` compatibility case. Existing
  `SiteThemeEndpointTests` / `EventRulesEndpointTests` / `LegalDocumentsEndpointTests`
  must pass unchanged.

---

### [BE-015] Add retention for `RefreshTokens` and `AuditLogs`

- **Priority:** P2
- **Axis:** Reliability
- **Location:** `src/Soulsjwa.Api/Program.cs` (no hosted service is registered anywhere); `src/Soulsjwa.Api/Infrastructure/Auth/JwtTokenService.cs`; `src/Soulsjwa.Api/Features/Audits/Entities/AuditLog.cs`
- **Problem:** A grep for `BackgroundService`, `IHostedService` and `AddHostedService`
  across `src/` returns nothing. There is no cleanup of any kind.
  - `RefreshTokens` gains one row per refresh. Access tokens expire in 15 minutes, so an
    active session inserts ~4 rows/hour; revoked and expired rows are never deleted.
    Every `ValidateRefreshTokenAsync` and `RevokeRefreshTokenAsync` queries
    `WHERE "TokenPrefix" = @p AND "TokenHash" = @h` against a table that only grows —
    `IX_RefreshTokens_TokenPrefix` is non-unique and its selectivity degrades as dead
    rows accumulate under the same prefixes.
  - `AuditLogs` is append-only by design and holds `BeforeJson`/`AfterJson` payloads that
    for `EventRulesUpdated` and `LegalDocumentUpdated` contain the full 64 KiB document
    twice per edit (see BE-036). It carries seven indexes, so every insert writes seven
    index entries.
- **Failure scenario:** Nothing breaks suddenly; the database gets steadily slower and
  larger, refresh latency creeps up, and the audit page's OFFSET pagination (BE-026)
  degrades in step. A year of modest use is on the order of hundreds of thousands of
  dead refresh-token rows for a handful of real sessions.
- **Why:** Unbounded growth in an authentication hot-path table is the classic slow
  production failure. It is cheap to prevent and expensive to remediate after the fact.
- **Change:**
  1. Add `src/Soulsjwa.Api/Infrastructure/Data/RetentionService.cs` — a
     `BackgroundService` that wakes on a `PeriodicTimer` (interval from
     `Retention:IntervalHours`, default 24) and, in its own DI scope:
     - deletes `RefreshTokens` where `ExpiresAt < now - Retention:RefreshTokenGraceDays` (default 7). Keeping a grace window past expiry preserves reuse detection for recently-expired tokens — do **not** delete on `IsRevoked` alone, because a revoked-but-unexpired row is exactly what `ValidateRefreshTokenAsync` needs to detect theft;
     - deletes `AuditLogs` where `CreatedAt < now - Retention:AuditRetentionDays`, default **null meaning never delete**. Audit retention is a policy decision the operator makes, not a default the code imposes; ship it disabled and document the setting.
     Use `ExecuteDeleteAsync` in bounded batches (`Take(BatchSize)` in a loop, default
     5000) so a first run against a large table does not hold one long transaction.
  2. Register it: `builder.Services.AddHostedService<RetentionService>();` Guard the whole
     loop so an exception logs and the service keeps running rather than taking the host
     down — a failed cleanup pass must never stop the API.
  3. Make it safe under multiple instances. Take a Postgres advisory lock for the
     duration of a pass, reusing the existing idiom from
     `src/Soulsjwa.Api/Features/Events/ObjectiveOutcomeLock.cs` — `pg_try_advisory_lock`
     (the `try` variant, so a second instance skips the pass instead of blocking).
  4. Add the supporting index — see below.
  5. Log a structured summary per pass (rows deleted per table, duration) through the
     existing `ApiLoggerMessages` source-generated pattern, not a raw `logger.LogInformation`.
- **Database requirements:**
  - Affected entities/tables: `RefreshToken` / `RefreshTokens`, `AuditLog` / `AuditLogs`.
  - Relevant queries: `DELETE FROM "RefreshTokens" WHERE "ExpiresAt" < @cutoff` and `DELETE FROM "AuditLogs" WHERE "CreatedAt" < @cutoff`, both batched.
  - Required index: `RefreshTokens` has no index on `ExpiresAt`, so the delete is a sequential scan. Add `IX_RefreshTokens_ExpiresAt` on `("ExpiresAt")`. `AuditLogs` already has `IX_AuditLogs_CreatedAt` — reuse it, add nothing.
  - Migration: `dotnet ef migrations add AddRefreshTokenExpiresAtIndex`. Index-only.
  - Transaction requirement: one transaction per batch, not per pass.
  - Concurrency strategy: `pg_try_advisory_lock` so only one instance runs a pass.
  - Expected behaviour: steady-state `RefreshTokens` row count proportional to active sessions rather than to lifetime refreshes.
- **Acceptance criteria:**
  - A `RefreshToken` expired longer ago than the grace window is deleted on the next pass.
  - A revoked but **not** yet expired token survives, so reuse detection still fires for it.
  - `AuditLogs` is untouched when `Retention:AuditRetentionDays` is unset.
  - A pass that throws is logged and the service survives to the next tick.
  - Two instances running concurrently perform one pass between them, not two.
  - `.env.example` and `docs/deployment.md` document all four `Retention:*` settings and state that audit retention defaults to "keep forever".
- **Validation:** New `tests/Soulsjwa.IntegrationTests/RetentionServiceTests.cs` seeding
  expired, recently-expired, revoked-unexpired and active tokens and asserting exactly
  the first group is removed. Add a case asserting audit rows survive with the default
  configuration. `Depends on: BE-004` — land the rotation fix first so the retention
  window is reasoned about against the corrected token lifecycle.

---

### [BE-016] Parse the connector submission once per request

- **Priority:** P2
- **Axis:** Performance
- **Location:** `src/Soulsjwa.Api/Features/Games/Services/RuleEvaluator.cs:Evaluate`; `src/Soulsjwa.Api/Features/Connector/Endpoints/ConnectorEndpoint.cs:SubmitGameData`; `src/Soulsjwa.Api/Features/Connector/ConnectorSubmissionValidator.cs:Validate`; `src/Soulsjwa.Api/Features/Connector/ConnectorIngameTime.cs:ExtractMilliseconds`
- **Problem:** One submission parses its JSON payload `2 + N` times, where N is the
  number of pending objectives with a rule:
  - `ConnectorSubmissionValidator.Validate` → `JsonDocument.Parse` (System.Text.Json);
  - `ConnectorIngameTime.ExtractMilliseconds` → `JsonDocument.Parse` again;
  - then, inside the `foreach (var objective in pendingObjectives)` loop,
    `RuleEvaluator.Evaluate(objective.Rule, submission.Data)` does
    `JObject.Parse(ruleJson)` **and** `JObject.Parse(dataJson)` — Newtonsoft this time —
    on every iteration, and again for `objective.FailRule`.

  So for an event-game with 180 objectives carrying rules, a single submission performs
  ~362 full JSON parses of the same payload, allocating a fresh `JObject` tree each time,
  all while holding the `pg_advisory_xact_lock` taken by `ObjectiveOutcomeLock` and an
  open transaction.
- **Failure scenario:** Twelve competitors each submitting at the connector policy's
  cadence against a 180-objective game. Every submission holds the per-event-game
  advisory lock for the duration of ~362 parses, so submissions serialise behind each
  other while each one burns CPU re-parsing identical bytes. Lock hold time is directly
  proportional to objective count, and objective count is the thing event owners are
  encouraged to grow (`import-predefined` imports the whole catalog). This is the path
  that turns a busy event into a queue.
- **Why:** It is pure waste on the highest-frequency write path, it inflates the hold
  time of a lock that serialises other writers, and the fix is mechanical.
- **Change:**
  1. Add parsed-input overloads to `RuleEvaluator` and make the string overloads thin
     wrappers so existing call sites and tests keep working:

     ```csharp
     public static bool Evaluate(JObject rule, JObject data, int? competitorCompletions);
     ```

     The existing `Evaluate(string, string, int?)` parses and delegates. Keep the same
     exception handling (`JsonException` / `InvalidOperationException` /
     `ArgumentException` → `false`) and the same `DiagnosticsConfig` business-operation
     span in the parsed overload, so telemetry is unchanged.
  2. In `SubmitGameData`, parse `submission.Data` once into a `JObject` before the loop
     and pass it in. Note the `competitorCompletions` injection currently **mutates** the
     data object (`data[CompetitorCompletionsVariable] = value`) — with a shared instance
     that mutation would leak across iterations. Either clone per evaluation
     (`(JObject)data.DeepClone()` only when `competitorCompletions.HasValue`, which is
     only for fail rules) or, better, set-then-remove around the single `Apply` call.
     Cloning per fail-rule evaluation is still O(N) clones but of a small object, and it
     is the safer default — prefer it and measure before optimising further.
  3. Cache the parsed **rule** too. `objective.Rule` is a stable string per objective;
     parsing it per submission is the other half of the waste. A
     `ConcurrentDictionary<Guid, JObject>` keyed by objective id with the rule string's
     hash as a validity check is tempting but is a cache-invalidation problem —
     `PatchObjective` can change a rule at any time. Simpler and sufficient: parse each
     objective's rule once per **request** (they are already materialised into
     `pendingObjectives` before the loop), not once per objective per rule *and* fail
     rule. Do not introduce a process-wide rule cache in this task.
  4. Have `ConnectorSubmissionValidator.Validate` return the parsed document (or accept a
     pre-parsed one) so `ExtractMilliseconds` reuses it instead of re-parsing. Watch the
     `using (document)` lifetime — `JsonDocument` is disposable and currently disposed
     inside `Validate`; hoist ownership to the caller.
  5. The two JSON stacks (System.Text.Json for validation, Newtonsoft for JsonLogic.Net)
     stay. Unifying them means replacing JsonLogic.Net — see Deferred.
- **Acceptance criteria:**
  - A submission against an event-game with N rule-bearing objectives parses the payload exactly once.
  - Each objective's `Rule` and `FailRule` strings are parsed at most once per request.
  - `competitorCompletions` injected for one objective's fail rule is not visible to the next objective's evaluation.
  - Results are byte-identical to the current implementation for every fixture in `tests/Soulsjwa.UnitTests/RuleEvaluatorEdgeCaseTests.cs`.
  - The `DiagnosticsConfig` span for `RuleEvaluate` is still emitted once per evaluation.
- **Validation:** `RuleEvaluatorEdgeCaseTests` must pass unchanged against the new
  overloads. Add a test asserting the cross-iteration isolation of
  `competitorCompletions` (two fail-rule objectives, different counts). Add a parse-count
  assertion — inject a counting wrapper or assert via a benchmark in
  `tests/Soulsjwa.ApiTests/ConnectorEndpointTests.cs` that a 50-objective submission does
  not scale parse count with objective count.

---

### [BE-017] Give the Twitch HTTP client a timeout, retries and a circuit breaker

- **Priority:** P2
- **Axis:** Reliability
- **Location:** `src/Soulsjwa.Api/Program.cs` (`builder.Services.AddHttpClient<TwitchAuthService>();`); `src/Soulsjwa.Api/Infrastructure/Auth/TwitchAuthService.cs`
- **Problem:** The registration is bare:

  ```csharp
  builder.Services.AddHttpClient<TwitchAuthService>();
  ```

  No `Timeout`, no resilience handler, no retry, no circuit breaker. `HttpClient`'s
  default timeout is 100 seconds. `TwitchAuthService` makes two outbound calls per login
  — `ExchangeCodeAsync` to `id.twitch.tv/oauth2/token` and `GetUserInfoAsync` to
  `api.twitch.tv/helix/users` — and both `await` inside the request path of
  `TwitchCallbackEndpoint.Handle`.

  Additionally, both methods read the whole response with `ReadAsStringAsync(ct)` and
  `GetUserInfoAsync` calls `JsonDocument.Parse` on it with no size bound, then
  `doc.RootElement.GetProperty("data")` — an unguarded `GetProperty` that throws
  `KeyNotFoundException` if Twitch returns a success status with an unexpected body.
  That is an uncaught 500 driven by a third party's response shape.
- **Failure scenario:** Twitch has a partial outage and `id.twitch.tv` starts hanging
  rather than erroring. Each login attempt occupies a request thread and a connection for
  up to 100 seconds, and the `"auth"` rate-limit policy (see BE-001) does nothing to shed
  them because they are slow, not numerous. Users retry; the pile grows; the thread pool
  starves and endpoints unrelated to auth start timing out. There is no circuit breaker
  to stop hammering a dependency that is already failing, and no fast failure to hand the
  user.
- **Why:** Twitch is the only external dependency in the system and the entire
  authentication story depends on it. "What happens when every dependency is slow?"
  currently answers "the whole API degrades."
- **Change:**
  1. Add the standard resilience pipeline:

     ```csharp
     builder.Services.AddHttpClient<TwitchAuthService>(client =>
     {
         client.Timeout = TimeSpan.FromSeconds(TwitchClientTimeoutSeconds); // 10
     })
     .AddStandardResilienceHandler(options =>
     {
         options.Retry.MaxRetryAttempts = TwitchRetryAttempts;      // 2
         options.Retry.UseJitter = true;                            // exponential + jitter
         options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(TwitchAttemptTimeoutSeconds); // 4
         options.CircuitBreaker.SamplingDuration = …;
     });
     ```

     This requires the `Microsoft.Extensions.Http.Resilience` package — add it to
     `Soulsjwa.Api.csproj` and regenerate `THIRD-PARTY-NOTICES.md` via
     `tools/generate_third_party_notices.sh` in the same PR (`AGENTS.md` §6; CI fails if
     it is stale). All numeric values go in named constants per `AGENTS.md` §4, ideally
     bound from configuration so an operator can tune them.
  2. **Retry only the safe call.** `ExchangeCodeAsync` posts a single-use OAuth
     authorization `code`; retrying it after a response was actually produced but lost
     will fail with `invalid_grant` and, worse, masks the real error. Configure the
     handler to retry only on timeouts and 5xx/429 — the standard handler's default
     predicate already does this — and confirm `POST` is included in what it retries. If
     in doubt, disable retry for the token exchange specifically and keep it for
     `GetUserInfoAsync`, which is an idempotent `GET`. State which you chose and why in
     the PR.
  3. Harden the response parsing in `GetUserInfoAsync`:
     - bound the body (`ReadAsStringAsync` on an unbounded stream → read with a cap, or check `Content.Headers.ContentLength`);
     - replace `doc.RootElement.GetProperty("data")` with `TryGetProperty` and return `null` (the method is already `Task<TwitchUserInfo?>` and the caller already handles null with a clean `Results.Problem`);
     - wrap the parse in `catch (JsonException)` → `null`.
  4. Improve the caller's failure response. `TwitchCallbackEndpoint` currently returns
     `Results.Problem("Failed to exchange authorization code")` — a bare 500 with the
     user stranded on an API URL. Redirect to
     `{frontendUrl}/auth/callback?error=twitch_unavailable` instead, matching the
     existing `not_allowlisted` pattern in the same handler, so the SPA can show a real
     message and a retry button.
- **Acceptance criteria:**
  - A Twitch endpoint that never responds fails the login attempt within `TwitchClientTimeoutSeconds`, not 100 seconds.
  - Repeated failures open the circuit breaker; subsequent attempts fail fast without an outbound call.
  - The OAuth `code` is not replayed against `id.twitch.tv/oauth2/token` after a non-retryable failure.
  - A 200 response with an unexpected JSON body yields a clean redirect with `error=`, not a 500.
  - `THIRD-PARTY-NOTICES.md` is regenerated in the same commit.
  - `docs/system-overview.md` documents the timeout/retry/breaker settings as part of the auth flow.
- **Validation:** Extend `tests/Soulsjwa.UnitTests/TwitchAuthServiceTests.cs` with a
  stubbed `HttpMessageHandler` covering: a hanging response (asserts the timeout), a
  malformed 200 body (asserts `null`, not a throw), a body missing `data` (asserts
  `null`), and a 500 followed by a 200 (asserts the retry succeeded for `GetUserInfoAsync`).

---

### [BE-018] Decide and enforce who can read an archived event

- **Priority:** P2
- **Axis:** Security
- **Location:** `src/Soulsjwa.Api/Features/Events/Endpoints/EventsEndpoint.cs:GetEvent` and `:ListEvents`
- **Problem:** `Event` carries a soft-delete query filter on `IsArchived`, but the two
  public read endpoints opt out of it:

  ```csharp
  // GetEvent — unconditionally, for every caller including anonymous
  var ev = await db.Events.IgnoreQueryFilters()…FirstOrDefaultAsync(…);

  // ListEvents — driven by two anonymous query parameters
  var shouldIncludeArchived = includeArchived || statusFilter == StatusArchived;
  var baseQuery = shouldIncludeArchived ? db.Events.IgnoreQueryFilters() : db.Events.AsQueryable();
  ```

  Both are `.AllowAnonymous()`. So `GET /api/v1/events/{archivedId}` returns the full
  event including competitors, games and every objective; and
  `GET /api/v1/events?includeArchived=true` (or `?status=archived`) enumerates every
  archived event to any anonymous caller. Archiving hides an event from the *default*
  list and from nothing else.
- **Failure scenario:** An owner archives an event that was set up in error, or one
  containing competitor handles they would rather not surface. It stays fully readable
  at a stable URL and is trivially enumerable. Nobody is told this.
- **Why:** The rest of the codebase treats `IsArchived` as soft delete — `TrialRunsEndpoint`
  refuses to enable trials on archived events, `GlobalCalendarEndpoint`'s doc comment
  relies on the filter to exclude them, and BE-003 is about archived events not being
  writable. The read path contradicts all of it. This is more a *specification* gap than
  an exploit: it may well be intended that archived events stay publicly linkable. The
  task is to decide, then make the code and the docs agree.
- **Change:**
  1. **Decide first, in the PR description.** Two defensible options:
     - **(a) Archived events are public history.** Then the current behaviour is correct and the task reduces to documenting it in `docs/api-reference.md` and `docs/auth.md`, and adding a test that pins it so nobody "fixes" it later.
     - **(b) Archived events are visible only to members.** Then `GetEvent` returns 404 for anonymous and non-member callers, and `includeArchived` / `status=archived` are honoured only for authenticated callers, filtered to events the caller owns, competes in, or moderates.

     Check `docs/feature-matrix.md` and `docs/flows/event-management.md` for a stated
     intent before choosing. If neither says, choose **(b)** — soft delete that does not
     hide anything is a surprising default, and (b) is the reversible direction.
  2. If (b): `GetEvent` keeps `IgnoreQueryFilters()` but, when `ev.IsArchived`, calls
     `EventOwnership.IsEventMemberAsync(ev, principal, db, ct)` and returns the same
     404 `"Event not found."` as a genuinely missing event when that is false. Return
     404, not 403 — a 403 confirms the id exists.
  3. If (b): in `ListEvents`, ignore `includeArchived` and `status=archived` for
     anonymous callers (return only non-archived), and for authenticated non-admin
     callers restrict archived rows to ones where
     `e.CreatedById == userId || e.Competitors.Any(c => c.UserId == userId) || e.Competitors.Any(c => c.Moderators.Any(m => m.ModeratorUserId == userId))`.
     Admins see all. `ListEvents` is currently a static method with no `ClaimsPrincipal`
     parameter — add one; the endpoint stays `.AllowAnonymous()`, and the principal is
     simply unauthenticated for public callers.
  4. Either way, add the decision as a comment at both `IgnoreQueryFilters()` call sites
     explaining *why* the filter is bypassed, matching the style of the existing comment
     in `UnarchiveEvent`.
- **API requirements:**
  - Endpoints: `GET /api/v1/events/{identifier}`, `GET /api/v1/events`.
  - Under (b): `GET /api/v1/events/{archivedId}` returns 404 for anonymous and non-members, 200 for members and admins. `?includeArchived=true` silently returns only the caller's own archived events rather than erroring.
  - Compatibility: under (b) this is a **breaking read change** for any client relying on public archived reads. The frontend's archived-events view must pass credentials — check `src/Soulsjwa.Web/src/features/events/api/` and update in the same PR.
- **Acceptance criteria (option b):**
  - Anonymous `GET /api/v1/events/{archivedId}` → 404, with a body identical to a nonexistent id.
  - A competitor of that event → 200.
  - Anonymous `GET /api/v1/events?includeArchived=true` returns zero archived events.
  - An admin still sees all archived events in both endpoints.
  - Unarchive (`POST /api/v1/events/{id}/unarchive`) still works — it uses its own `IgnoreQueryFilters()` path and must not regress.
  - `docs/auth.md` states the archived-event visibility rule explicitly.
- **Validation:** Extend `tests/Soulsjwa.ApiTests/EventsEndpointTests.cs` and
  `PermissionsEndpointTests.cs` with the anonymous/member/admin matrix for both
  endpoints. Whichever option is chosen, the tests must pin it so the behaviour stops
  being accidental.

---

### [BE-019] Make the events list query bounded and indexable

- **Priority:** P2
- **Axis:** Performance
- **Location:** `src/Soulsjwa.Api/Features/Events/Endpoints/EventsEndpoint.cs:ListEvents`
- **Problem:** Two separate costs on a public, anonymous, paginated endpoint:
  1. **The search predicate is non-sargable.**

     ```csharp
     var searchText = (search ?? string.Empty).Trim().ToLowerInvariant();
     query = query.Where(e => e.Name.ToLower().Contains(searchText) ||
                              e.Description.ToLower().Contains(searchText));
     ```

     This translates to `LOWER("Name") LIKE '%…%' OR LOWER("Description") LIKE '%…%'`.
     A leading wildcard defeats a B-tree index, and `LOWER(col)` defeats one on the raw
     column, so this is always a sequential scan over `Events` — executed twice, because
     `CountAsync` runs the same predicate before the page query.
  2. **The page materialises the full object graph.** The query carries four `Include`
     chains — competitors + their users, competitors + their moderators + those
     moderators' users, event games + known game, event games + objectives — with
     `AsSplitQuery()`. For `pageSize = 20` (max 100) that is five round trips returning
     every objective of every game of every event on the page, all change-tracked, so
     `MapToResponse` can emit them. A list view that shows event names and competitor
     counts is paying for the entire detail payload of twenty events.
- **Failure scenario:** The public events page with a search box. Each keystroke-driven
  request sequentially scans `Events` twice and then pulls, for 20 events × 1 game × 180
  objectives, 3,600 objective rows — to render a list. At 100 events in the table this is
  merely wasteful; the objective fan-out is what makes it expensive, and it grows with
  event richness rather than event count.
- **Why:** It is the landing-page query. It is anonymous, so it is reachable at the
  global 60/min-per-IP budget by anyone, and it is uncached.
- **Change:**
  1. Project instead of including. Add a `EventListItemResponse` containing only what a
     list renders: id, name, alias, description (or an excerpt), `CreatedById`, the three
     flags, `TieBreakMode`, timestamps, `CompetitorCount`, `GameCount`. Build it with a
     single `Select` so EF emits one query with two scalar sub-selects and no fan-out.
     Keep the existing full `EventResponse` for `GetEvent` — the detail endpoint needs it.
  2. Make search indexable. Use `EF.Functions.ILike(e.Name, $"%{searchText}%")` rather
     than `LOWER(...).Contains(...)` so the intent is explicit, then add a trigram index
     so it can actually be served:

     ```sql
     CREATE EXTENSION IF NOT EXISTS pg_trgm;
     CREATE INDEX "IX_Events_Name_Trgm" ON "Events" USING gin ("Name" gin_trgm_ops);
     CREATE INDEX "IX_Events_Description_Trgm" ON "Events" USING gin ("Description" gin_trgm_ops);
     ```

     `pg_trgm` is the standard answer for `ILIKE '%x%'` in Postgres and turns both scans
     into bitmap index scans. If the deployment cannot install the extension, fall back
     to requiring a minimum search length (3 characters, matching
     `SearchUsersEndpoint.MinQueryLength`) and accept the scan — say which path was taken.
  3. Add `IX_Events_CreatedAt` on `("CreatedAt" DESC)` to serve the
     `OrderByDescending(e => e.CreatedAt)` + `Skip`/`Take` without a sort.
  4. `AsNoTracking()` on the list query — nothing on this path is mutated.
  5. Keep `CountAsync`, but only compute it on page 1 or when explicitly requested; on
     later pages reuse the client's known total. If that complicates the
     `PaginatedResponse` contract more than it is worth, leave the count and note the
     cost — the projection and index changes are the substantive wins.
- **Database requirements:**
  - Affected entity/table: `Event` / `Events`. Columns `Name`, `Description`, `CreatedAt`.
  - Relevant query: `SELECT … FROM "Events" WHERE ("Name" ILIKE @p OR "Description" ILIKE @p) ORDER BY "CreatedAt" DESC OFFSET @o LIMIT @l`.
  - Required indexes: GIN trigram on `Name` and `Description`; B-tree on `CreatedAt DESC`.
  - Migration: `dotnet ef migrations add AddEventSearchIndexes`, containing the `CREATE EXTENSION` and both `CREATE INDEX` statements via `migrationBuilder.Sql(...)` (EF cannot express `gin_trgm_ops` declaratively). `CREATE EXTENSION` requires superuser or the extension being pre-allowlisted — check the deployment's Postgres role first and, if it is not available, fall back to step 2's minimum-length option rather than shipping a migration that fails.
  - Transaction requirement: none (read-only).
  - Expected query behaviour: `EXPLAIN ANALYZE` shows `Bitmap Index Scan` on the trigram indexes for a search, and `Index Scan Backward` on `IX_Events_CreatedAt` for the unfiltered page — not `Seq Scan` + `Sort`.
- **API requirements:**
  - Endpoint: `GET /api/v1/events`.
  - Response: items change from `EventResponse` to the slimmer `EventListItemResponse`. This is **breaking** for any client reading `competitors[]` or `games[]` off the list — `src/Soulsjwa.Web/src/features/events/` must be updated in the same PR to fetch detail from `GET /api/v1/events/{id}` where it needs the graph.
  - Status codes unchanged.
- **Acceptance criteria:**
  - The list query issues one SQL statement (plus the count), not five.
  - No `Objective` row is loaded when listing events.
  - `EXPLAIN ANALYZE` for a search shows no `Seq Scan` on `Events` (or, on the fallback path, the minimum-length rule is enforced and documented).
  - Pagination, the `status` filter and the search filter return the same event ids as before the change for a fixed fixture.
- **Validation:** Extend `tests/Soulsjwa.ApiTests/EventsEndpointTests.cs` with a
  before/after id-equivalence test across every `status` value and a search term, plus a
  command-count assertion. Add the `EXPLAIN` check in `tests/Soulsjwa.IntegrationTests/`.
  `Depends on: BE-018` — settle archived visibility first, since it changes this query's
  `WHERE` clause.

---

### [BE-020] Stop the SPA fallback from swallowing unmatched API routes

- **Priority:** P2
- **Axis:** Maintainability
- **Location:** `src/Soulsjwa.Api/Program.cs` — `app.MapFallbackToFile("index.html");`
- **Problem:** The fallback matches every request that no endpoint matched, including
  `/api/v1/anything-misspelled`. A typo'd or removed API route returns **200 OK** with
  `text/html` — the React shell — instead of a 404 `application/problem+json`.
- **Failure scenario:** A client calls a renamed endpoint. Instead of a 404 it gets 200
  and an HTML body; its JSON deserialiser throws something unrelated to the real
  problem, or worse, a lenient client treats 200 as success. The same masks typos in
  tests: an integration test asserting "not 500" passes against a route that does not
  exist. It also means the API surface has no honest 404 at all.
- **Why:** Cheap to fix, and it removes a whole class of confusing failures for the
  connector and any third-party client.
- **Change:**
  1. Before the fallback, map a terminal 404 for the API prefix:

     ```csharp
     // Any /api/* request that reached routing without matching an endpoint is a
     // genuine 404 — it must not fall through to the SPA shell below.
     app.Map($"/{ApiRoutePrefix}/{{**catchAll}}", () => Results.Problem(
         detail: "Endpoint not found.",
         statusCode: StatusCodes.Status404NotFound));

     app.MapFallbackToFile("index.html");
     ```

     Route specificity puts the explicit `/api/...` catch-all ahead of the fallback.
     Verify the ordering with a test rather than assuming it.
  2. Per `AGENTS.md` §4, introduce `ApiRoutePrefix = "api/v1"` (or `"api"`) as a named
     constant. Every endpoint currently hardcodes `/api/v1/...` in its `MapGroup` /
     `Map*` calls — that is 20-plus literals of the same string. Centralising all of them
     is a larger change than this task needs; introduce the constant, use it for the
     catch-all, and leave the rest to XC-2.
  3. Confirm `/health`, `/health/live`, `/health/ready` and the Swagger routes are
     unaffected — they are outside the prefix.
- **API requirements:**
  - Endpoint: any unmatched path under `/api/`.
  - Response: 404 `application/problem+json` with `detail: "Endpoint not found."`, matching the shape every other 404 in the codebase returns.
  - Compatibility: a client that was receiving 200+HTML for a bad path now receives 404. That is the fix, not a regression.
- **Acceptance criteria:**
  - `GET /api/v1/does-not-exist` → 404, `content-type: application/problem+json`.
  - `GET /api/v1/events` → 200 JSON (unchanged).
  - `GET /some/spa/route` → 200 `text/html` (unchanged).
  - `GET /health/ready` → unchanged.
  - A wrong HTTP verb on a real route (e.g. `DELETE /api/v1/events`) still returns 405, not 404 — confirm the catch-all does not shadow method mismatches.
- **Validation:** New `tests/Soulsjwa.ApiTests/ApiNotFoundTests.cs` covering all five
  cases above.

---

### [BE-021] Validate the inbound `X-Correlation-Id`

- **Priority:** P2
- **Axis:** Security
- **Location:** `src/Soulsjwa.Api/Middleware/CorrelationIdMiddleware.cs:GetCorrelationId` / `InvokeAsync`
- **Problem:** The middleware takes a client-supplied header value verbatim and both
  echoes it into the response and pushes it into every log event for the request:

  ```csharp
  if (context.Request.Headers.TryGetValue(HeaderName, out var values)
      && !string.IsNullOrWhiteSpace(values.FirstOrDefault()))
      return values.First()!;
  ```

  No length bound, no character validation. The value is written to
  `context.Response.Headers[HeaderName]`, to `context.Items`, into a Serilog
  `LogContext` property, and into the `EnrichDiagnosticContext` callback in `Program.cs`.
- **Failure scenario:** The realistic harms are bounded but real. Kestrel rejects CR/LF
  in outgoing header values, so response splitting is not available — but a rejected
  header value surfaces as an unhandled exception mid-response, i.e. a 500 an anonymous
  caller can trigger at will. A multi-kilobyte correlation id is copied into every log
  event for the request, multiplying log volume and storage cost at the attacker's
  discretion. And downstream log consumers that parse or index the field receive
  arbitrary attacker-chosen content — the compact JSON formatter escapes it correctly,
  so this is a cost and hygiene problem rather than log forging, but it is still
  attacker-controlled data flowing unchecked into observability infrastructure.
- **Why:** A correlation id is a diagnostic convenience. Accepting an unbounded arbitrary
  string for it has no upside, and the fix is six lines.
- **Change:**
  1. Add a named `MaxCorrelationIdLength = 128` and accept the client's value only if it
     matches a conservative pattern — use a `[GeneratedRegex]` partial method, as
     `EventsEndpoint.UrlAliasRegex` already does:

     ```csharp
     [GeneratedRegex("^[A-Za-z0-9_.:-]{1,128}$", RegexOptions.CultureInvariant)]
     private static partial Regex CorrelationIdRegex();
     ```

     That covers UUIDs, W3C trace ids, and typical gateway-generated ids. Anything else
     is discarded and a fresh id generated — do not reject the request; a malformed
     diagnostic header is not worth a 400.
  2. Make `CorrelationIdMiddleware` a `partial class` for the generated regex.
  3. Log at `Debug` when a supplied id is rejected, through the existing
     `ApiLoggerMessages` source-generated pattern, so the case is observable without
     being noisy.
  4. While here: `context.Items[HeaderName]` uses the literal header name as the key and
     `Program.cs` reads it back with the same literal
     (`httpContext.Items["X-Correlation-Id"]`). That is a magic string duplicated across
     two files — `AGENTS.md` §4. Expose `CorrelationIdMiddleware.HeaderName` as `public
     const` and use it in `Program.cs`.
- **Acceptance criteria:**
  - A request with `X-Correlation-Id: abc-123` echoes `abc-123`.
  - A request with a 10 KB value gets a freshly generated id in the response, and no log event contains the supplied value.
  - A request with `X-Correlation-Id: a\r\nInjected: 1` gets a generated id and the response contains no `Injected` header and no 500.
  - A request with no header still gets the current-activity trace id or a new GUID.
  - No literal `"X-Correlation-Id"` remains in `Program.cs`.
- **Validation:** New `tests/Soulsjwa.ApiTests/CorrelationIdMiddlewareTests.cs` covering
  the four cases. Assert against an in-memory Serilog sink that the oversized value never
  appears in any emitted event.

---

### [BE-022] Validate `Jwt:Secret` strength at startup

- **Priority:** P2
- **Axis:** Security
- **Location:** `src/Soulsjwa.Api/Program.cs` (the `_ = builder.Configuration["Jwt:Secret"] ?? throw …` guard); `src/Soulsjwa.Api/Infrastructure/Auth/JwtTokenService.cs:BuildValidationParameters`
- **Problem:** Startup checks only that the value is **present**:

  ```csharp
  _ = builder.Configuration["Jwt:Secret"]
      ?? throw new InvalidOperationException("Jwt:Secret must be configured. Set the Jwt__Secret environment variable.");
  ```

  Tokens are signed with `HmacSha256` over
  `new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret))`. HMAC-SHA256 wants a key of
  at least 256 bits (32 bytes); the `Microsoft.IdentityModel` stack enforces only a much
  lower floor, so an 8-character secret is accepted and signs tokens happily. Nothing
  warns. Additionally, `Jwt__Secret` is supplied via `docker-compose.yml` as
  `"${JWT_SECRET}"` — if the variable is unset, Compose substitutes an empty string,
  which is non-null, so the presence check passes and the app starts with an empty signing key.
- **Failure scenario:** An operator sets `JWT_SECRET=changeme` — or forgets to set it at
  all, so it expands to `""`. The service starts normally and issues tokens that any
  attacker can forge offline in seconds, including tokens with `"role": "Admin"` for an
  arbitrary `sub`. Because `EventOwnership.IsAdmin` trusts the `role` claim, a forged
  token is full administrative access. There is no runtime signal that anything is wrong.
- **Why:** This turns a deployment mistake into total authentication bypass with no
  diagnostic. Startup validation is the only place it is cheap to catch, and failing fast
  is unambiguously the right behaviour for a signing key.
- **Change:**
  1. Replace the presence check with a real one, next to it in `Program.cs`:

     ```csharp
     const int MinJwtSecretBytes = 32; // 256-bit key for HMAC-SHA256
     var jwtSecret = builder.Configuration["Jwt:Secret"];
     if (string.IsNullOrWhiteSpace(jwtSecret))
         throw new InvalidOperationException("Jwt:Secret must be configured. Set the Jwt__Secret environment variable.");
     if (Encoding.UTF8.GetByteCount(jwtSecret) < MinJwtSecretBytes)
         throw new InvalidOperationException(
             $"Jwt:Secret must be at least {MinJwtSecretBytes} bytes. Generate one with: openssl rand -base64 48");
     ```

     Note `IsNullOrWhiteSpace`, not `is null` — that is what catches the empty-string
     Compose expansion.
  2. Reject known placeholder values outside Development. The committed
     `appsettings.Development.json` contains
     `"dev-secret-change-in-production-32chars!!"`, which is exactly 40 bytes and so
     passes the length check. Add an explicit refusal when
     `!builder.Environment.IsDevelopment()` and the secret matches any entry in a small
     `KnownPlaceholderSecrets` array (that string, plus `"changeme"` and `"secret"`), with
     a message naming the problem.
  3. Do the same for the other credentials that are read with `?? throw` in
     `TwitchAuthService` — `Twitch:ClientId`, `Twitch:ClientSecret`, `Twitch:RedirectUri`.
     Those throw lazily, on first use, which means a misconfigured deployment appears
     healthy (`/health/ready` only checks the database) and fails at the first login
     attempt. Move them to a startup check so misconfiguration is caught at deploy time.
     Keep the lazy `?? throw` as a backstop.
  4. Document the requirement in `.env.example` next to `JWT_SECRET`, with the
     `openssl rand -base64 48` command, and in `docs/deployment.md`.
- **Acceptance criteria:**
  - Starting with `Jwt__Secret` unset fails with the existing message.
  - Starting with `Jwt__Secret=""` (the unset-Compose-variable case) fails with the same message, not a successful boot.
  - Starting with a 16-byte secret fails and the message states the minimum and how to generate one.
  - Starting in Production with the committed development placeholder fails.
  - Starting in Development with that placeholder succeeds — the dev loop must not break.
  - Missing `Twitch:ClientId` / `ClientSecret` / `RedirectUri` fails at startup rather than at first login.
  - `.env.example` and `docs/deployment.md` state the 32-byte minimum.
- **Validation:** New `tests/Soulsjwa.UnitTests/JwtSecretValidationTests.cs` exercising
  the validation helper directly for each case (extract it into a small static method so
  it is testable without booting a host). Add one `WebApplicationFactory`-based case in
  `tests/Soulsjwa.ApiTests/` asserting the host fails to start with a short secret.

---

### [BE-023] Stream media instead of buffering it, and support conditional requests

- **Priority:** P2
- **Axis:** Performance
- **Location:** `src/Soulsjwa.Api/Features/Media/Endpoints/MediaEndpoint.cs:GetMedia`; `src/Soulsjwa.Api/Features/Media/Services/MediaStore.cs:ReadAsync`
- **Problem:** Serving an image reads the whole file into a `byte[]` and hands it to
  `Results.Bytes`:

  ```csharp
  public async Task<byte[]?> ReadAsync(Guid assetId, CancellationToken ct = default)
  {
      var asset = await db.MediaAssets.FindAsync([assetId], ct);
      …
      return File.Exists(path) ? await File.ReadAllBytesAsync(path, ct) : null;
  }
  ```

  Uploads are capped at 5 MiB (`MediaEndpoint.MaxUploadBytes`), so every concurrent
  request for a large asset allocates up to 5 MiB on the large object heap. `GetMedia`
  also performs two database round trips — one `Select` for `ContentType`, then
  `FindAsync` inside `ReadAsync` for the same row.

  The response sets `Cache-Control: public, max-age=31536000, immutable`, which is
  correct for content-addressed storage, but there is no `ETag` and no
  `If-None-Match` handling, so a client that chooses to revalidate (a hard refresh, a
  proxy with its own policy, a CDN) gets the full 5 MiB back instead of a 304.
- **Failure scenario:** The site theme background (`SiteTheme.BackgroundAssetId`) is
  served from this endpoint to every visitor, anonymously. A 4 MiB background plus a
  burst of cold-cache visitors means each concurrent request holds a 4 MiB LOH
  allocation for the duration of the response. LOH allocations are not compacted by
  default, so this fragments the heap rather than simply using it.
- **Why:** The storage layer is already content-addressed by SHA-256 — the perfect ETag
  is sitting in the `Sha256` column, unused. Streaming and a strong ETag are both
  small changes that remove the allocation entirely.
- **Change:**
  1. Change `IMediaStore.ReadAsync` to return a stream (or add
     `Task<Stream?> OpenReadAsync(Guid assetId, CancellationToken ct)`), opened with
     `FileOptions.Asynchronous | FileOptions.SequentialScan`. Keep the byte-array
     overload only if a caller genuinely needs it — check; if `GetMedia` is the sole
     consumer, replace rather than add.
  2. Fetch `ContentType` and `Sha256` in the endpoint's existing single projection and
     pass them down, so the second `FindAsync` disappears. Net one database round trip.
  3. Serve with `Results.Stream(stream, contentType, enableRangeProcessing: true, entityTag: new EntityTagHeaderValue($"\"{sha256}\""))`
     (or `Results.File(path, …)` if handing Kestrel the path is acceptable — that enables
     `sendfile`-style transmission and is the fastest option, but it couples the endpoint
     to the filesystem layout; prefer the stream overload to keep `IMediaStore` as the
     seam, which is also what the tests mock).
     Range processing also gives correct behaviour for large images and any future video.
  4. The existing `MediaHeadersResult` wrapper sets `nosniff`, `Content-Disposition:
     inline` and `Cache-Control`. Keep all three — they are deliberate and documented.
     Add nothing that would let a client override the verified `Content-Type`.
  5. `ReadAsync` currently returns `null` both for "no such asset" and for "row exists
     but the file is missing from disk". Those are different operational problems —
     the second means storage and database have diverged. Keep the 404 for callers but
     log the divergent case at `Warning` through `ApiLoggerMessages`.
- **Database requirements:**
  - Affected entity/table: `MediaAsset` / `MediaAssets`.
  - No schema change and **no migration** — `Sha256` already exists, is `IsRequired()`, `HasMaxLength(64)`, and carries a unique index.
  - Relevant query: one `SELECT "ContentType", "Sha256" FROM "MediaAssets" WHERE "Id" = @id`, replacing today's two round trips.
- **API requirements:**
  - Endpoint: `GET /api/v1/media/{assetId}`.
  - Response: 200 with the image bytes and an `ETag` of the asset's SHA-256; 304 when `If-None-Match` matches; 206 for a satisfiable `Range` request; 404 unchanged.
  - Compatibility: additive. Existing clients that ignore `ETag` see identical behaviour.
- **Acceptance criteria:**
  - Serving a 5 MiB asset allocates no 5 MiB buffer (assert via a streaming assertion or a memory-pressure test, or simply by asserting `ReadAllBytesAsync` no longer appears on the path).
  - The response carries a strong `ETag` equal to the asset's `Sha256`.
  - A second request with `If-None-Match: "<sha>"` returns 304 with no body.
  - A `Range: bytes=0-99` request returns 206 with 100 bytes.
  - `X-Content-Type-Options: nosniff`, `Content-Disposition: inline; filename="<id>.<ext>"` and the immutable `Cache-Control` are all still present.
  - `GetMedia` issues exactly one database query.
- **Validation:** Extend `tests/Soulsjwa.ApiTests/MediaEndpointTests.cs` with the ETag,
  304 and 206 cases and a header-preservation assertion. Update
  `tests/Soulsjwa.UnitTests/MediaStoreTests.cs` for the new stream-returning signature.

---

### [BE-024] Make competitor invitation atomic

- **Priority:** P2
- **Axis:** Correctness
- **Location:** `src/Soulsjwa.Api/Features/Events/Endpoints/EventCompetitorsEndpoint.cs:AddCompetitor`
- **Problem:** Inviting by Twitch handle writes in two separate, untransacted steps. The
  placeholder `User` and the auto-created `AllowlistedTwitchLogin` are committed in an
  inner `SaveChangesAsync`:

  ```csharp
  db.Users.Add(placeholder);
  …
  db.AllowlistedTwitchLogins.Add(new AllowlistedTwitchLogin { … });
  await db.SaveChangesAsync(ct);      // commit #1
  targetUserId = placeholder.Id;
  ```

  and only afterwards does the method check for an existing competitor row, add the
  `EventCompetitor`, log the audit entry and call `SaveChangesAsync` again.
- **Failure scenarios:**
  - *Second save fails or the process dies.* The placeholder user and the allowlist entry
    persist; the competitor row does not. The handle is now allowlisted — i.e. **granted
    the ability to sign in** — as a side effect of an invitation that did not complete.
    Nothing cleans it up and nothing reports it.
  - *Concurrent invites of the same new handle.* Both requests find no existing user
    (`u.TwitchLogin.ToLower() == login`), both create a placeholder. `User.TwitchId` has
    a unique index and `PendingUserMarker.For(login)` is deterministic, so the second
    insert fails with 23505 → uncaught `DbUpdateException` → 500 instead of the
    `AddCompetitor` succeeding against the row the first request created.
  - The `AllowlistedTwitchLogins.TwitchLogin` unique index has the same race, with the
    same uncaught outcome.
- **Why:** Granting sign-in rights is a security-relevant write, and here it can be left
  behind by a partial failure. The 500 on concurrent invite is a smaller, but real,
  correctness defect on an owner-facing flow.
- **Change:**
  1. Wrap the whole handler in one transaction
     (`BeginTransactionAsync(IsolationLevel.ReadCommitted, ct)`, guarded by
     `db.Database.IsRelational()` as elsewhere) and remove the inner `SaveChangesAsync`.
     EF will order the inserts by dependency, so the placeholder is available as an FK
     target for `EventCompetitor` within the same save. Verify that — if EF needs the
     generated `Guid` first, note that `User.Id` is assigned client-side
     (`= Guid.NewGuid()`), so `placeholder.Id` is already populated before any save and
     the single-save form works.
  2. Catch the unique violations and resolve them as successes rather than errors.
     Following `EventsEndpoint.PatchEvent`'s established pattern, catch
     `PostgresErrorCodes.UniqueViolation` and, for the `Users.TwitchId` case, re-query the
     existing row and continue with it; for the allowlist case, treat it as already
     allowlisted. Add named constants for both index names next to the existing
     `UrlAliasIndexName` convention.
  3. Audit the allowlist grant. Today the auto-created `AllowlistedTwitchLogin` is
     written with `Note = "Auto-added via competitor invite"` and **no** audit row — the
     only audit emitted is `CompetitorAdded`. `AllowlistEndpoint.Add` emits
     `AuditEventTypes.AllowlistAdded` for the same kind of write. Emit it here too, so
     the audit trail shows every grant of sign-in rights regardless of which endpoint
     performed it.
  4. Consider whether the auto-allowlist should happen at all. `RequireOwner` permits the
     event's creator, and events are admin-created today — but an admin who is later
     demoted still owns their events and would retain the ability to allowlist arbitrary
     Twitch handles through this path. Either restrict the auto-allowlist branch to
     `EventOwnership.IsAdmin(principal)` (non-admin owners may invite existing users
     only), or document explicitly that event ownership implies the right to grant
     sign-in. Recommend the former; state the decision in the PR.
- **Database requirements:**
  - Affected entities/tables: `User` / `Users` (unique index on `TwitchId`), `AllowlistedTwitchLogin` / `AllowlistedTwitchLogins` (unique index on `TwitchLogin`), `EventCompetitor` / `EventCompetitors` (composite PK `(EventId, UserId)`).
  - No schema change and **no migration** — the constraints already exist; the code just has to respect them.
  - Transaction requirement: placeholder user + allowlist entry + competitor row + both audit rows commit together, or none do.
  - Concurrency strategy: existing unique indexes arbitrate; violations are caught and resolved to the existing row.
  - Expected behaviour: two concurrent invites of the same new handle → one 201, one 409 `"User is already a competitor in this event."` (the existing duplicate response), and exactly one `User` row and one allowlist row.
- **API requirements:**
  - Endpoint: `POST /api/v1/events/{eventId}/competitors`.
  - Response: 201 unchanged; 409 for the already-a-competitor case unchanged; the 500 on concurrent invite becomes a 201 or 409. Declare no new status codes.
  - Compatibility: none broken. If step 4 restricts auto-allowlist to admins, a non-admin owner inviting an unknown handle now receives 403 with a message telling them to ask an admin to allowlist first — document in `docs/auth.md`.
- **Acceptance criteria:**
  - A forced failure after the placeholder insert leaves **no** `User` row and **no** `AllowlistedTwitchLogin` row.
  - Two concurrent invites of the same unknown handle produce one 201 and one 409, one user row, one allowlist row, and no 500.
  - Inviting an unknown handle emits both `CompetitorAdded` and `AllowlistAdded` audit rows.
  - Inviting an existing user is unchanged.
  - `tests/Soulsjwa.ApiTests/EventCompetitorsEndpointTests.cs` passes unchanged.
- **Validation:** Extend `EventCompetitorsEndpointTests` with the audit-row assertion and
  the restricted-auto-allowlist case. Add a rollback test (throw after the placeholder
  insert, assert no rows) and a concurrency test in `tests/Soulsjwa.IntegrationTests/`
  — both need real Postgres.

---

### [BE-025] Reconcile the global rate limiter with the connector policy

- **Priority:** P2
- **Axis:** Reliability
- **Location:** `src/Soulsjwa.Api/Program.cs` — `options.GlobalLimiter` and the `"connector"` policy
- **Problem:** `GlobalLimiter` applies to **every** request in addition to any endpoint
  policy, and it permits 100/minute per authenticated user:

  ```csharp
  options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx => { … PermitLimit = 100 … });
  …
  options.AddPolicy("connector", ctx => { … PermitLimit = 120, QueueLimit = 2 … });
  ```

  The connector policy's 120/min can therefore never be reached — the global 100/min
  bucket exhausts first. The comment on the connector policy says it is "more permissive
  to avoid blocking frequent game-state submissions", which is precisely what the
  configuration prevents.

  A second interaction: the global limiter also covers every other authenticated call
  the connector's user makes from the web UI at the same time. A competitor whose
  connector is submitting steadily can find their own browser session 429-ing, because
  both share the `user_{userId}` partition.
- **Failure scenario:** A competitor runs the connector during a live event. The
  connector submits at its configured cadence; the competitor also has the event page
  open, polling the scoreboard. Their browser requests start failing with 429 even though
  neither the connector policy nor any documented limit has been exceeded, and the
  failure looks like a site outage to them.
- **Why:** Two limiters with contradictory intent produce a limit nobody chose and
  nobody can predict from reading either one. Whatever the right numbers are, they should
  be derivable from the configuration.
- **Change:**
  1. Decide the intended budget explicitly and encode it. Recommended: exempt endpoints
     that carry their own policy from the global limiter, so a policy means what it says.
     Implement by having `GlobalLimiter` return `RateLimitPartition.GetNoLimiter<string>(…)`
     when the matched endpoint has `IRateLimiterPolicyMetadata` / an
     `EnableRateLimitingAttribute` — inspect `ctx.GetEndpoint()?.Metadata` and skip. This
     is the smallest change that makes the per-endpoint numbers authoritative.
  2. Raise the connector policy's limit to what the connector actually needs. Check
     `src/Soulsjwa.Connector/` for its poll interval and set the limit from that with
     headroom, as a named constant bound from configuration
     (`RateLimits:ConnectorPerMinute`). Do not leave a number in the code that nobody can
     trace to a requirement.
  3. Make every limit configurable and named, per `AGENTS.md` §4:
     `RateLimits:GlobalPerUserPerMinute`, `RateLimits:GlobalPerIpPerMinute`,
     `RateLimits:AuthPerIpPerMinute` (BE-001), `RateLimits:ConnectorPerMinute`. Defaults
     equal to today's values except where step 2 changes them.
  4. Emit `Retry-After` on 429. `options.RejectionStatusCode` is set but no
     `OnRejected` callback exists, so clients get a bare 429 with no guidance and the
     connector cannot back off intelligently. Add an `OnRejected` that writes
     `Retry-After` from the lease's `RetryAfter` metadata when available, and logs the
     rejection through `ApiLoggerMessages` with the partition key so throttling is
     observable.
  5. Document the resulting budget in `docs/api-reference.md` — the numbers, the
     partitioning, and the fact that endpoint policies override the global limit.
- **Acceptance criteria:**
  - A connector user can sustain the documented connector rate without the global limiter interfering.
  - A 429 response carries a `Retry-After` header.
  - Every limit is read from configuration with the current value as its default.
  - Browser traffic from a user whose connector is active is not throttled by the connector's consumption (or, if the decision is that they should share a budget, that is documented and tested as intentional).
  - `docs/api-reference.md` states the full rate-limit budget.
- **Validation:** New `tests/Soulsjwa.ApiTests/RateLimitBudgetTests.cs` asserting the
  connector endpoint admits more than the global per-user limit within one window, and
  that a 429 carries `Retry-After`. `Depends on: BE-001` — that task establishes the
  named-constant structure this one extends.

---

### [BE-026] Replace OFFSET pagination on the audit log

- **Priority:** P2
- **Axis:** Performance
- **Location:** `src/Soulsjwa.Api/Features/Audits/Endpoints/AuditsEndpoint.cs:PaginateAsync`
- **Problem:** Every audit page runs a full `CountAsync` over the filtered set and then
  `Skip((page - 1) * pageSize).Take(pageSize)`:

  ```csharp
  var total = await query.CountAsync(ct);
  var rows = await query
      .OrderByDescending(a => a.CreatedAt)
      .ThenByDescending(a => a.Id)
      .Skip((page - 1) * pageSize)
      .Take(pageSize)
      .ToListAsync(ct);
  ```

  `AuditLogs` is append-only and unbounded (BE-015). `OFFSET n` requires Postgres to
  generate and discard `n` rows, so page 500 costs 500 × `pageSize` row reads. The
  `COUNT(*)` scans the whole filtered set on every request regardless of page.

  The composite index `IX_AuditLogs_EventId_CreatedAt` serves the per-event ordering
  well; the admin endpoint without an `eventId` filter falls back to
  `IX_AuditLogs_CreatedAt`, which does order correctly but still pays the offset cost.
- **Failure scenario:** An admin opens `/api/v1/admin/audits` and pages deep into the
  history of a long-running deployment, or applies a `type` filter that matches a small
  subset of a large table — the `COUNT` scans everything matching while the page returns
  20 rows. Latency grows linearly with both offset and table size, and the table only
  grows.
- **Why:** This is the standard, well-understood failure mode of offset pagination on an
  append-only table. The data has a perfect cursor already — `(CreatedAt, Id)` is the
  sort key and `Id` makes it unique.
- **Change:**
  1. Add keyset (cursor) pagination alongside the existing page-number form. Accept an
     opaque `cursor` query parameter encoding the last row's `(CreatedAt, Id)`; when
     present, replace the offset with

     ```csharp
     query = query.Where(a => a.CreatedAt < cursorCreatedAt
                           || (a.CreatedAt == cursorCreatedAt && a.Id < cursorId));
     ```

     matching the existing `OrderByDescending(CreatedAt).ThenByDescending(Id)` exactly —
     the tuple comparison must mirror the sort or rows are skipped or repeated at page
     boundaries.
  2. Return `nextCursor` in the response. Extend `PaginatedResponse<T>` with an optional
     `NextCursor`, or introduce a sibling `CursorPagedResponse<T>` — prefer extending, so
     there is one pagination envelope in the codebase rather than two.
  3. Make the count optional. Keep `totalCount` for page 1 (the UI needs it to size the
     pager) and omit it when a cursor is supplied. Add a `includeTotal` parameter
     defaulting to `cursor is null`.
  4. Keep offset pagination working for compatibility, but cap it: reject `page` beyond a
     named `MaxOffsetPage` (e.g. 100) with a 400 directing the caller to use the cursor.
     That bounds the worst case without breaking existing clients.
  5. The multi-value `type` filter uses `normalized.Contains(a.Type)`, which becomes
     `"Type" = ANY(@p)` — fine, and `IX_AuditLogs_Type` serves it. Leave it.
- **Database requirements:**
  - Affected entity/table: `AuditLog` / `AuditLogs`.
  - Relevant query: `… WHERE "EventId" = @e AND ("CreatedAt", "Id") < (@c, @i) ORDER BY "CreatedAt" DESC, "Id" DESC LIMIT @l`.
  - Required index: `IX_AuditLogs_EventId_CreatedAt` exists but does not include `Id`, so the tie-break column is not covered. Extend it to `("EventId", "CreatedAt", "Id")` and extend `IX_AuditLogs_CreatedAt` to `("CreatedAt", "Id")`. Both are cheap — `Id` is the primary key and already in every index entry as the heap pointer, but making it an explicit trailing column lets the planner satisfy the tuple comparison from the index alone.
  - Migration: `dotnet ef migrations add ExtendAuditIndexesForKeyset` — drop and recreate the two indexes. On a large table build the replacements `CONCURRENTLY` via `migrationBuilder.Sql(...)` so the migration does not take a long `ACCESS EXCLUSIVE` lock; note that `CREATE INDEX CONCURRENTLY` cannot run inside a transaction, so the migration must be marked accordingly (`migrationBuilder.Sql(sql, suppressTransaction: true)`).
  - Transaction requirement: none (read-only endpoint).
  - Expected query behaviour: constant-time page fetches independent of depth — `EXPLAIN ANALYZE` shows `Index Scan Backward` with `rows=pageSize` and no `Seq Scan`, and no `COUNT` on cursor pages.
- **API requirements:**
  - Endpoints: `GET /api/v1/events/{eventId}/audits`, `GET /api/v1/admin/audits`.
  - Request: new optional `cursor` and `includeTotal`. Existing `page`/`pageSize` still work, with `page` capped.
  - Response: `PaginatedResponse<AuditLogResponse>` gains `nextCursor`; `totalCount` may be absent on cursor pages. Both are additive.
  - 400 when `page` exceeds `MaxOffsetPage`, with a message naming the cursor parameter.
  - Compatibility: additive. Update `src/Soulsjwa.Web/src/features/audits/` to use the cursor for "load more" in the same PR.
- **Acceptance criteria:**
  - Paging through a 10,000-row audit fixture by cursor returns every row exactly once, with no duplicates or gaps at page boundaries — including when many rows share a `CreatedAt`.
  - Cursor page N costs the same as page 1 (assert via query plan or timing).
  - No `COUNT` is issued for cursor pages.
  - `page=101` returns 400.
  - Existing offset behaviour for pages 1–100 is unchanged.
- **Validation:** New `tests/Soulsjwa.IntegrationTests/AuditKeysetPaginationTests.cs`
  seeding rows with deliberately colliding `CreatedAt` values and asserting complete,
  duplicate-free traversal. Extend the existing audit tests for the 400 and the
  additive response fields. `Depends on: BE-015` — decide retention before optimising
  pagination over the table, since retention may make the deep pages moot.

---

### [BE-027] Batch the fail-rule cascade writes

- **Priority:** P2
- **Axis:** Performance
- **Location:** `src/Soulsjwa.Api/Features/Games/Services/FailRuleCascadeEvaluator.cs:ApplyAsync`
- **Problem:** The cascade saves inside its loop:

  ```csharp
  foreach (var candidateUserId in pendingUserIds)
  {
      var otherCompletions = completedUserIds.Count(id => id != candidateUserId);
      if (!RuleEvaluator.Evaluate(objective.FailRule, "{}", otherCompletions)) continue;
      db.FailedObjectives.Add(new FailedObjective { ObjectiveId = objectiveId, UserId = candidateUserId });
      await db.SaveChangesAsync(ct);   // one round trip per newly-failed competitor
      newlyFailed++;
  }
  ```

  Each iteration also re-evaluates the rule from its string form
  (`RuleEvaluator.Evaluate(objective.FailRule, "{}", …)` re-parses both the rule and the
  `"{}"` data every time — the same waste as BE-016) and recomputes `otherCompletions`
  with a linear `Count` over `completedUserIds`.

  Worse, the caller invokes this **per completed objective**:

  ```csharp
  foreach (var objectiveId in completedObjectiveIdsThisSubmission)
      await FailRuleCascadeEvaluator.ApplyAsync(db, objectiveId, userId, ct);
  ```

  and `ApplyAsync` itself opens with four queries (objective, event game, competitor ids,
  completed ids) plus one for failed ids. So a submission that completes 10 objectives
  issues 50 queries before any writes.
- **Failure scenario:** A connector submission that satisfies many objectives at once —
  common right after a competitor enables a game, or when a save file is first read —
  triggers 5 queries × N objectives plus one round trip per cascaded failure, all inside
  the transaction that holds the `pg_advisory_xact_lock` for the event game. Every other
  competitor's submission for that game waits behind it.
- **Why:** It multiplies lock hold time on the hottest write path, and it is the kind of
  loop that looks fine at three competitors and is pathological at thirty.
- **Change:**
  1. Move the `SaveChangesAsync` out of the loop — add all `FailedObjective` rows, then
     save once. The method already runs inside the caller's transaction, so batching
     changes nothing about atomicity.
  2. Change the signature to take **all** the objective ids completed in this submission
     at once: `ApplyAsync(AppDbContext db, IReadOnlyList<Guid> objectiveIds, Guid completingUserId, CancellationToken ct)`.
     Load the objectives, their event games, the competitor roster and the
     completed/failed sets in one query each for the whole batch rather than per
     objective — the shape mirrors `ScoreboardEndpoint.BuildBatchAsync`, which already
     solves this exact problem for scoreboards and is the pattern to copy.
  3. Hoist the parsed rule out of the loop (`JObject` parsed once per objective — see
     BE-016, which this depends on for the parsed-input overload).
  4. Replace the per-candidate `completedUserIds.Count(id => id != candidateUserId)` with
     `completedUserIds.Count - (completedUserIds.Contains(candidateUserId) ? 1 : 0)` over
     a `HashSet` — the current form is O(n) per candidate for a value that is almost
     always just `completedUserIds.Count`.
  5. Preserve semantics exactly. In particular: the trial filter (`TrialRunId == null` on
     both sets) and the caller's guard that the cascade runs only for official
     completions must both survive. The existing code comments call these out as
     deliberate; keep the comments.
  6. `CompletedObjectivesEndpoint.CompleteObjective` calls `ApplyAsync` for a single
     objective — update it to pass a single-element list rather than keeping a second
     overload.
- **Database requirements:**
  - Affected entities/tables: `Objective`, `EventGame`, `EventCompetitor`, `CompletedObjective`, `FailedObjective`.
  - Relevant queries: five batched reads keyed by `objectiveIds` / `eventId`, then one batched insert of `FailedObjectives`.
  - Required index: none new. The inserts hit `IX_FailedObjectives_ObjectiveId_UserId_Official`, which already enforces uniqueness.
  - Transaction requirement: inherits the caller's transaction. Do not open a nested one.
  - Expected behaviour: a constant number of queries per submission rather than `5N + failures`.
- **Acceptance criteria:**
  - A submission completing N objectives issues a number of queries independent of N for the cascade.
  - One `SaveChangesAsync` per cascade invocation.
  - The set of competitors marked failed is identical to the current implementation for every existing fixture.
  - Trial completions still do not cascade, and trial rows are still excluded from both the completed and failed sets.
  - A unique-violation on `FailedObjectives` (another writer got there first) is handled, not thrown — the advisory lock should prevent it, but the batched insert makes a partial failure noisier, so catch `PostgresErrorCodes.UniqueViolation` and treat the row as already present.
- **Validation:** Extend `tests/Soulsjwa.ApiTests/ConnectorEndpointTests.cs` and the
  fail-rule tests with a multi-objective submission asserting identical outcomes and a
  bounded query count. `Depends on: BE-016`.

---

### [BE-028] Normalise error responses on one shape

- **Priority:** P2
- **Axis:** Maintainability
- **Location:** `src/Soulsjwa.Api/Features/Connector/Endpoints/ConnectorEndpoint.cs` (`Results.NotFound("Game is not part of this event.")`, `Results.NotFound("User is not a competitor in this event.")`, `GetGameData`'s three `Results.NotFound(string)` calls); `src/Soulsjwa.Api/Features/Users/Endpoints/DeleteApiKeyEndpoint.cs` (`Results.NotFound()`); `src/Soulsjwa.Api/Features/Users/Endpoints/GetCurrentUserEndpoint.cs` (`Results.NotFound()`); `src/Soulsjwa.Api/Features/Auth/Endpoints/TwitchCallbackEndpoint.cs` (`Results.BadRequest("Invalid state parameter")`)
- **Problem:** Three different error shapes coexist:
  - `Results.Problem(detail: "…", statusCode: 404)` → RFC 7807 `application/problem+json`. This is the dominant form — roughly 60 call sites.
  - `Results.NotFound("some string")` → a bare JSON string body with `content-type: application/json`.
  - `Results.NotFound()` / `Results.Unauthorized()` → empty body.

  A client cannot parse errors uniformly: sometimes `response.detail` exists, sometimes
  the body is a bare quoted string, sometimes there is no body at all. `AddProblemDetails()`
  is registered and `UseStatusCodePages()` fills in bodies for empty non-success
  responses, so the empty cases are partially covered — but `Results.NotFound(string)`
  produces a body and therefore bypasses `UseStatusCodePages` entirely, leaving the
  inconsistent shape intact.
- **Why:** Not a bug, but it is the kind of inconsistency that makes client error
  handling defensive and verbose, and it undermines the OpenAPI contract — the endpoints
  declare `.ProducesProblem(StatusCodes.Status404NotFound)` while returning something
  that is not a problem document.
- **Change:**
  1. Convert every `Results.NotFound(string)` / `Results.BadRequest(string)` to
     `Results.Problem(detail: …, statusCode: …)`, matching the surrounding style. There
     are roughly eight call sites; `grep -rn "Results.NotFound(\|Results.BadRequest(" src/Soulsjwa.Api --include=*.cs`
     finds them all.
  2. Leave `Results.Unauthorized()` and `Results.NoContent()` alone — an empty 401/204 is
     correct, and `UseStatusCodePages` handles the 401 body. Do **not** add a `detail` to
     401 responses; BE-002 and BE-005 both depend on 401s being indistinguishable.
  3. Add an analyzer-style guard so this does not drift back: a small test that reflects
     over the endpoint assemblies is overkill. Instead, add the rule to
     `docs/agent-conventions/` — a short "error responses use `Results.Problem`; empty
     401/204 are the only exceptions" note — so future agents follow it.
  4. While converting `ConnectorEndpoint`, note it returns 404 for
     `"User is not a competitor in this event."` where the parallel check in
     `CompletedObjectivesEndpoint` returns 403 for the same condition. Pick one. 403 is
     more accurate (the resource exists; the caller is not entitled to write to it) but
     404 leaks less. Given the event and game ids are already known to the caller at that
     point, 403 leaks nothing extra — use 403 and make both consistent.
- **API requirements:**
  - Endpoints: the eight listed call sites.
  - Response: `application/problem+json` with `detail`, replacing bare strings and empty bodies where a body is expected.
  - Status codes: unchanged except the connector's not-a-competitor case, 404 → 403.
  - Compatibility: a client parsing the bare string body breaks. Check `src/Soulsjwa.Web/src/lib/axios/` and `src/Soulsjwa.Connector/Services/` for error-body handling and update in the same PR. The connector's `ApiResult` type (`tests/Soulsjwa.ConnectorTests/ApiResultTests.cs` exists, so there is one) is the main consumer to verify.
- **Acceptance criteria:**
  - Every non-2xx response from the API carries either an RFC 7807 body or no body at all — never a bare JSON string.
  - `grep -rn "Results.NotFound(\"\|Results.BadRequest(\"" src/Soulsjwa.Api --include=*.cs` returns nothing.
  - 401 responses remain empty and indistinguishable from one another.
  - The connector returns 403, not 404, for a non-competitor submission, and `docs/api-reference.md` reflects it.
  - `tests/Soulsjwa.ConnectorTests/ApiResultTests.cs` passes, updated if it asserted the old shape.
- **Validation:** Extend `tests/Soulsjwa.ApiTests/ConnectorEndpointTests.cs` and
  `UsersApiKeysEndpointTests.cs` to assert `content-type: application/problem+json` and a
  present `detail`. Add one test per converted call site.

---

## P3 — Cleanup & Simplification

### [BE-029] Remove the two unused NuGet packages

- **Priority:** P3
- **Axis:** Maintainability
- **Location:** `src/Soulsjwa.Api/Soulsjwa.Api.csproj`
- **Problem:** Two packages are referenced and never used. Verified by grep across `src/`:
  - `BCrypt.Net-Next` (4.2.0) — zero occurrences of `BCrypt` anywhere. Password hashing does not exist in this system; authentication is Twitch OAuth plus SHA-256-hashed opaque tokens.
  - `System.IdentityModel.Tokens.Jwt` (8.22.0) — zero occurrences of `System.IdentityModel` or `JwtSecurityToken`. `JwtTokenService` uses `Microsoft.IdentityModel.JsonWebTokens.JsonWebTokenHandler`, which comes from `Microsoft.AspNetCore.Authentication.JwtBearer`'s own dependency chain.
- **Why:** Dead dependencies are supply-chain surface, they appear in
  `THIRD-PARTY-NOTICES.md` implying a licence obligation that is not real, and they show
  up in vulnerability scans generating work that cannot be justified. `BCrypt.Net-Next` in
  particular suggests to a reader that passwords are stored somewhere, which they are not.
- **Change:**
  1. Delete both `<PackageReference>` lines.
  2. Build. If `System.IdentityModel.Tokens.Jwt` turns out to be a transitive requirement of something, the build will say so — restore it and note why in a comment rather than guessing.
  3. Run `tools/generate_third_party_notices.sh` and commit the regenerated `THIRD-PARTY-NOTICES.md` in the same PR — `AGENTS.md` §6 requires it and CI fails if it is stale.
- **Acceptance criteria:**
  - `dotnet build Soulsjwa.slnx` succeeds.
  - `dotnet test Soulsjwa.slnx` passes.
  - `THIRD-PARTY-NOTICES.md` no longer lists either package and is regenerated by the script, not hand-edited.
- **Validation:** Full build and test run.

---

### [BE-030] Paginate `GET /api/v1/objectives/predefined`

- **Priority:** P3
- **Axis:** Performance
- **Location:** `src/Soulsjwa.Api/Features/Events/Endpoints/ObjectivesEndpoint.cs:ListPredefined`
- **Problem:** Anonymous, unpaginated, and returns every predefined objective template in
  the catalog with its full `Rule`, `FailRule` and `Metadata` JSON. The `gameId` filter
  is optional. `src/Soulsjwa.Api/Features/Games/Definitions/SoulMemoryCatalogData.cs` is
  8,964 lines, and `PredefinedObjectiveSeeder` populates the table from those
  definitions, so the unfiltered result set is large and each row carries JSON payloads.
- **Why:** Same class of problem as BE-006 but much smaller in practice — the data is
  static, cacheable, and the frontend almost certainly always passes `gameId`. Worth
  bounding, not worth urgency.
- **Change:**
  1. Add `page`/`pageSize` with the same clamping used by `EventsEndpoint.ListEvents` (default 20, max 100) and return `PaginatedResponse<PredefinedObjectiveResponse>`.
  2. Alternatively — and preferably, if the frontend's rule builder genuinely needs the whole set for a game at once — make `gameId` **required** and keep the response unpaginated per game. That matches how the data is actually consumed. Check `src/Soulsjwa.Web/src/features/events/components/blockly/` before choosing.
  3. Add `.CacheOutput(...)` with a long expiry and a `CacheTags.PredefinedObjectives` tag, evicted by `CreatePredefined`. The catalog changes only when an admin adds a template.
  4. `AsNoTracking()`.
- **API requirements:**
  - Endpoint: `GET /api/v1/objectives/predefined`.
  - Either a required `gameId` (400 without it) or a paginated envelope — not both.
  - Compatibility: breaking either way; update the frontend in the same PR.
- **Acceptance criteria:**
  - The endpoint cannot return an unbounded result set.
  - Two identical requests within the cache window hit the cache.
  - Creating a predefined objective evicts it.
- **Validation:** Extend `tests/Soulsjwa.ApiTests/ObjectivesEndpointTests.cs`.

---

### [BE-031] Normalise `TwitchLogin` so lookups are indexable

- **Priority:** P3
- **Axis:** Performance
- **Location:** `src/Soulsjwa.Api/Infrastructure/Auth/TwitchAuthService.cs:UpsertUserAsync`; `src/Soulsjwa.Api/Features/Admin/Endpoints/AllowlistEndpoint.cs` (`Add`, `Remove`, `List`); `src/Soulsjwa.Api/Features/Events/Endpoints/EventCompetitorsEndpoint.cs:AddCompetitor`; `src/Soulsjwa.Api/Features/Users/Endpoints/SearchUsersEndpoint.cs`
- **Problem:** Case-insensitive matching is done by calling `ToLower()` on the column:

  ```csharp
  u.TwitchLogin.ToLower() == loginLower          // TwitchAuthService, AllowlistEndpoint ×3, EventCompetitorsEndpoint
  u.TwitchLogin.ToLower().Contains(lower)        // SearchUsersEndpoint
  logins.Contains(u.TwitchLogin.ToLower())       // AllowlistEndpoint.List
  ```

  `User` has a unique index on `TwitchId` and none on `TwitchLogin`; even if one existed,
  `LOWER("TwitchLogin")` could not use it. Every one of these is a sequential scan of
  `Users`. The OAuth login path hits it twice per sign-in.

  `AllowlistedTwitchLogin` is handled correctly by contrast — the value is lowercased on
  write (`request.TwitchLogin?.Trim().ToLowerInvariant()`) and compared raw
  (`a.TwitchLogin == login`), so its unique index works. `User.TwitchLogin` stores
  whatever Twitch returned, mixed case.
- **Why:** Small table today, so the scans are cheap — this is a latent cost, not a
  current outage. But the inconsistency between the two tables is a correctness trap:
  a reader could reasonably assume `Users.TwitchLogin` is lowercase because
  `AllowlistedTwitchLogins.TwitchLogin` is.
- **Change:** Two viable approaches; pick one and apply it everywhere.
  - **(a) Functional index.** Keep the column as-is and add
    `CREATE INDEX "IX_Users_TwitchLogin_Lower" ON "Users" (LOWER("TwitchLogin"));` via
    `migrationBuilder.Sql(...)`. The existing `.ToLower()` predicates then use it with no
    code change. Smallest diff, preserves the display casing Twitch supplies.
  - **(b) Normalised column.** Add `TwitchLoginNormalized`, maintained on write, with a
    unique index; rewrite the predicates to compare against it. More invasive, and it
    duplicates state.

  Prefer **(a)**. For `SearchUsersEndpoint`'s `Contains`, a functional B-tree index does
  not help a leading-wildcard `LIKE` — either accept the scan (the endpoint is
  authenticated, capped at 20 results, and requires 2+ characters) or add a trigram index
  as in BE-019. Accepting the scan is fine here; say so in a comment rather than leaving
  it unexplained.
- **Database requirements:**
  - Affected entity/table: `User` / `Users`. Column `TwitchLogin`.
  - Required index: `IX_Users_TwitchLogin_Lower` on `LOWER("TwitchLogin")`.
  - Migration: `dotnet ef migrations add AddUsersTwitchLoginLowerIndex`, using raw SQL — EF cannot express a functional index declaratively.
  - Note: this cannot be a **unique** index. Two Twitch accounts cannot share a login in practice, but placeholder rows created by `PendingUserMarker` use the login as the display name too, and enforcing uniqueness here would turn a benign data state into a failed migration. Keep it non-unique.
  - Expected behaviour: `EXPLAIN ANALYZE` on the OAuth upsert lookup shows an index scan, not `Seq Scan`.
- **Acceptance criteria:**
  - The login-path lookups use the index (verified by query plan).
  - Behaviour is unchanged for mixed-case logins.
  - `docs/database-design.md` documents the index and why it is functional rather than plain.
- **Validation:** An `EXPLAIN` assertion in `tests/Soulsjwa.IntegrationTests/`;
  `tests/Soulsjwa.UnitTests/TwitchAuthServiceTests.cs` and
  `SearchUsersEndpointTests.cs` pass unchanged.

---

### [BE-032] Fix the wrong log message on objective deletion

- **Priority:** P3
- **Axis:** Maintainability
- **Location:** `src/Soulsjwa.Api/Features/Events/Endpoints/ObjectivesEndpoint.cs:DeleteObjective` — final line before the `return`
- **Problem:** The delete handler logs the *update* message:

  ```csharp
  db.Objectives.Remove(objective);
  await db.SaveChangesAsync(ct);
  await cache.EvictByTagAsync(CacheTags.Scoreboard, ct);
  logger.ObjectiveUpdated(objectiveId, eventGameId, eventId, callerId);   // should be ObjectiveDeleted
  ```

  The audit row is correct (`AuditEventTypes.ObjectiveDeleted`); only the structured log
  is wrong.
- **Why:** Anyone searching logs for a deletion finds nothing, and anyone counting
  `ObjectiveUpdated` events overcounts. Trivial to fix, genuinely misleading if not.
- **Change:** Add an `ObjectiveDeleted` message to
  `src/Soulsjwa.Api/Diagnostics/ApiLoggerMessages.Events.cs` following the existing
  source-generated `[LoggerMessage]` pattern and the surrounding event-id numbering, and
  call it here. While in that file, check the other delete handlers for the same
  copy-paste — `EventGamesEndpoint.RemoveEventGame` and
  `EventCompetitorsEndpoint.RemoveCompetitor` both look correct, but confirm.
- **Acceptance criteria:**
  - Deleting an objective emits an `ObjectiveDeleted` log event.
  - No other handler logs a message naming a different operation than it performs.
  - Event ids in `ApiLoggerMessages.Events.cs` remain unique.
- **Validation:** Assert against an in-memory Serilog sink in
  `tests/Soulsjwa.ApiTests/ObjectivesEndpointTests.cs`.

---

### [BE-033] Replace the hand-rolled base64 substitution in token generation

- **Priority:** P3
- **Axis:** Maintainability
- **Location:** `src/Soulsjwa.Api/Infrastructure/Auth/ApiKeyAuthHandler.cs:GenerateApiKey`; `src/Soulsjwa.Api/Features/Events/OverlayToken.cs:Generate`
- **Problem:** Both generators produce URL-safe text by substituting characters:

  ```csharp
  var keyPart = Convert.ToBase64String(keyBytes)
      .Replace("+", "A").Replace("/", "B").Replace("=", "C")[..32];
  ```

  This is not an encoding — it is a lossy mapping that collapses three distinct symbols
  onto three that already exist, biasing the output alphabet. `OverlayToken.Generate`
  carries a comment acknowledging it and correctly notes the entropy reduction is
  negligible at 32 bytes of input. That analysis is right; 32 characters of a
  ~62-symbol alphabet is ~190 bits, far beyond what an opaque token needs. So this is
  **not** a security finding.
- **Why:** It is a correctness smell that reads like a security bug to every future
  reviewer, and .NET now has the right primitive. Replacing it removes the need for the
  defensive comment entirely.
- **Change:**
  1. Use `System.Buffers.Text.Base64Url.EncodeToString(bytes)` (available in .NET 9+; the project targets `net10.0`). It produces a genuine URL-safe encoding with no padding and no substitution.
  2. Apply to both generators. Keep the emitted length the same (`[..32]` after encoding) so `TokenPrefix`/`KeyPrefix` semantics and the `HasMaxLength(16)` on `EventOverlayToken.TokenPrefix` are unaffected.
  3. Delete the entropy-justification comment in `OverlayToken` — it documents a workaround that no longer exists. Keep the comment explaining the `ot_` scheme and prefix purpose.
  4. **Do not** change `HashApiKey` or `OverlayToken.Hash`. They are `Convert.ToBase64String(SHA256.HashData(...))` and are stored in the database; changing that encoding would invalidate every existing key and token.
- **Acceptance criteria:**
  - Newly generated keys and tokens contain only URL-safe characters.
  - `TryExtractPrefix` still parses them (`sk_`/`ot_` scheme, prefix length unchanged).
  - Existing keys and tokens in the database still authenticate — the hash function is untouched.
  - No `.Replace("+", …)` remains in either file.
- **Validation:** `tests/Soulsjwa.UnitTests/ApiKeyAuthHandlerTests.cs` and
  `tests/Soulsjwa.ApiTests/OverlayTokensEndpointTests.cs` pass; add a case asserting a
  generated value matches `^[A-Za-z0-9_-]+$` and one asserting a pre-existing
  hash still validates.

---

### [BE-034] Do not carry `IsEnabled` into a duplicated event

- **Priority:** P3
- **Axis:** Correctness
- **Location:** `src/Soulsjwa.Api/Features/Events/Endpoints/EventDuplicationEndpoint.cs:DuplicateEvent`
- **Problem:** The copy inherits the source game's enabled flag:

  ```csharp
  var gameCopy = new EventGame
  {
      …
      IsEnabled = game.IsEnabled,
      SortOrder = game.SortOrder,
  };
  ```

  while the new `Event` is created with `IsStarted` defaulting to `false`. That combination
  is unreachable through any other code path: `EnableEventGame` refuses unless
  `ev.IsStarted`, and `StopEvent` refuses while any game is enabled. The duplicate lands
  in a state the invariants say cannot exist — stopped, with an active game.
- **Why:** It is a small inconsistency, but it is exactly the kind of state that makes a
  later assertion or query behave unexpectedly, and it contradicts the endpoint's own doc
  comment that "the copy starts fresh: not started, not archived, not featured".
- **Change:** Set `IsEnabled = false` on every copied game and extend the existing doc
  comment to say so. The partial unique index tolerates the current behaviour (at most
  one enabled per event is still satisfied), so this is a semantic fix, not a constraint
  fix.
- **Acceptance criteria:**
  - Duplicating an event whose game is enabled produces a copy with no enabled game.
  - Every other copied field is unchanged.
  - The endpoint's doc comment lists `IsEnabled` among the reset fields.
- **Validation:** Extend `tests/Soulsjwa.ApiTests/EventDuplicationEndpointTests.cs`.

---

### [BE-035] Cap API keys per user

- **Priority:** P3
- **Axis:** Reliability
- **Location:** `src/Soulsjwa.Api/Features/Users/Endpoints/CreateApiKeyEndpoint.cs:Handle`
- **Problem:** No limit on how many keys a user can mint. Each is a permanent row and a
  permanent credential; `GetApiKeysEndpoint` returns all of them including revoked ones,
  unpaginated.
- **Why:** Low impact — it requires an authenticated, allowlisted account, and the table
  is tiny. But an unbounded credential-minting endpoint is worth a ceiling on principle,
  and the revoked-key accumulation makes the user's own key list progressively less
  usable.
- **Change:**
  1. Add a named `MaxActiveKeysPerUser` (suggest 10) and return 409 when the caller already has that many un-revoked, un-expired keys, with a message telling them to revoke one first.
  2. Filter `GetApiKeysEndpoint` to un-revoked keys by default, with an `includeRevoked=true` opt-in. Revoked keys are audit history, not something the key-management UI needs by default.
  3. Consider having BE-015's retention service delete `ApiKeys` revoked longer than a configurable window. Optional; only do it if the retention service already exists by the time this task is picked up.
- **API requirements:**
  - Endpoint: `POST /api/v1/users/me/api-keys` gains a 409; declare `.ProducesProblem(StatusCodes.Status409Conflict)`.
  - `GET /api/v1/users/me/api-keys` gains `includeRevoked`; default response now excludes revoked keys — a minor breaking change, so update `src/Soulsjwa.Web/src/features/users/` in the same PR.
- **Acceptance criteria:**
  - Creating an 11th active key returns 409.
  - Revoking one then creating another succeeds.
  - The list endpoint omits revoked keys unless asked.
- **Validation:** Extend `tests/Soulsjwa.ApiTests/UsersApiKeysEndpointTests.cs`.

---

### [BE-036] Stop storing whole documents twice per audit row

- **Priority:** P3
- **Axis:** Maintainability
- **Location:** `src/Soulsjwa.Api/Features/Legal/Endpoints/LegalDocumentsEndpoint.cs:UpdateDocument`; `src/Soulsjwa.Api/Features/Events/Endpoints/EventRulesEndpoint.cs:UpdateRules`
- **Problem:** Both audit the full document body on both sides of the edit:

  ```csharp
  audit.Log(db, AuditEventTypes.LegalDocumentUpdated, actorId,
      before: new { Kind = parsedKind.ToString(), Content = beforeContent },
      after:  new { Kind = parsedKind.ToString(), doc.Content });
  ```

  `MaxContentBytes` is 64 KiB, so one edit can write 128 KiB of `jsonb` into `AuditLogs` —
  a table with seven indexes and (before BE-015) no retention. `CalendarEntriesEndpoint`
  by contrast audits only the scalar fields and omits `DescriptionMarkdown` entirely,
  which is the right instinct.
- **Why:** It inflates the audit table by orders of magnitude relative to every other
  event type, and the audit list endpoint returns `BeforeJson`/`AfterJson` verbatim to
  any event member — so a 128 KiB row is also 128 KiB on the wire per audit page entry.
- **Change:**
  1. Replace the full content with a digest plus a size: `new { Kind, ContentSha256, ContentLength }`, using the same `Convert.ToHexStringLower(SHA256.HashData(...))` form `MediaStore` already uses. That preserves the audit's purpose — proving *that* and *when* the document changed, and by whom — without storing it twice.
  2. If the product genuinely needs the previous text recoverable, that is document
     versioning, not auditing — a separate `LegalDocumentVersion` / `EventRulesVersion`
     table. Do not build it under this task; note it as a follow-up if the owner asks.
  3. Leave every other `audit.Log` call alone. They record small scalar deltas and are
     appropriately sized.
- **Acceptance criteria:**
  - Updating a legal document or event rules writes an audit row whose `BeforeJson`/`AfterJson` are a few hundred bytes, not tens of kilobytes.
  - The audit row still identifies the actor, the kind, the timestamp and the fact of change.
  - `docs/api-reference.md` notes that document audits record a digest rather than content.
- **Validation:** Extend `tests/Soulsjwa.ApiTests/LegalDocumentsEndpointTests.cs` and
  `EventRulesEndpointTests.cs` with a size assertion on the emitted audit row.

---

### [BE-037] Constrain `AllowedHosts`

- **Priority:** P3
- **Axis:** Security
- **Location:** `src/Soulsjwa.Api/appsettings.json` — `"AllowedHosts": "*"`
- **Problem:** Host filtering is disabled. The app accepts any `Host` header. Combined
  with `UseForwardedHeaders` and `UseHsts`, an attacker-supplied `Host` reaches
  `httpContext.Request.Host`, which the Serilog enricher logs as `RequestHost` and which
  any future absolute-URL generation would use.
- **Why:** Currently low impact — no code builds an absolute URL from the request host
  (redirects use `Frontend:Url` from configuration, and `Results.Created` emits relative
  paths), so there is no cache-poisoning or password-reset-link vector today. It is a
  defence-in-depth gap that costs one configuration line to close, and it stops the
  "someone adds an absolute URL later" failure before it starts.
- **Change:**
  1. Leave `appsettings.json` permissive for local development, but document and set
     `AllowedHosts` in deployment. Add `ALLOWED_HOSTS` to `.env.example` and wire
     `AllowedHosts: "${ALLOWED_HOSTS}"` into `docker-compose.yml`'s `api` service
     environment as `AllowedHosts`.
  2. Document in `docs/deployment.md` that `AllowedHosts` must be set to the deployment's
     real hostname(s) and that `*` is development-only.
  3. Optionally fail fast: warn at startup when the environment is not Development and
     `AllowedHosts` is `*`. A warning, not a throw — an operator behind a trusted proxy
     that already filters hosts has a legitimate reason to leave it open.
- **Acceptance criteria:**
  - `.env.example` and `docker-compose.yml` expose the setting.
  - `docs/deployment.md` states the requirement.
  - A non-Development start with `AllowedHosts: "*"` logs a warning naming the setting.
  - Development is unaffected.
- **Validation:** Manual verification plus a startup-warning assertion in
  `tests/Soulsjwa.ApiTests/`.

---

### [BE-038] Decide whether self-join should work on a started event

- **Priority:** P3
- **Axis:** Correctness
- **Location:** `src/Soulsjwa.Api/Features/Events/Endpoints/EventCompetitorsEndpoint.cs:SelfJoin`
- **Problem:** `SelfJoin` checks that the event exists and that the caller is not already
  a competitor. It does not check `ev.IsStarted`. Any authenticated user can insert
  themselves into a running event's competitor list mid-competition, immediately
  appearing on the public scoreboard with a score of zero and a rank below everyone.

  Self-join itself is **intentional** — `docs/auth.md` states "any authenticated user can
  add themselves to an event as a competitor", `docs/feature-matrix.md` lists it as a
  shipped feature, and the allowlist means only vetted users have accounts at all. So the
  open enrolment is not the finding. Joining *after the event has started* is the
  question.
- **Why:** Low severity — the population is closed and an owner can remove a competitor.
  But a late joiner distorts `TotalCompetitors`, shifts `SharedPlace` rank assignment for
  everyone, and appears in the overlay mid-broadcast. It is worth an explicit decision
  rather than an accident of omission.
- **Change:**
  1. Decide, and state the decision in the PR: either late self-join is allowed (document
     it in `docs/auth.md` and add a test pinning it), or it returns 409 while
     `ev.IsStarted` is true, with a message directing the user to ask the owner to add
     them — `AddCompetitor` remains available to the owner for legitimate late additions.
  2. Recommend the 409: `AddGame`, `AddCustomGame` and `RemoveEventGame` already refuse
     while `ev.IsStarted`, so refusing roster changes mid-event is the established
     convention in this file's neighbours.
  3. Either way, add the missing archived-event guard — `SelfJoin` uses `db.Events`
     without `IgnoreQueryFilters()`, so an archived event already 404s correctly. Confirm
     with a test; no change expected.
- **API requirements:**
  - Endpoint: `POST /api/v1/events/{eventId}/competitors/self`.
  - Response: 409 while the event is started (if that option is chosen); 201 and 409-already-a-competitor unchanged otherwise. Declare `.ProducesProblem(StatusCodes.Status409Conflict)` — already declared.
  - Compatibility: the frontend's self-join button should be hidden or disabled for started events; update `src/Soulsjwa.Web/src/features/events/components/CompetitorsSection.tsx` in the same PR.
- **Acceptance criteria:**
  - The behaviour for a started event is explicit, tested, and documented in `docs/auth.md`.
  - Self-join into a not-started event still returns 201.
  - Self-join into an archived event returns 404.
- **Validation:** Extend `tests/Soulsjwa.ApiTests/EventCompetitorsEndpointTests.cs` with
  the started and archived cases.

---

### [BE-039] Return 401 rather than 500 for a malformed identity claim

- **Priority:** P3
- **Axis:** Maintainability
- **Location:** `src/Soulsjwa.Api/Features/Events/EventOwnership.cs:GetUserId`; the direct `Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!)` calls in `CompletedObjectivesEndpoint`, `CreateApiKeyEndpoint`, `DeleteApiKeyEndpoint`, `GetApiKeysEndpoint`, `GetCurrentUserEndpoint`
- **Problem:** Identity extraction throws on anything unexpected:

  ```csharp
  public static Guid GetUserId(ClaimsPrincipal principal) =>
      Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
  ```

  The doc comment says this "should never happen on an endpoint protected by
  `RequireAuthorization()`", and that is right for tokens this service issues.
  `JwtTokenService.GenerateAccessToken` always sets `sub`, and `ApiKeyAuthHandler`
  always sets `ClaimTypes.NameIdentifier`. But `Guid.Parse` on `null` throws
  `ArgumentNullException`, and on a non-GUID string throws `FormatException` — either
  way an unhandled 500. Five endpoints duplicate the same unguarded `Guid.Parse`
  inline rather than calling the helper.
- **Why:** No known path reaches it, so this is hardening plus deduplication rather than
  a bug fix. It matters because BE-002 adds an `OnTokenValidated` hook and any future
  change to claim mapping (for example, `MapInboundClaims` behaviour differing between
  the JWT and API-key schemes — which `IsAdmin` already has to work around by checking
  both `"role"` and `ClaimTypes.Role`) could make it reachable, and a 500 on an auth
  primitive is the wrong failure.
- **Change:**
  1. Add `EventOwnership.TryGetUserId(ClaimsPrincipal principal, out Guid userId)` and keep `GetUserId` as the throwing convenience for call sites that have already validated.
  2. Replace the five inline `Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!)` occurrences with `EventOwnership.GetUserId(principal)` so there is one implementation. `grep -rn "FindFirstValue(ClaimTypes.NameIdentifier)" src/Soulsjwa.Api --include=*.cs` finds them.
  3. Prefer a framework-level guard over per-endpoint checks: add a fallback authorization policy requiring a parseable `NameIdentifier`, or handle it in the `OnTokenValidated` hook BE-002 introduces, so a malformed principal fails authentication once rather than being re-checked in 40 handlers. Do not add a `TryGetUserId` call to every endpoint — that is noise for a case the auth layer should have rejected.
  4. While in `EventOwnership`: the `IsAdmin` dual-claim check (`FindFirstValue(RoleClaim) ?? FindFirstValue(ClaimTypes.Role)`) exists because the two authentication schemes emit role claims differently. That is a real inconsistency worth removing at the source — set `MapInboundClaims = false` on the JWT bearer options so `"role"` stays `"role"`, then drop the fallback. Verify against `tests/Soulsjwa.UnitTests/EventOwnershipTests.cs` and the API-key auth tests before removing either branch; if the tests show both forms are genuinely reachable, leave the fallback and document why.
- **Acceptance criteria:**
  - A request whose principal has a missing or non-GUID `NameIdentifier` is rejected with 401, not 500.
  - Exactly one implementation of user-id extraction exists in the codebase.
  - `IsAdmin` behaves identically for both JWT and API-key principals (existing tests prove it).
  - `grep -rn "Guid.Parse(principal" src/Soulsjwa.Api --include=*.cs` returns nothing.
- **Validation:** Extend `tests/Soulsjwa.UnitTests/EventOwnershipTests.cs` with malformed
  and missing claim cases. `EventOwnershipMembershipTests.cs`,
  `ApiKeyAuthHandlerAuthenticationTests.cs` and `PermissionsEndpointTests.cs` must pass
  unchanged. `Depends on: BE-002` if the `OnTokenValidated` route is taken.

---

## Database & Data Integrity

Consolidated view of every schema change this backlog asks for, so migrations can be
planned and ordered rather than discovered one task at a time.

### Migrations required

| Order | Migration | Task | Contents | Risk |
|---|---|---|---|---|
| 1 | `AddFeaturedEventUniqueIndex` | BE-010 | Data fix (`UPDATE "Events" SET "IsFeatured" = false` for all but the newest) then partial unique index `IX_Events_FeaturedEvent` on `("IsFeatured") WHERE "IsFeatured"` | **Fails if data is not fixed first** — the `UPDATE` must be in the same migration, before the `CreateIndex` |
| 2 | `AddOverlayTokenExpiry` | BE-005 | Nullable `ExpiresAt timestamptz` on `EventOverlayTokens` | Additive, safe |
| 3 | `AddRefreshTokenExpiresAtIndex` | BE-015 | `IX_RefreshTokens_ExpiresAt` on `("ExpiresAt")` | Index-only |
| 4 | `AddCalendarWindowIndexes` | BE-006 | `IX_CalendarEntries_StartsAt`, `IX_PlannedRuns_StartsAt` | Index-only |
| 5 | `AddEventSearchIndexes` | BE-019 | `CREATE EXTENSION pg_trgm` + GIN trigram on `Events."Name"` and `Events."Description"` + `IX_Events_CreatedAt` | **Needs elevated DB privileges for `CREATE EXTENSION`** — verify the role first or take the documented fallback |
| 6 | `AddUsersTwitchLoginLowerIndex` | BE-031 | Functional index on `LOWER("TwitchLogin")`, raw SQL, non-unique | Index-only |
| 7 | `ExtendAuditIndexesForKeyset` | BE-026 | Recreate `IX_AuditLogs_EventId_CreatedAt` → `("EventId","CreatedAt","Id")` and `IX_AuditLogs_CreatedAt` → `("CreatedAt","Id")`, `CONCURRENTLY`, `suppressTransaction: true` | Long-running on a large table; must not hold `ACCESS EXCLUSIVE` |
| 8 | `AddXminConcurrencyTokens` | BE-014 | Model annotation only — **inspect the generated `Up`/`Down` and confirm no `AddColumn`/`AlterColumn`** | If EF emits a real column, the mapping is wrong |

Migrations 2–8 are independent of each other and can land with their own tasks.
Migration 1 should go first because it repairs data that may already be invalid.

### Constraints and invariants

Enforced in the database today, and correct — **do not weaken any of these**:

- `IX_EventGames_EventId_ActiveGame` — at most one enabled game per event. BE-012 makes the write path respect it rather than depending on EF's statement ordering.
- `IX_CompletedObjectives_ObjectiveId_UserId_Official` / `…_TrialRunId`, and the `FailedObjectives` equivalents — the two-partial-index trick that works around Postgres treating NULLs as distinct. The comments in `AppDbContext` explain it well; leave them.
- `IX_TrialRuns_EventGameId_UserId` — one trial slot per competitor per game.
- `CK_Events_UrlAlias_Lowercase` — alias casing.
- `IX_Events_UrlAlias`, `IX_Users_TwitchId`, `IX_AllowlistedTwitchLogins_TwitchLogin`, `IX_MediaAssets_Sha256` — uniqueness.

Added by this backlog:

- `IX_Events_FeaturedEvent` (BE-010) — at most one featured event site-wide. This is the one genuine invariant currently defended only by application code.

### Transaction boundaries

Operations that must become atomic and are not today:

| Operation | Task | Scope |
|---|---|---|
| Refresh-token rotation | BE-004 | Conditional revoke + issue + cookie, one transaction; loser of the race gets 401 and mints nothing |
| Feature an event | BE-010 | Clear all + set one |
| Enable an event game | BE-012 | Clear others (explicit statement) + set one, in that order |
| Invite a competitor by handle | BE-024 | Placeholder user + allowlist entry + competitor row + both audits |

Already correct — `ConnectorEndpoint.SubmitGameData` and
`CompletedObjectivesEndpoint.CompleteObjective` both take
`pg_advisory_xact_lock` via `ObjectiveOutcomeLock` inside an explicit `ReadCommitted`
transaction, and `TwitchAuthService.UpsertUserAsync` uses `RepeatableRead` for the
bootstrap-admin path. Leave all three alone.

### Concurrency strategy summary

- **Conditional update** (`WHERE … AND NOT "IsRevoked"`, check rows-affected) — `RefreshTokens` (BE-004).
- **Unique index as arbiter**, violation translated to 409 — `Events.IsFeatured` (BE-010), `EventGames.IsEnabled` (BE-012), `Users.TwitchId` and `AllowlistedTwitchLogins.TwitchLogin` (BE-024).
- **Optimistic via `xmin`**, translated to 409 — the nine mutable entities in BE-014.
- **Advisory lock** — objective outcomes per event-game (existing); retention passes (BE-015).
- **Deliberately none** — `AuditLog` (insert-only), `EventCompetitor` / `EventCompetitorModerator` (composite PK is sufficient), `ApiKey`.

### Retention

`RefreshTokens` and `AuditLogs` currently grow forever (BE-015). Refresh tokens get a
hard policy with a grace window that preserves reuse detection; audit retention ships
**disabled by default** because it is the operator's policy call, not the code's.

---

## Security Hardening

Every security finding in one place, with the attack chain stated once. Nothing here is
theoretical unless it says so.

### Confirmed

| ID | Finding | Vector | Attacker needs | Impact |
|---|---|---|---|---|
| BE-001 | `"auth"` limiter is one shared bucket | 20 req/min to `/api/v1/auth/revoke` from one host | Nothing — endpoint is anonymous | Total login/refresh denial for all users |
| BE-002 | API keys survive de-allowlisting | Replay a retained `sk_` key | One key that was ever valid | Full account access indefinitely after deprovisioning; admin actions if the owner was an admin |
| BE-003 | Event-state gate skipped for archived events | Write to an archived event's objectives | Competitor membership in that event | Post-hoc mutation of archived results |
| BE-005 | Overlay token in the query string | Read logs, proxy logs, browser history, `Referer` | Log access, or any URL-recording intermediary | Read the event's full scoreboard; tokens never expire today |
| BE-011 | No request-body limit | 30 MB `Data` at connector cadence | One connector API key | Memory pressure, thread-pool starvation |
| BE-022 | No `Jwt:Secret` strength check | Deploy with a weak or empty secret | Operator error — `${JWT_SECRET}` unset expands to `""` and passes the presence check | Offline token forgery, including `"role": "Admin"` |

### Hardening (no known exploit path today)

| ID | Finding | Why it still matters |
|---|---|---|
| BE-018 | Archived events publicly readable and enumerable | Soft delete that hides nothing; may be intended — the task is to decide and document |
| BE-021 | Unvalidated `X-Correlation-Id` echoed and logged | Anonymous 500 trigger; unbounded attacker-controlled data into log storage |
| BE-037 | `AllowedHosts: "*"` | Defence in depth; no absolute URLs are built from the request host today |
| BE-039 | `Guid.Parse` on a claim throws | 500 on an auth primitive; not currently reachable |

### Verified sound — do not "fix"

Checked adversarially and found correct. Recorded so a future review does not re-open them:

- **IDOR / BOLA.** Every event-scoped endpoint resolves the child through its parent (`eg.EventId == eventId && eg.Id == eventGameId`) rather than trusting the child id alone. Changing an id in the URL yields 404, not another tenant's data. `EventOwnership` centralises the checks and a scripted sweep of every `MapPost`/`MapPut`/`MapPatch`/`MapDelete` found zero missing `RequireAuthorization()` — only `/api/v1/auth/refresh` and `/api/v1/auth/revoke` are anonymous, both intentionally cookie-authenticated.
- **SQL injection.** No raw SQL anywhere except `ObjectiveOutcomeLock`, which uses `ExecuteSqlInterpolatedAsync` — parameterised by construction.
- **Mass assignment / over-posting.** Request DTOs are explicit records listing only writable fields. The connector submission deliberately omits a user id and derives it from the credential, with a comment saying why.
- **File upload.** `ImageValidator` parses PNG/JPEG/WebP headers with `ReadOnlySpan<byte>` — no decoder, no pixel buffer, no native library, so no decompression-bomb surface. Dimensions capped at 4096×4096, only the first 64 KiB scanned, container sniffing separated from full detection so 415 and 400 are distinguishable, EXIF stripped by rewriting the container. Storage is content-addressed by SHA-256, so the uploaded filename never touches the filesystem — no path traversal. Uploads are admin-only. This is the strongest part of the codebase.
- **Open redirect.** `TwitchCallbackEndpoint` redirects only to `config["Frontend:Url"]`, never to a request-supplied value.
- **OAuth state.** `OAuthStateCookie` is signed+encrypted via DataProtection, single-use (deleted on every attempt, including failures), 10-minute window, compared in constant time, `SameSite=Lax` with a correct justification comment.
- **Token storage.** Refresh tokens, API keys and overlay tokens are all stored as SHA-256 hashes with a cleartext prefix for indexed lookup. Raw values are returned exactly once at creation.
- **CSP / security headers.** Deliberately tight, with `img-src` scoped to `self`, `data:` and Twitch's documented CDN rather than a blanket `https:`, and a comment forbidding widening it. `frame-ancestors 'none'`, `base-uri 'self'`, `form-action` scoped.
- **Forwarded headers.** `ForwardLimit = 1` with `KnownProxies`/`KnownIPNetworks` cleared, and a comment explaining that a higher limit would let clients spoof their own IP. Correct, and load-bearing for BE-001's fix.
- **CSRF.** The refresh cookie is `SameSite=Strict`, `HttpOnly`, `Secure`, `Path=/api/v1/auth`. CORS allows one configured origin with credentials. The `revoke` endpoint is reachable cross-site only to the extent `Strict` allows, which is not at all.

### Sequencing

Do BE-001 and BE-002 first and ship them together — they are the two findings that
would be called out in an external assessment. BE-022 is a one-line startup guard that
can ride along in the same PR. BE-003 next, because it is the one that silently corrupts
data rather than merely exposing it.

---

## Cross-Cutting Refactors

Three refactors that each subsume several individual findings. Doing the refactor is
strictly better than doing the findings separately, because it removes the shape that
produced them.

### [XC-1] Extract the endpoint preamble — subsumes BE-003, and de-risks BE-012, BE-018, BE-024

**The pattern.** Roughly thirty handlers open with some subset of:

```csharp
var ev = await db.Events.FirstOrDefaultAsync(e => e.Id == eventId, ct);
if (ev is null) return Results.Problem(detail: "Event not found.", statusCode: 404);
if (EventOwnership.RequireOwner(ev, principal, "…") is { } ownerError) return ownerError;
var eventGame = await db.EventGames.FirstOrDefaultAsync(eg => eg.EventId == eventId && eg.Id == eventGameId, ct);
if (eventGame is null) return Results.Problem(detail: "Game is not part of this event.", statusCode: 404);
```

Copied by hand each time. The copies have **drifted**: some load the event twice
(`ev` and `ev2`), some use `IgnoreQueryFilters()` and some do not, some check
`IsStarted` and some skip it, the not-a-competitor case is 403 in one file and 404 in
another, and five sites acquired the `is not null &&` mutation that is BE-003.

**The refactor.** Add `src/Soulsjwa.Api/Features/Events/EventContext.cs` — a small set of
loader functions returning `(entities…, IResult? Error)` tuples, in the same style
`TrialRunsEndpoint.LoadContextAsync` already uses locally (that method is the proof this
works; the task is to promote it and use it everywhere):

- `RequireEventAsync(eventId, db, ct)` — event or 404.
- `RequireRunningEventAsync(eventId, db, ct)` — event, or 404 archived / 403 not started (BE-003).
- `RequireOwnedEventAsync(eventId, principal, db, ct)` — the above plus `RequireOwner`.
- `RequireEventGameAsync(eventId, eventGameId, db, ct)` — both entities or 404.

Migrate handlers one feature folder at a time, smallest first
(`EventRulesEndpoint`, `CalendarEntriesEndpoint`) to establish the shape, then the large
ones. **Do not** introduce a base class, a filter pipeline, or an
`IEndpointFilter` — a static loader returning a tuple is the smallest thing that removes
the duplication, and it keeps every handler's control flow readable and debuggable.

**Payoff.** Removes ~200 lines. Makes the event-state gate a single implementation that
cannot be half-applied. Makes the 403/404 choice a single decision. Gives BE-018 one
place to enforce archived-event visibility instead of two.

**Order.** Do BE-003 as a targeted fix first (it is P1 and should not wait), then this
refactor generalises it. `Depends on: BE-003`.

### [XC-2] Centralise route strings and policy names — subsumes part of BE-001, BE-020, BE-021

`/api/v1/...` appears as a literal in every endpoint's `MapGroup`/`Map*` call — over
twenty occurrences. `"auth"` and `"connector"` appear as literals at both their
definition in `Program.cs` and their `RequireRateLimiting` call sites. `"X-Correlation-Id"`
appears in both `CorrelationIdMiddleware` and `Program.cs`. `"X-Api-Key"` appears in
`ApiKeyAuthHandler` (as a private const, correctly) and again as a literal in
`Program.cs`'s `ForwardDefaultSelector` and in the Swagger security definition.

This is exactly what `AGENTS.md` §4 forbids, and the `X-Api-Key` case is the dangerous
kind: the scheme-selection logic in `Program.cs` and the handler that reads the header
are in different files, each with its own copy of the string. A typo in one silently
disables API-key authentication.

Add `src/Soulsjwa.Api/Common/ApiRoutes.cs` (prefix and per-feature group builders) and
`src/Soulsjwa.Api/Common/RateLimitPolicies.cs`. Make `ApiKeyAuthHandler.HeaderName` and
`CorrelationIdMiddleware.HeaderName` `public const` and use them at every call site. Do
this incrementally — it touches many files and should not land in the same PR as a
behavioural fix.

### [XC-3] One read-path strategy for aggregates — subsumes BE-006, BE-007, BE-019, BE-030

Four endpoints share one anti-pattern: `Include` the object graph, materialise it, then
project in memory to something much smaller. `GlobalCalendarEndpoint` does it with no
filter at all; `MyEventsEndpoint` does it via `BuildBatchAsync`; `ListEvents` does it
with four `Include` chains for a list view; `ListPredefined` does it unpaginated.

The rule to adopt, and to write into `docs/agent-conventions/`:

> A read endpoint that returns a projection must `Select` into that projection in the
> query. `Include` is for endpoints that return the full graph. Every list endpoint is
> bounded — by pagination, by a required filter, or by an explicit cap.

Applying it uniformly is worth more than the four fixes individually, because it stops
the next list endpoint from being written the same way. Treat BE-006 and BE-007 as the
urgent instances and this as the convention they establish.

---

## Verification Plan

### Per-task

Every task above names its own tests. Those are the contract — a task is not done until
its Acceptance criteria are demonstrably met by an automated test, not by inspection.

### Before each PR

```bash
dotnet build Soulsjwa.slnx
dotnet test  Soulsjwa.slnx
dotnet format Soulsjwa.slnx --verify-no-changes
```

If the change touches a shared contract in `Soulsjwa.Shared` or any response DTO the
frontend consumes, also, from `src/Soulsjwa.Web/`:

```bash
npm ci && npm run lint && npm run build && npm test
```

Never run the frontend and backend builds concurrently — `AGENTS.md` §2 warns that they
conflict over `wwwroot/`.

### Tests that must keep passing untouched

These encode behaviour this backlog does not intend to change. If one of them fails,
the change went further than the task allowed:

- `tests/Soulsjwa.UnitTests/EventOwnershipTests.cs`, `EventOwnershipMembershipTests.cs` — the permission matrix.
- `tests/Soulsjwa.UnitTests/ImageValidatorTests.cs`, `ImageValidatorMetadataStrippingTests.cs` — upload safety.
- `tests/Soulsjwa.UnitTests/ScoreboardRankingTests.cs` — tie-break semantics, which BE-007 must reproduce exactly.
- `tests/Soulsjwa.UnitTests/RuleEvaluatorEdgeCaseTests.cs` — BE-008's validator must not reject anything this file says is evaluable, and BE-016's parsed overloads must agree with the string ones.
- `tests/Soulsjwa.IntegrationTests/ActiveGameConstraintTests.cs` — BE-012.
- `tests/Soulsjwa.ApiTests/SecurityHeadersTests.cs` — CSP and header hardening.
- `tests/Soulsjwa.ApiTests/PermissionsEndpointTests.cs` — the cross-endpoint authorization sweep.

### New test infrastructure this backlog needs

1. **An in-memory Serilog sink** in `tests/Soulsjwa.ApiTests/`, shared by BE-005, BE-021 and BE-032. Build it once as a fixture rather than three times.
2. **A `DbCommandInterceptor` that counts commands**, shared by BE-006, BE-007, BE-019, BE-023 and BE-027. Query-count assertions are how "this no longer N+1s" is proven; without it those acceptance criteria are unverifiable.
3. **Real-Postgres concurrency tests.** BE-004, BE-010, BE-012, BE-014 and BE-024 all need genuine concurrent transactions. The InMemory provider cannot express row locks, partial indexes, or `xmin`. `tests/Soulsjwa.IntegrationTests/IntegrationTestBase.cs` already exists — extend it with a helper for "run these two operations concurrently against separate DbContexts".
4. **An `EXPLAIN ANALYZE` assertion helper** for BE-006, BE-019, BE-026 and BE-031. Asserting "no `Seq Scan` on this table" is the only way to prove an index change worked.

### Suggested PR grouping

| PR | Tasks | Rationale |
|---|---|---|
| 1 | BE-001, BE-002, BE-022 | The security set. Ship together, ship first. |
| 2 | BE-003 | Data integrity, standalone, small. |
| 3 | BE-004, BE-015 | Refresh-token lifecycle end to end. |
| 4 | BE-008, BE-011 | Input validation on the two write paths that accept opaque payloads. |
| 5 | BE-006, BE-009 | The two anonymous read-path fixes plus the cache scoping they depend on. |
| 6 | BE-007, BE-016, BE-027 | The aggregate/parse performance set. |
| 7 | BE-010, BE-012, BE-014, BE-024 | All the concurrency work, sharing the new test infrastructure. |
| 8 | BE-005, BE-013, BE-017, BE-018 | Mixed correctness and hardening. |
| 9 | XC-1 | The refactor, once BE-003 has settled the semantics. |
| 10+ | Remaining P2/P3, XC-2, XC-3 | Incremental. |

### Definition of done for the whole backlog

- All P0 and P1 tasks merged with their tests.
- No endpoint returns a bare-string error body.
- No list endpoint is unbounded.
- Every schema invariant that the code enforces is also enforced by a constraint, or has a written justification for why it is not.
- `docs/` is in sync per the `AGENTS.md` §6 table — in particular `database-design.md` (eight migrations), `api-reference.md` (rate limits, error shapes, new parameters), `auth.md` (archived visibility, self-join, allowlist semantics), `system-overview.md` (Twitch resilience), `deployment.md` (secrets, retention, `AllowedHosts`, the single-instance output-cache constraint).

---

## Deferred / Rejected Suggestions

**The implementation agent must NOT implement anything in this section.** Each was
considered and rejected for the stated reason. If a future change makes one of these
worth revisiting, that is a new decision with new evidence — not a licence to do it now.

### Architecture — rejected

- **Clean Architecture / Domain-Application-Infrastructure project split.** The feature-folder layout under `Features/` already gives cohesion, and dependencies already point inward through `EventOwnership` and the service classes. Splitting into projects would add three csproj files, a mapping layer, and interface indirection to solve a coupling problem this codebase does not have.
- **CQRS / MediatR.** Minimal-API handlers that take `AppDbContext` directly are the simplest thing that works here. A mediator would add a request record, a handler class and a registration per endpoint — roughly 120 new types — to replace method calls that are already one level deep and directly testable.
- **A repository layer over EF Core.** `DbSet<T>` *is* the repository. Wrapping it would obstruct the projections XC-3 depends on and the `ExecuteUpdateAsync`/`ExecuteDeleteAsync` calls BE-004 and BE-012 need.
- **Removing `IMediaStore` or `IAuditService`.** Both have one implementation, which normally makes an interface suspect. `IMediaStore` is a genuine filesystem seam that `MediaStoreTests` exercises; `IAuditService` is registered as a singleton and mocked in `AuditServiceTests`. Both earn their keep. Deleting them would be simplification for its own sake.
- **Splitting the long endpoint files.** `EventsEndpoint.cs` (683 lines) and `ObjectivesEndpoint.cs` (582) are long, but they are long because they group related handlers, and each handler is short. XC-1 removes the duplicated preamble, which is the part that actually hurts. Splitting by file count would just scatter cohesive code.

### Correctness — deferred

- **Converting every entity `DateTime` to `DateTimeOffset`.** BE-013 fixes the boundary, which is where the bug is. Changing the storage type means a migration across nine tables for no behavioural gain — every stored value is already a UTC instant.
- **Event sourcing the scoreboard.** `AuditLogs` already records what happened; recomputing scores from an event stream would be a rewrite of the scoring path to solve a problem (auditability) that is already solved.
- **Serializable isolation on the connector submit path.** The advisory lock plus partial unique indexes already prevent the anomalies that matter, at far lower cost than serialization failures and retries on the highest-frequency write path.

### Security — rejected as unnecessary

- **Replacing symmetric JWT signing with RS256/asymmetric keys.** There is one issuer and one audience, both this service. Asymmetric signing buys key distribution the system does not need, and adds key management. BE-022's strength check is the right-sized fix.
- **Scoping API keys to specific endpoints.** Tempting — a connector key only needs `submit` — and it would reduce BE-002's blast radius. Rejected **for now** because it is a schema change plus an authorization-policy redesign, and BE-002's actual fix (revalidate the owner, revoke on de-allowlist) closes the exploitable gap at a fraction of the cost. Revisit if API keys are ever issued to third parties rather than to the first-party connector.
- **Rotating `Jwt:Secret` / supporting multiple signing keys.** No key-rotation requirement has been stated. `BuildValidationParameters` accepts a single `IssuerSigningKey`; adding `IssuerSigningKeys` plus a rollover window is real work with no current driver.
- **Rate limiting by API key rather than by user.** The global limiter already partitions on `ClaimTypes.NameIdentifier`, which an API key populates. A separate key-level partition would let one user multiply their budget by minting keys — worse, not better.
- **Adding a Content-Security-Policy nonce.** The production CSP is already `script-src 'self'` with no `unsafe-inline`. A nonce would add complexity to a policy that is already strict.

### Performance — deferred

- **Redis or any distributed output cache.** BE-009 documents the single-instance constraint rather than removing it. Introducing Redis means a new dependency, new failure modes, and DataProtection key-ring changes — worth it when the deployment actually scales out, not before. The documentation BE-009 adds is what makes that decision possible later.
- **Materialised view for the scoreboard.** BE-007 and BE-009 should be measured first. A materialised view adds refresh scheduling and staleness semantics to a problem that projection and correct cache scoping may fully solve.
- **Replacing JsonLogic.Net to unify on System.Text.Json.** The library works, is exercised by `RuleEvaluatorEdgeCaseTests`, and its rule format is persisted in `jsonb` columns and understood by the frontend's Blockly editor. Replacing it risks silent evaluation differences in live scoring to remove one dependency. BE-016 gets the performance win without touching the evaluator's semantics.
- **Compiled EF queries.** Micro-optimisation. The wins in this backlog are query *shape*, not query compilation.

### Maintainability — rejected

- **Introducing FluentValidation.** Validation is currently hand-rolled into `Dictionary<string, string[]>` and returned via `Results.ValidationProblem` — consistent, dependency-free, and readable. A validation library would add a package and a validator class per DTO to replace code that is already short. The real inconsistency is the *error shape*, which BE-028 fixes directly.
- **AutoMapper or any mapping library.** The explicit `MapToResponse` methods are clearer than configured conventions, and XC-3 makes several of them into EF projections, which mapping libraries handle poorly.
- **Renaming `ev2` and similar locals as a standalone task.** They disappear as a side effect of BE-003 and XC-1. A rename-only PR is churn.
- **Backfilling tests for untested endpoints as one task.** Coverage is already good (61 test files). Each task above adds the tests its change needs, which is better targeted than a coverage sprint.

### Explicitly not a finding

- **Open self-join.** `docs/auth.md` and `docs/feature-matrix.md` both document it as intended, and the allowlist means only vetted users have accounts. BE-038 asks only about the *started-event* case, not about self-join itself.
- **`RuleEvaluator` returning `false` on a malformed rule.** This is deliberate and correct at evaluation time — a live connector submission must not 500 because an objective has a bad rule. BE-008 moves the rejection to *write* time, where it belongs, and leaves this behaviour intact as the backstop.
- **`GetEvent` and `ListEvents` using `IgnoreQueryFilters()`.** Flagged as BE-018, but as a *decision to document* rather than a confirmed vulnerability — it may well be the intended product behaviour, and the task says so.
- **The bootstrap-admin `RepeatableRead` transaction in `TwitchAuthService`.** The comment slightly overstates what `RepeatableRead` guarantees (it prevents write-write conflicts on rows actually read, not phantom inserts). In practice the path is safe: two concurrent logins can only both self-promote if they share a Twitch login, and `Users.TwitchId` is unique, so one fails. Not worth changing; noted here so a future reviewer does not re-derive it.
