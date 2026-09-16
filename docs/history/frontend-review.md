# Frontend Review TODO

> **Status: completed.** Every item below was implemented (PRs #200, #203 and
> follow-ups; commit subjects carry the `FE-nnn` ids). Kept for the reasoning
> it records, not as a backlog. See [README.md](README.md).


## Summary

### Overall assessment

`src/Soulsjwa.Web` is a well-organised React 19 + TanStack Query codebase. The
feature-folder layout (`features/<area>/{api,hooks,components}`), the one-hook-per-file
rule, the `*_QUERY_KEYS` convention and the `const`-tuple enum mirrors from
`AGENTS.md` §4 are followed consistently. Tests are real (290 passing across 59
files), the Markdown pipeline is genuinely safe, and the route-level code
splitting is done properly.

The problems are concentrated in three places, and they are the same three
places in every axis:

1. **The auth/session layer** has two independent, uncoordinated refresh paths
   against a backend that treats a replayed refresh token as proof of theft and
   revokes the user's entire token family. This is a real session-destroying
   bug, not a theoretical one.
2. **The server-state layer** has ~40 near-identical mutation hooks whose
   invalidation is ad hoc: over-broad in one direction (`['events', eventId]`
   prefix-matches every sub-query) and full of redundant no-op calls in the
   other. No list query uses `keepPreviousData`, which makes the Events search
   box unusable.
3. **The overlay** (`OverlayPage.tsx`, 832 lines) carries a genuine effect-cleanup
   bug, a CSS-injection vector, and a chunk of dead code.

There is comparatively little over-abstraction to tear out — the main
simplification wins are collapsing the mutation-hook boilerplate and the
103-line `lazyPages.tsx`, not deleting layers.

### 5-axis scores

| Axis | Score | Notes |
|---|---|---|
| Architecture & Design | 7 / 10 | Clean boundaries; server-state layer is the weak spot. `useEvent` has a mixed return contract and a request waterfall. |
| Correctness & Edge Cases | 5 / 10 | One session-destroying auth race, one broken effect cleanup, one unusable search input, one feature saved but never applied. |
| Performance | 6 / 10 | Good route splitting and vendor chunking. Undermined by double-rendered scoreboards, un-debounced search, over-broad invalidation, and background polling. |
| Security & Production Reliability | 7 / 10 | Markdown/XSS handled correctly and deliberately. Lost points for the refresh-token race and an unvalidated CSS value. |
| Maintainability & Simplification | 7 / 10 | Strong conventions and comments. ~40 copy-pasted mutation hooks and 103 lines of lazy-import boilerplate are the debt. |

### Task counts

| Priority | Count |
|---|---|
| P0 | 1 |
| P1 | 6 |
| P2 | 8 |
| P3 | 6 |
| **Total** | **21** |

### Biggest risk

**FE-001.** `lib/axios/apiClient.ts` and `lib/axios/tokenManager.ts` each POST
`/api/v1/auth/refresh` independently, with no shared in-flight promise and no
cross-tab coordination. The backend
(`src/Soulsjwa.Api/Infrastructure/Auth/JwtTokenService.cs:119-124`) rotates the
refresh token on every call and, on seeing a revoked-but-unexpired token,
calls `RevokeAllForUserAsync` — logging the user out of every device. Two
browser tabs whose proactive timers fire within the same window is enough to
trigger it. This is the one finding that silently destroys user state in
production.

### Biggest simplification opportunity

**FE-006 / FE-021.** ~40 mutation hooks under `features/events/hooks/` are
three-line copies of each other that differ only in which keys they invalidate —
and roughly half of those invalidations are provably no-ops, because
`EVENTS_QUERY_KEYS.detail(id)` is `['events', id]` and TanStack Query matches by
prefix. Collapsing these onto one documented invalidation helper deletes the
most code, removes the most inconsistency, and fixes a real refetch storm at the
same time.

---

## Scorecard

| # | ID | Priority | Axis | Area | One-line task |
|---|---|---|---|---|---|
| 1 | FE-001 | P0 | Security | auth | Single-flight + cross-tab lock for `/auth/refresh` |
| 2 | FE-002 | P1 | Correctness | events list | Stop remounting the Events filter bar on every keystroke |
| 3 | FE-003 | P1 | Correctness | overlay | Fix highlight that never clears |
| 4 | FE-004 | P1 | Correctness | theme | Apply the site background image/treatment, or remove the controls |
| 5 | FE-005 | P1 | Architecture | events | Collapse `useEvent`'s double fetch and mixed return shape |
| 6 | FE-006 | P1 | Architecture | server state | Consolidate event cache invalidation |
| 7 | FE-007 | P1 | Correctness | auth | Make `tokenStore` reactive via `useSyncExternalStore` |
| 8 | FE-008 | P2 | Security | overlay | Validate the `bg` query parameter |
| 9 | FE-009 | P2 | Performance | scoreboard | Render cards **or** table, not both |
| 10 | FE-010 | P2 | Performance | users | Debounce the user search |
| 11 | FE-011 | P2 | Performance | my-events | Stop background polling in hidden tabs |
| 12 | FE-012 | P2 | Correctness | my-events | Fix optimistic toggle vs. poll race |
| 13 | FE-013 | P2 | Correctness | auth | Remove the redundant post-bootstrap refresh |
| 14 | FE-014 | P2 | Reliability | theme | Guard `createAppTheme` against malformed colours |
| 15 | FE-015 | P2 | Security | pwa | Clear the service-worker cache on logout |
| 16 | FE-016 | P3 | Maintainability | overlay/auth | Delete dead code |
| 17 | FE-017 | P3 | Maintainability | routing | Collapse `lazyPages.tsx` boilerplate |
| 18 | FE-018 | P3 | Performance | dnd | Stabilise `useDragReorder` row refs |
| 19 | FE-019 | P3 | Maintainability | audits | Drop the pointless `useMemo` in audit hooks |
| 20 | FE-020 | P3 | Maintainability | overlay | Extract `OverlayPage` view components |
| 21 | FE-021 | P3 | Maintainability | server state | Collapse mutation-hook boilerplate |

---

## P0 — Critical

### FE-001 Serialize `/auth/refresh` across callers and browser tabs

- **Priority:** P0
- **Axis:** Security / Correctness
- **Location:** `src/Soulsjwa.Web/src/lib/axios/tokenManager.ts:performRefresh`, `src/Soulsjwa.Web/src/lib/axios/apiClient.ts:refreshAuthLogic`
- **Problem:** Two independent code paths POST `/api/v1/auth/refresh`:
  - `apiClient.ts:27` — reactive, driven by `axios-auth-refresh` on a 401.
  - `tokenManager.ts:21` — proactive, driven by a `setTimeout` scheduled 60 s
    before token expiry.

  `deduplicateRefresh: true` only dedupes *within* the `axios-auth-refresh`
  interceptor. It does not know about `tokenManager`'s raw `axios.post`, and
  neither knows about other browser tabs. There is no shared in-flight promise
  (`grep BroadcastChannel|navigator.locks` over `src/` returns nothing).

  The backend rotates the refresh token on every single call
  (`RefreshTokenEndpoint.cs:37-44`: validate → `RevokeRefreshTokenAsync` → issue
  new) and treats a revoked-but-unexpired token as theft
  (`JwtTokenService.cs:119-124` → `RevokeAllForUserAsync`).

  So any two overlapping refreshes — two tabs whose timers fire together, a
  proactive refresh racing a 401-triggered one, or a request in flight when the
  timer fires — send the same cookie twice. The second is classified as reuse
  and **every refresh token for that user is revoked**, logging them out
  everywhere with no explanation.
- **Why:** Silent, unattributable, full-session logout across all of a user's
  devices. It gets *more* likely the more a user uses the app (more tabs, longer
  sessions), and it is invisible in frontend logs because the client just sees a
  401. This is the single highest-severity defect in the frontend.
- **Change:**
  1. Add `src/Soulsjwa.Web/src/lib/axios/refreshSession.ts` exporting one
     `refreshSession(): Promise<string>`. Hold a module-level
     `let inFlight: Promise<string> | null`. On call: if `inFlight` is non-null,
     return it; otherwise start the POST, assign it to `inFlight`, and clear
     `inFlight` in a `.finally()`. This is the single-flight guarantee.
  2. Wrap the network call in a cross-tab lock when available:
     `navigator.locks?.request('soulsjwa:auth-refresh', cb)`, falling back to
     calling `cb()` directly when the Web Locks API is absent. Inside the lock,
     re-check whether another tab already stored a newer token before issuing
     the request (see step 3).
  3. Broadcast the outcome so sibling tabs adopt the new token instead of
     refreshing on their own: post `{ accessToken }` on a
     `BroadcastChannel('soulsjwa:auth')` after a successful refresh, and on
     failure post a `signed-out` message. Subscribe in `tokenManager.start()`;
     on receiving a token, call `tokenStore.setAccessToken` and reschedule
     without a network call.
  4. Rewrite `tokenManager.performRefresh`, `tokenManager.bootstrap` and
     `apiClient.refreshAuthLogic` to all delegate to `refreshSession()`. Delete
     the duplicated `axios.post('/api/v1/auth/refresh', …)` bodies — there must
     be exactly one in the codebase after this change.
  5. Fix the timer leak in `scheduleNext` (`tokenManager.ts:39-50`): it assigns
     `scheduleHandle` without clearing a previously pending handle, so two
     overlapping `bootstrap()` calls (React 19 StrictMode double-invokes the
     effect in `AppProviders.tsx:27-30`) leave an orphaned timer that later
     fires an extra refresh. Call `tokenManager.stop()` at the top of
     `scheduleNext`.
  6. Add jitter of 0–5 s to the proactive delay so tabs that *do* fall back to
     the no-Web-Locks path don't fire in lockstep.
- **Acceptance criteria:**
  - Exactly one `axios.post('/api/v1/auth/refresh', …)` call site exists in
    `src/`.
  - Invoking `refreshSession()` 5 times concurrently issues exactly **one**
    HTTP request and resolves all 5 callers with the same token.
  - A proactive refresh firing while a 401-triggered refresh is in flight issues
    one request, not two.
  - `scheduleNext()` called twice in a row leaves exactly one live timer.
  - Two simulated tabs sharing a `BroadcastChannel` mock perform one network
    refresh between them; the second adopts the broadcast token.
- **Validation:**
  - New unit test `src/lib/axios/refreshSession.test.ts` with a mocked `axios`
    asserting the call count under concurrency (cases above).
  - Extend `src/lib/axios/tokenStore.test.ts` (or add `tokenManager.test.ts`)
    with fake timers asserting no orphaned timer after a double `scheduleNext`.
  - `npm test && npm run lint && npm run build` from `src/Soulsjwa.Web/`.
  - Manual: open two tabs, sign in, leave both until the proactive refresh
    fires, confirm both stay signed in and the API logs show no
    `JwtRefreshTokenReuseDetected` event (event id 1104).

---

## P1 — High Priority

### FE-002 Stop remounting the Events filter bar on every keystroke

- **Priority:** P1
- **Axis:** Correctness / Performance
- **Location:** `src/Soulsjwa.Web/src/pages/EventsPage.tsx:68-70`, `src/Soulsjwa.Web/src/features/events/hooks/useEvents.ts`
- **Problem:** `useEvents` builds its query key from `search`, and `EventFilters`
  calls `onSearchChange` on every `onChange` with no debounce. Each keystroke
  therefore produces a brand-new query key with no cached data, so
  `isLoading === true`, and `EventsPage.tsx:68` returns `<CardListSkeleton />`
  **before** `<EventFilters>` is rendered (line 114).

  The consequence: the entire page — including the search `TextField` the user is
  typing into — unmounts and remounts on every character. The input loses DOM
  focus after each keystroke and the page flashes a skeleton. The same applies to
  the pagination buttons.

  `useEvents` is also the only list hook with no `placeholderData`; the codebase
  uses `keepPreviousData` in exactly one place (`useUserSearch.ts:15`).
- **Why:** Search on the primary Events page is effectively unusable — a user
  cannot type a second character without re-clicking the field. It also fires one
  request per keystroke.
- **Change:**
  1. In `useEvents.ts`, add `placeholderData: keepPreviousData` (import from
     `@tanstack/react-query`). This alone keeps the previous page's data visible
     and makes `isLoading` false on subsequent keys.
  2. In `EventsPage.tsx`, move the loading gate below the header and filter bar:
     render `<PageHeader>` and `<EventFilters>` unconditionally, and use the
     skeleton only in place of the results grid. Keep `isError` handling in the
     same position.
  3. Debounce the value that reaches the query key, not the input. Add a small
     shared hook `src/Soulsjwa.Web/src/lib/useDebouncedValue.ts`
     (`useDebouncedValue<T>(value: T, delayMs: number): T`, `setTimeout` +
     cleanup). In `EventsPage`, keep `search` as immediate controlled input state
     and pass `useDebouncedValue(search.trim(), 300)` to `useEvents`.
  4. Reset `page` to 1 when the *debounced* value changes, not on every keypress,
     so paging isn't reset mid-type.
- **Acceptance criteria:**
  - Typing `souls` into the search field leaves focus in the field for all five
    characters.
  - The filter bar and page header stay mounted while a search request is in
    flight; only the results region shows a loading state.
  - Typing `souls` at normal speed issues **one** request, not five.
  - Paging from page 1 to page 2 shows the previous page's rows until the new
    page arrives, with no skeleton flash.
- **Validation:**
  - New test `src/pages/EventsPage.test.tsx` using `@testing-library/user-event`:
    type into the search box and assert `document.activeElement` is still the
    input, and that the query fired once after advancing fake timers.
  - `npm test`, then manual check at `/events`.

### FE-003 Fix the overlay highlight that never clears

- **Priority:** P1
- **Axis:** Correctness
- **Location:** `src/Soulsjwa.Web/src/pages/OverlayPage.tsx:162-207` (`useChangeHighlights`)
- **Problem:** The effect schedules two timers — `addHandle` (adds ids to
  `active`) and `removeHandle` (removes them after `durationSeconds`) — and its
  cleanup clears both. But the effect returns early at line 180
  (`if (newlyFlashed.length === 0) return`) **before** scheduling any timers.

  So when `entries` changes again with no *new* completions, the cleanup cancels
  the still-pending `removeHandle` from the previous run and the early return
  never schedules a replacement. The ids stay in `active` forever and those rows
  stay visually flashed for the rest of the stream.

  This fires whenever any second change lands inside the highlight window — and
  `ScoreboardEntry.totalInGameTimeMs` (`types/index.ts:318`) ticks while a run is
  live, so `entries` changes on essentially every poll. With the shipped defaults
  (`refresh=5`, `highlightSeconds=6` — `overlayConfig.ts:118,122`) the un-highlight
  is cancelled before it can ever run.
- **Why:** The overlay is on-stream, public-facing output. Permanently
  highlighted rows make the "someone just completed something" signal meaningless
  and look like a rendering bug to viewers.
- **Change:** Stop storing highlight expiry in a timer whose lifetime is tied to
  the effect. Store a deadline per id instead:
  1. Change the state to `Map<string, number>` of `userId → expiresAtMs`.
  2. In the effect, merge newly-flashed ids with `Date.now() + durationSeconds * 1000`.
     Do not return early before scheduling — always (re)schedule a single timer
     for the *earliest* future deadline in the map.
  3. On that timer, drop every entry whose deadline has passed and reschedule for
     the next one, if any.
  4. Return the derived `Set` of ids whose deadline is still in the future, so the
     public signature of `useChangeHighlights` is unchanged and
     `OverlayPage.tsx:608` needs no edit.
  5. Keep the existing deferred-`setState` behaviour (the `setTimeout(…, 0)` at
     line 183 exists to satisfy `react-hooks/set-state-in-effect`).
- **Acceptance criteria:**
  - A row flashed at `t=0` with `highlightSeconds=6` un-flashes at `t≈6 s` even
    when `entries` changes at `t=1,2,3,4,5`.
  - Two rows flashed at different times each un-flash on their own schedule.
  - No timer leaks: unmounting the hook clears the pending timer.
  - Default config (`refresh=5`, `highlightSeconds=6`) clears highlights.
- **Validation:**
  - New test `src/pages/OverlayPage.test.tsx` (or a dedicated
    `useChangeHighlights.test.tsx`) with `vi.useFakeTimers()`: rerender with
    changed-but-not-newly-completed entries during the window and assert the
    highlight clears on schedule.
  - `npm test`; manual check by loading an overlay URL against a live event.

### FE-004 Apply the site background image/treatment, or remove the controls

- **Priority:** P1
- **Axis:** Correctness / Architecture
- **Location:** `src/Soulsjwa.Web/src/theme/theme.ts:createAppTheme`, `src/Soulsjwa.Web/src/features/theme/siteTheme/components/SiteThemeEditor.tsx:91-115`, `src/Soulsjwa.Web/src/theme/ThemeModeProvider.tsx:76-80`
- **Problem:** The admin theme editor exposes a background-image upload and a
  `backgroundTreatment` selector, serialises both into `UpdateSiteThemeRequest`
  (`SiteThemeEditor.tsx:27-28`) and persists them. But nothing ever reads them
  back for rendering: `ThemeModeProvider` passes only
  `resolveSiteThemePalette(...)` and `SITE_FONT_STACKS[...]` into
  `createAppTheme`, and `createAppTheme` consumes only the six palette slots and
  `fontFamily`.

  A repo-wide grep for `backgroundTreatment|backgroundUrl|backgroundAssetId`
  outside `types/index.ts` returns only the editor itself. The admin uploads an
  image, picks a treatment, saves successfully — and the site looks identical.
- **Why:** A settings screen that reports success and changes nothing is a
  correctness bug and a support burden. It also violates `AGENTS.md` §3.11 (no
  half-implemented features). The implementing agent must pick one of the two
  honest resolutions rather than leave it as-is.
- **Change:** Prefer **(a)**; fall back to **(b)** only if the intended visual
  treatment is genuinely unspecified.

  **(a) Implement it.** In `ThemeModeProvider`, read `siteTheme.backgroundUrl`
  and `siteTheme.backgroundTreatment` and apply them to the app background —
  the natural seam is the existing effect at `ThemeModeProvider.tsx:82-86` that
  already writes `document.documentElement.style.backgroundColor`. Map each
  member of `BACKGROUND_TREATMENTS` to concrete `background-size` /
  `background-repeat` / `background-position` values in a
  `BACKGROUND_TREATMENT_STYLES: Record<BackgroundTreatment, …>` map placed next
  to `SITE_FONT_STACKS` in `paletteMapping.ts` (per `AGENTS.md` §4, no magic
  strings). **Do not** build a CSS string and inject a `<style>` tag — that would
  break the guarantee asserted by
  `features/theme/siteTheme/noRawHtml.test.ts`. Set discrete style properties, and
  build the `url(...)` value with `CSS.escape`-equivalent quoting so an unexpected
  URL cannot break out of the declaration.
  `OverlayPage` must be unaffected: it already overrides `html, body, #root`
  backgrounds at `OverlayPage.tsx:508` for OBS transparency — verify that still
  wins.

  **(b) Remove it.** Delete the `ImageUploadField` and treatment `Select` from
  `SiteThemeEditor`, drop `backgroundAssetId`/`backgroundTreatment` from
  `toRequest`, delete `toUploadStub`, and note the removal in
  `docs/frontend.md` + `docs/feature-matrix.md`.
- **Acceptance criteria:**
  - (a) Uploading a background as an admin visibly changes the site background
    for all visitors after reload; each treatment option produces a visibly
    distinct result; the overlay route remains transparent.
  - (a) `noRawHtml.test.ts` still passes unchanged (no `<style>` string
    building, no `dangerouslySetInnerHTML`).
  - (b) No UI control exists for a value that is not rendered.
  - Either way: `docs/frontend.md` and `docs/feature-matrix.md` reflect reality
    (`AGENTS.md` §6).
- **Validation:**
  - Extend `src/features/theme/siteTheme/components/SiteThemeEditor.test.tsx`.
  - For (a), add a `ThemeModeProvider` test asserting the background style is
    applied from a mocked `useSiteTheme`.
  - `npm test && npm run lint && npm run build`.

### FE-005 Collapse `useEvent`'s double fetch and mixed return shape

- **Priority:** P1
- **Axis:** Architecture / Performance
- **Location:** `src/Soulsjwa.Web/src/features/events/hooks/useEvent.ts`
- **Problem:** Two issues in 27 lines.

  1. **Waterfall.** For alias URLs (`/events/my-cool-event`), the `resolved`
     query fetches `GET /events/{alias}` — which already returns the complete
     `EventResponse`. The hook then starts a *second* query for
     `GET /events/{id}` with the same payload. `initialData` is seeded from
     `resolved.data`, but `initialDataUpdatedAt` comes from a query declared
     `staleTime: 0` (line 13), so the detail query is immediately stale and
     refetches. Every alias page load costs two identical round-trips, and on
     `OverlayPage` both must finish before the token-gated scoreboard query is
     even enabled (`OverlayPage.tsx:374-379`).
  2. **Mixed contract.** `return canonicalId ? detail : resolved` returns two
     different query objects depending on a value that changes across renders.
     Callers see `isLoading` flip back to `true` mid-flow as the hook swaps which
     query it is reporting on, and the returned object's identity is unstable.

  (Note: `GET /events/{identifier}` is anonymous per `docs/api-reference.md:318`,
  so this is a latency and clarity problem, not an auth problem.)
- **Why:** Doubles time-to-first-paint on every aliased event URL — which is the
  URL shape actually shared with viewers — and makes every consumer's loading
  state subtly wrong.
- **Change:**
  1. Use a **single** query keyed by the identifier the caller passed:
     `queryKey: EVENTS_QUERY_KEYS.detail(identifier)`, `queryFn: () => eventsApi.get(identifier)`.
     The endpoint already accepts either form, so no second request is needed.
  2. To keep alias and id lookups sharing one cache entry, add an `onSuccess`-style
     seed: after the fetch resolves, if `identifier !== data.id`, call
     `queryClient.setQueryData(EVENTS_QUERY_KEYS.detail(data.id), data)` so a later
     navigation by id is an instant cache hit. Keep `EVENTS_QUERY_KEYS.resolve`
     only if something else uses it; otherwise delete it from `eventsApi.ts:50`.
  3. Keep the existing `refetchInterval: (query) => query.state.data?.isStarted ? 5000 : false`.
  4. Return one query object unconditionally.
  5. Normalise the identifier to lower-case once at the boundary when it matches
     `EVENT_ID_PATTERN`, preserving today's `identifier.toLowerCase()` behaviour
     so two casings of the same GUID don't produce two cache entries.
- **Acceptance criteria:**
  - Loading `/events/{alias}` issues exactly **one** `GET /events/…` request.
  - Loading `/events/{guid}` issues exactly one request.
  - `useEvent` returns an object whose `isLoading` transitions `true → false`
    once, never back to `true` on the same identifier.
  - Navigating from `/events/{alias}` to `/events/{guid}` for the same event
    resolves from cache without a new request.
  - `OverlayPage`'s scoreboard query is enabled after one event round-trip.
- **Validation:**
  - New `src/features/events/hooks/useEvent.test.tsx` with a mocked `eventsApi`
    asserting request counts for both identifier shapes.
  - Existing `src/routes/index.test.tsx` and `src/pages/OverlayPage.test.tsx`
    must still pass. `npm test`.

### FE-006 Consolidate event-scoped cache invalidation

- **Priority:** P1
- **Axis:** Architecture / Performance
- **Location:** ~40 files under `src/Soulsjwa.Web/src/features/events/hooks/`, plus `src/Soulsjwa.Web/src/features/myEvents/hooks/useToggleMyEventObjective.ts`
- **Problem:** Invalidation is copy-pasted per hook and is wrong in two opposite
  directions at once, because `EVENTS_QUERY_KEYS.detail(id)` is `['events', id]`
  and TanStack Query matches invalidation keys **by prefix**:

  - **Redundant no-ops.** `EVENTS_QUERY_KEYS.scores(id)` is `['events', id, 'scores']`
    and `scoreboard(id)` is `['events', id, 'scoreboard']`. Both are already
    covered by `detail(id)`. So in `useFailObjective.ts:15-17`,
    `useResetFailedObjective.ts:15-17`, `useDeleteObjective.ts:10-12`,
    `useSelfJoinEvent.ts:9-11` and `useToggleMyEventObjective.ts:51-53`, lines 2
    and 3 do nothing. They read as meaningful and are maintained as if they were.
  - **Over-broad blast radius.** Because of that same prefix match, invalidating
    `detail(id)` after a single checkbox toggle also invalidates the event's
    scoreboard, scores, **paginated audit log**, per-competitor info queries, the
    overlay-token list (`OVERLAY_TOKENS_QUERY_KEYS.list` is
    `['events', eventId, 'overlay-tokens']`) and the polled overlay scoreboard.
    On the Games tab that is a burst of refetches per click.
  - **Nuclear option used casually.** `useCreateObjective.ts:13` and
    `useImportPredefinedObjectives.ts:10` invalidate `EVENTS_QUERY_KEYS.all`
    (`['events']`), discarding every cached event, every event list page and
    every scoreboard in the app to add one objective.
  - **Inconsistency.** `useCompleteObjective` and `useUncompleteObjective`
    invalidate `{detail, scores}` while the semantically identical
    `useFailObjective` and `useResetFailedObjective` invalidate
    `{detail, scores, scoreboard}`. Nothing documents why, and the difference has
    no effect — which is exactly what makes it dangerous to "fix" blindly later.
- **Why:** Every objective toggle in a live event triggers far more network
  traffic than it needs, on the one screen most likely to be used on a phone
  mid-run. And the redundant lines actively mislead the next maintainer about
  what the cache actually does.
- **Change:**
  1. Add `src/Soulsjwa.Web/src/features/events/api/eventCache.ts` exporting a
     documented helper, e.g.
     `invalidateEventScope(queryClient, eventId, scope)` where `scope` is a
     union of named, intentional scopes:
     - `'progress'` — objective completion/failure changed: scoreboard + scores only.
     - `'structure'` — games/objectives/competitors changed: detail + scoreboard + scores.
     - `'listing'` — event created/archived/featured/renamed: detail + list + featured.
     The helper must invalidate the *narrowest* keys explicitly and must not rely
     on prefix matching as a side effect.
  2. Change `EVENTS_QUERY_KEYS.detail(id)` to `['events', 'detail', id]` so it no
     longer prefix-matches sibling sub-resources. This is the change that makes
     narrow invalidation actually possible. Update every reference to `detail(…)`
     (including `useReorderObjectives.ts:7` and `useReorderEventGames.ts` which
     use it as an optimistic-update target) and re-run the suite.
  3. Migrate the hooks in this order, testing after each group, so each step is
     independently verifiable:
     - **Group 1 (progress):** `useCompleteObjective`, `useUncompleteObjective`,
       `useFailObjective`, `useResetFailedObjective`, `useEditCompletionTime`,
       `useToggleMyEventObjective`, `useToggleMyEventObjectiveFailure`.
     - **Group 2 (structure):** `useAddGame`, `useAddCustomGame`, `useRemoveEventGame`,
       `useEnableEventGame`, `useDisableEventGame`, `useEditEventGame`,
       `useCreateObjective`, `useDeleteObjective`, `useEditObjective`,
       `useImportPredefinedObjectives`, `useAddCompetitor`, `useRemoveCompetitor`,
       `useUpdateCompetitor`, `useAddModerator`, `useRemoveModerator`,
       `useSelfJoinEvent`, `useAddCompetitorInfo`, `useUpdateCompetitorInfo`,
       `useRemoveCompetitorInfo`.
     - **Group 3 (listing):** `useCreateEvent`, `useUpdateEvent`, `useArchiveEvent`,
       `useUnarchiveEvent`, `useDuplicateEvent`, `useFeatureEvent`,
       `useUnfeatureEvent`, `useStartEvent`, `useStopEvent`.
  4. Replace both `EVENTS_QUERY_KEYS.all` uses with `'listing'`.
  5. Delete every invalidation line the helper makes redundant. Do not keep them
     "for clarity".
- **Acceptance criteria:**
  - No mutation hook calls `queryClient.invalidateQueries` directly; all go
    through `invalidateEventScope`.
  - `EVENTS_QUERY_KEYS.all` is no longer used by any mutation.
  - Completing one objective triggers refetches of the scoreboard and scores
    **only** — not the audit log, overlay-token list, or event list.
  - The four objective mutations use identical invalidation.
  - All 290 existing tests still pass.
- **Validation:**
  - New `src/features/events/api/eventCache.test.ts` asserting, with a real
    `QueryClient` seeded with detail/scores/scoreboard/audits/overlay-token
    entries, exactly which queries each scope marks stale.
  - `npm test && npm run lint && npm run build`.
  - Update `docs/frontend.md` (state-management section) per `AGENTS.md` §6.
- **Related:** FE-021 builds on this. Do FE-006 first.

### FE-007 Make `tokenStore` reactive instead of read-during-render

- **Priority:** P1
- **Axis:** Correctness / Architecture
- **Location:** `src/Soulsjwa.Web/src/components/ProtectedRoute.tsx:10`, `src/Soulsjwa.Web/src/components/AppShell.tsx:80`, `src/Soulsjwa.Web/src/pages/EventsPage.tsx:33`, `src/Soulsjwa.Web/src/features/users/hooks/useCurrentUser.ts:9`
- **Problem:** `tokenStore` is a plain mutable module variable
  (`tokenStore.ts:3`), and four components read `tokenStore.isAuthenticated()`
  **during render**. React has no idea the value changed, so nothing re-renders
  when the token is set or cleared. Today the UI happens to recover because
  unrelated events force a re-render — `AppShell` re-renders on `useLocation`
  changes, and the auth-failure path navigates (`AppShell.tsx:91-96`).

  That makes correctness accidental. Concretely:
  - `useCurrentUser`'s `enabled: tokenStore.isAuthenticated()` is evaluated only
    when something else re-renders the consumer, so the `me` query can sit
    disabled after a token arrives until an unrelated render happens.
  - Reading mutable external state during render is exactly what React 19's
    concurrent rendering does not guarantee is consistent — two components in one
    pass can observe different values if a refresh resolves mid-render.
- **Why:** Fragile auth-dependent UI (nav items, protected routes, the `me`
  query) whose correctness depends on incidental re-renders rather than on the
  value it claims to track. Any future change to what triggers renders can
  silently break sign-in/sign-out chrome.
- **Change:**
  1. Give `tokenStore` a subscription API: a module-level `Set<() => void>` of
     listeners, a `subscribe(listener): () => void`, and notification from
     `setAccessToken` / `clearAccessToken`.
  2. Add `src/Soulsjwa.Web/src/lib/axios/useIsAuthenticated.ts` exporting
     `useIsAuthenticated()` built on `useSyncExternalStore(tokenStore.subscribe,
     tokenStore.isAuthenticated, () => false)`. One hook per file, per
     `AGENTS.md` §5.
  3. Replace the render-time reads in `ProtectedRoute`, `AppShell`, `EventsPage`
     and `useCurrentUser` with `useIsAuthenticated()`.
  4. Leave non-render callers (`apiClient` request interceptor, `tokenManager`)
     reading `tokenStore` directly — they are outside React and correct as-is.
- **Acceptance criteria:**
  - Setting a token via `tokenStore.setAccessToken` re-renders a component using
    `useIsAuthenticated()` with no other trigger.
  - Clearing the token re-renders it back to `false`.
  - `useCurrentUser` becomes enabled as soon as a token is set.
  - `ProtectedRoute` redirects on token clear without depending on a navigation.
  - `src/components/ProtectedRoute.test.tsx` and `src/components/AppShell.test.tsx`
    still pass.
- **Validation:**
  - New `src/lib/axios/useIsAuthenticated.test.tsx` rendering a probe component
    and mutating `tokenStore` outside React.
  - `npm test && npm run lint`.
- **Depends on:** FE-001 (both touch `lib/axios/`; land FE-001 first to avoid
  conflicting edits to `tokenStore.ts`).

---

## P2 — Normal

### FE-008 Validate the overlay `bg` query parameter

- **Priority:** P2
- **Axis:** Security
- **Location:** `src/Soulsjwa.Web/src/features/events/overlay/overlayConfig.ts:121`, `src/Soulsjwa.Web/src/pages/OverlayPage.tsx:476,508`
- **Problem:** `background: params.get('bg')` is taken raw from the URL with no
  validation — it is the only `parseOverlayConfig` field with no parser, while
  every sibling goes through `parseBool` / `parseInt32` / `parseTheme` /
  `parseView`.

  It is then used in two places. `style={{ background: pageBg }}`
  (`OverlayPage.tsx:498`) is safe — React sanitises inline style values. But
  `<GlobalStyles styles={{ 'html, body, #root': { background: pageBg } }} />`
  (line 508) hands the string to Emotion, which serialises object styles into a
  real stylesheet by string interpolation and does not sanitise. A value such as
  `red;}body{display:none}` closes the rule and injects arbitrary CSS into the
  page.
- **Why:** Limited but real. It is not script execution, but it permits UI
  redressing (hiding or covering overlay content on a live stream) and outbound
  requests via `background-image: url(...)`, from a URL the streamer is
  encouraged to copy and paste from elsewhere. It is also the one unvalidated
  input in an otherwise carefully validated parser — cheap to close.
- **Change:**
  1. Add `parseCssColor(value: string | null): string | null` to
     `overlayConfig.ts`, alongside the existing parsers. Accept only:
     `#rgb`/`#rgba`/`#rrggbb`/`#rrggbbaa`, `rgb()`/`rgba()`/`hsl()`/`hsla()` with
     numeric/percent/comma/space/slash content only, the literal `transparent`,
     and a conservative CSS named-colour allowlist. Reject anything containing
     `;`, `}`, `{`, `/*`, `url(`, or `\`. Return `null` on rejection so the
     existing `?? 'transparent'` default applies.
  2. Use it: `background: parseCssColor(params.get('bg'))`.
  3. Mirror the same validation in `OverlayLinkBuilder` if it ever gains a `bg`
     input (it currently does not — no change needed today).
- **Acceptance criteria:**
  - `?bg=%23ff0000`, `?bg=rgba(0,0,0,0.5)`, `?bg=transparent` all still work.
  - `?bg=red;}body{display:none}` falls back to `transparent` and injects no
    extra CSS rule.
  - `?bg=url(https://evil.example/x)` is rejected.
  - No other overlay knob changes behaviour.
- **Validation:**
  - Extend `src/features/events/overlay/overlayConfig.test.ts` with accept/reject
    cases including the breakout payloads above. `npm test`.

### FE-009 Render scoreboard cards **or** the table, not both

- **Priority:** P2
- **Axis:** Performance
- **Location:** `src/Soulsjwa.Web/src/features/events/components/scoreboard/EventScoreboardView.tsx:33-80`
- **Problem:** The component renders the full card list *and* the full table on
  every render, hiding one with `sx={{ display: { xs: 'flex', md: 'none' } }}`
  and the other with the inverse. Every competitor is therefore mounted twice —
  once as a `ScoreboardCard`, once as a `ScoreboardRow` — along with each row's
  `ScoreboardGameBreakdown`. The loading skeletons (lines 36-42) do the same.

  This view is polled every 5 s while an event is live (`useScoreboard.ts:9`) and
  is rendered on the home page, the event scoreboard tab, and the public
  scoreboard deep link.
- **Why:** Doubles the React reconciliation and DOM node count of the app's most
  frequently re-rendered surface, and it is worst on the mobile devices the
  card layout exists to serve — they pay to build a desktop table they never see.
- **Change:** Pick the layout in JS and render one branch.
  ```tsx
  const theme = useTheme()
  const isWide = useMediaQuery(theme.breakpoints.up('md'))
  ```
  Render `<ScoreboardTable>` when `isWide`, the `<Stack>` of `<ScoreboardCard>`
  otherwise; do the same for the two skeleton branches. `useMediaQuery` is already
  used this way in `CalendarPage.tsx:34`, so this matches an existing pattern.
  Keep the breakpoint at `md` so the visual result is unchanged.
- **Acceptance criteria:**
  - At a viewport ≥ `md`, no `ScoreboardCard` is in the DOM.
  - Below `md`, no `<table>` is in the DOM.
  - The rendered output at each width is visually identical to today.
  - `src/features/events/components/scoreboard/EventScoreboardView.test.tsx`
    still passes (adjust matchMedia mocking in `src/test/setup.ts` if needed).
- **Validation:** `npm test`; manual resize check on `/` with a featured event.

### FE-010 Debounce the user search

- **Priority:** P2
- **Axis:** Performance
- **Location:** `src/Soulsjwa.Web/src/features/users/hooks/useUserSearch.ts`, `src/Soulsjwa.Web/src/features/users/components/UserPicker.tsx:51`
- **Problem:** `UserPicker` passes its raw `input` state to `useUserSearch` on
  every keystroke. Each distinct prefix ≥ 2 characters is a new query key and a
  new request, so typing an 8-character handle fires 7 requests. `UserPicker` is
  mounted up to three times at once on the audit screens (actor picker, subject
  picker) — see `AuditLogTable.tsx:170-199`.

  The hook's own doc-comment claims it avoids "spam on every keystroke", but the
  `trimmed.length >= 2` guard only suppresses the first character.
- **Why:** Multiplies load on `/users/search` for no benefit;
  `placeholderData: keepPreviousData` already hides the latency, so a debounce
  costs nothing perceptible.
- **Change:** Reuse the `useDebouncedValue` hook added in FE-002. In
  `UserPicker`, keep `input` as the immediate controlled value for the
  `Autocomplete` and pass `useDebouncedValue(input, 250)` to `useUserSearch`.
  Keep `isFetching` wired to the `loading` prop. Do not debounce inside
  `useUserSearch` — the hook should stay a thin query wrapper so callers control
  the cadence.
- **Acceptance criteria:**
  - Typing an 8-character handle at normal speed issues one request.
  - The visible input text updates with no perceptible lag.
  - Results still appear without requiring a keypress after the pause.
  - `src/features/users/components/UserPicker.test.tsx` passes.
- **Validation:** Extend `UserPicker.test.tsx` with fake timers asserting the
  request count. `npm test`.
- **Depends on:** FE-002 (creates `useDebouncedValue`).

### FE-011 Stop polling in hidden tabs

- **Priority:** P2
- **Axis:** Performance
- **Location:** `src/Soulsjwa.Web/src/features/myEvents/hooks/useMyEventObjectives.ts:73`
- **Problem:** `refetchIntervalInBackground: true` keeps the 5 s poll running
  while the tab is hidden. Each event's objectives query used to mount per expanded card
  (previously `MyEventCard.tsx:60`), so a competitor in several events who expanded them all
  and switches tabs leaves N requests every 5 s running indefinitely.
- **Why:** Battery and data drain on mobile, and sustained API load from users
  who are not looking at the page. Unlike the overlay, nobody is watching this
  view when it is hidden.
- **Change:** Remove `refetchIntervalInBackground: true` from
  `useMyEventObjectives`. TanStack Query then pauses the interval while the
  document is hidden and refetches on return.

  **Do not** change `useOverlayScoreboard.ts:19` — background refetching is
  correct there: an OBS browser source is never the foreground document, and
  stopping it would freeze live stream output. Add a one-line comment there
  saying so, so the two are not "made consistent" later by mistake.
- **Acceptance criteria:**
  - Hiding the tab stops `/my-events/…/objectives` polling.
  - Returning to the tab refetches immediately.
  - The overlay still polls when hidden, with a comment explaining why.
- **Validation:** `npm test`; manual check with DevTools Network while switching
  tabs on `/my-events` with a card expanded.

### FE-012 Fix the optimistic-toggle vs. poll race on My Events

- **Priority:** P2
- **Axis:** Correctness
- **Location:** `src/Soulsjwa.Web/src/features/myEvents/hooks/useToggleMyEventObjective.ts:21-54`, `src/Soulsjwa.Web/src/features/myEvents/hooks/useToggleMyEventObjectiveFailure.ts`
- **Problem:** `onMutate` calls `cancelQueries` and writes the optimistic value,
  but `useMyEventObjectives` polls the same key every 5 s. A refetch that starts
  *after* `onMutate` and returns *before* the server has committed the mutation
  overwrites the optimistic value with pre-mutation data. The checkbox visibly
  flips back, then flips forward again on the next poll.
- **Why:** On the primary "tick off your objective" surface during a live run,
  a checkbox that bounces makes the user re-click and double-submit.
- **Change:**
  1. Track in-flight toggles per objective id (a `useRef<Set<string>>` in the
     hook, or `useMutationState` filtered to this mutation key).
  2. In `useMyEventObjectives`, disable the interval while any toggle for that
     event is pending: `refetchInterval: enabled && !hasPendingToggle ? 5000 : false`.
     The simplest wiring is to lift the pending flag into the panel and pass
     it down, since both hooks are already constructed there
     (now `MyEventsPanel.tsx`; this item is already implemented).
  3. Keep `onSettled`'s invalidation so the authoritative value always lands.
- **Acceptance criteria:**
  - Toggling an objective never shows the checkbox reverting before settling.
  - Polling resumes once no toggle is pending.
  - A failed mutation still rolls back via the existing `onError` path.
- **Validation:** New test in `src/features/myEvents/` with fake timers: start a
  toggle, advance past the poll interval with a stale server response queued,
  assert the optimistic value survives until the mutation settles. `npm test`.

### FE-013 Remove the redundant refresh on the auth callback

- **Priority:** P2
- **Axis:** Correctness / Performance
- **Location:** `src/Soulsjwa.Web/src/pages/AuthCallbackPage.tsx:24-41`, `src/Soulsjwa.Web/src/app/providers/AppProviders.tsx:27-30`
- **Problem:** `AppProviders` gates all children behind
  `tokenManager.bootstrap()`, which already POSTs `/auth/refresh` and stores the
  token. `AuthCallbackPage` then mounts and calls `authApi.refresh()` again —
  a second rotation of a token that was just issued. Under React 19 StrictMode
  (`main.tsx:8`) the effect double-invokes in development, making it a third.

  Each rotation is individually legal, so this is not the FE-001 reuse bug — but
  it burns the `auth` rate-limit bucket (`RefreshTokenEndpoint.cs:18`), adds a
  round-trip to every sign-in, and is exactly the kind of duplicated call that
  turns into the FE-001 failure the moment timing shifts.

  `await queryClient.invalidateQueries()` with no filter (line 32) also
  invalidates *every* query in the cache and awaits all refetches before
  navigating, delaying the post-login redirect.
- **Why:** Slower, noisier login than necessary, and a standing hazard next to a
  P0-severity race.
- **Change:**
  1. In `AuthCallbackPage`, drop the `authApi.refresh()` call. By the time the
     route renders, `AppProviders` has finished bootstrapping, so check
     `tokenStore.isAuthenticated()` (via `useIsAuthenticated()` once FE-007
     lands): if true, `tokenManager.start()` and navigate; if false, show the
     existing `failed` state.
  2. Replace the unfiltered `invalidateQueries()` with
     `queryClient.invalidateQueries({ queryKey: USERS_QUERY_KEYS.me })` and do
     not `await` it before navigating.
  3. Add a `useRef` guard so the effect body runs once even under StrictMode
     double-invocation.
- **Acceptance criteria:**
  - A full Twitch sign-in issues exactly one `POST /auth/refresh`.
  - The redirect to `/` happens without waiting on a cache-wide refetch.
  - The `?error=not_allowlisted` and failure paths render unchanged.
- **Validation:** Extend a test under `src/pages/` mocking `authApi` and
  `tokenStore`; assert the call count and that the blocked/failed branches still
  render. `npm test`.
- **Depends on:** FE-001, FE-007.

### FE-014 Guard `createAppTheme` against malformed palette values

- **Priority:** P2
- **Axis:** Production Reliability
- **Location:** `src/Soulsjwa.Web/src/theme/theme.ts:createAppTheme`, `src/Soulsjwa.Web/src/theme/ThemeModeProvider.tsx:76-80`
- **Problem:** Site-theme colours are fed straight into MUI's
  `createTheme({ palette: { primary: { main: … } } })`. MUI runs `augmentColor`
  over these and **throws** on a value it cannot parse. `createAppTheme` is
  called inside `ThemeModeProvider`'s `useMemo`, which sits *above* the router —
  so a throw is not caught by any `errorElement` and takes down the whole app to
  the top-level `ErrorBoundary`'s "Something went wrong" screen, for every
  visitor, until the data is fixed server-side.

  The frontend currently trusts the API completely here; a malformed or partially
  migrated `SiteTheme` row is enough.
- **Why:** A single bad admin-entered or migrated colour is a total site outage
  with no client-side recovery path. Cheap to make fail-soft.
- **Change:**
  1. In `paletteMapping.ts`, validate each of the six slots in
     `resolveSiteThemePalette` against a hex-colour pattern; substitute the
     corresponding `FALLBACK_LIGHT`/`FALLBACK_DARK` value for any slot that fails,
     rather than passing it through.
  2. In `ThemeModeProvider`, wrap the `createAppTheme` call in `try/catch` and
     fall back to `createAppTheme(resolvedMode)` (no custom palette) on throw, so
     a bad theme degrades to the default look instead of a white screen.
- **Acceptance criteria:**
  - A `SiteTheme` with `darkAccent: 'not-a-color'` renders the app with the
    fallback accent and no error screen.
  - A `SiteTheme` with all-valid colours is unaffected.
  - `noRawHtml.test.ts` still passes.
- **Validation:** Extend
  `src/features/theme/siteTheme/paletteMapping.test.ts` with invalid-value cases
  and add a `ThemeModeProvider` test with a malformed mocked theme. `npm test`.

### FE-015 Clear cached API responses on logout

- **Priority:** P2
- **Axis:** Security
- **Location:** `src/Soulsjwa.Web/src/features/auth/hooks/useLogout.ts:12-17`, `src/Soulsjwa.Web/public/sw.js`
- **Problem:** `useLogout` clears the in-memory token and the React Query cache,
  but the service worker's `soulsjwa-scoreboards-v1` Cache Storage bucket
  (`sw.js:3,29-32`) survives logout and persists on disk. The next user of the
  same browser profile can be served the previous session's cached scoreboard
  responses from `networkFirst`'s fallback path while offline.

  Scoreboard data is not currently the app's most sensitive payload, but the
  cache is unbounded, never evicted, and will silently start holding more if the
  `SCOREBOARD_PATTERN` regex is ever widened.
- **Why:** Cached authenticated-session responses outliving the session is a
  straightforward data-hygiene defect on shared devices, and the fix is four
  lines.
- **Change:**
  1. In `useLogout`'s `onSettled`, after `queryClient.clear()`, delete the API
     cache: `if ('caches' in window) void caches.delete('soulsjwa-scoreboards-v1')`.
  2. Export the cache name as a shared constant rather than duplicating the
     literal in `sw.js` and the hook (`AGENTS.md` §4 — no magic strings). Since
     `sw.js` is a plain public asset and cannot import from `src/`, define the
     constant in `src/lib/` and add a test asserting it matches the literal in
     `public/sw.js` (read via `?raw`, the same technique
     `features/theme/siteTheme/noRawHtml.test.ts` already uses).
  3. Also clear it on the `onAuthRefreshFailed` path in `AppShell.tsx:91-96`, so
     an expired session is treated the same as an explicit logout.
- **Acceptance criteria:**
  - After logout, `caches.keys()` no longer contains the scoreboard cache.
  - The app-shell cache is left intact (it holds only public static assets).
  - A session that ends via refresh failure clears it too.
  - The cache-name constant exists in exactly one place, guarded by a test.
- **Validation:** New test in `src/features/auth/` with a mocked `caches` global.
  `npm test`.

---

## P3 — Cleanup & Simplification

### FE-016 Delete dead code

- **Priority:** P3
- **Axis:** Maintainability
- **Location:** `src/Soulsjwa.Web/src/lib/axios/tokenStore.ts:39-47`, `src/Soulsjwa.Web/src/pages/OverlayPage.tsx:82`
- **Problem:** Two provably dead fragments:
  1. `tokenStore.decodeAccessToken` — a repo-wide grep finds exactly one
     occurrence: its own definition. Nothing calls it.
  2. `OverlayPage.tsx:82` — `if (filteredGames === entry.games) return entry`.
     `filteredGames` is produced by `Array.prototype.filter` on the line above,
     which always returns a new array, so this identity check is never true. The
     intended "nothing was filtered, skip the work" fast path never runs, and the
     comment block above it describes behaviour that does not happen.
- **Why:** Dead code that looks load-bearing. The `filterEntry` line in
  particular will mislead anyone optimising the overlay.
- **Change:**
  1. Delete `decodeAccessToken` and its now-unused `JwtPayload` fields if
     `msUntilAccessTokenExpiry` does not need them (it uses `exp` only — narrow
     the interface to `{ exp: number }`).
  2. Either delete line 82 outright, or replace it with a real fast path:
     `if (filteredGames.length === entry.games.length) return entry`. Prefer
     deleting it — `filterEntry` runs over a handful of games and the branch buys
     nothing measurable.
- **Acceptance criteria:**
  - No references to `decodeAccessToken` remain.
  - `OverlayPage` has no unreachable branch in `filterEntry`.
  - `npm run lint` passes (`noUnusedLocals` is on).
- **Validation:** `npm test && npm run lint && npm run build`.

### FE-017 Collapse the `lazyPages.tsx` boilerplate

- **Priority:** P3
- **Axis:** Maintainability
- **Location:** `src/Soulsjwa.Web/src/routes/lazyPages.tsx` (103 lines)
- **Problem:** 26 exports, each an identical 3-line
  `lazy(() => import('…').then(({ X }) => ({ default: X })))` wrapper. The only
  variation is the module path and the named export. Adding a page means
  copy-pasting the incantation and getting the name right in three places.
- **Why:** Pure ceremony with a real typo surface, and it is the first file a new
  contributor has to touch to add a route.
- **Change:** Add a tiny local helper in the same file and express each page as
  one line:
  ```ts
  const page = <K extends string>(
    loader: () => Promise<Record<K, ComponentType<never>>>, name: K,
  ) => lazy(async () => ({ default: (await loader())[name] }))

  export const HomePage = page(() => import('../pages/HomePage'), 'HomePage')
  ```
  Keep the literal `import('…')` calls inline — do **not** parameterise the
  specifier with a variable, or Vite loses static analysability and the route
  chunks stop being split.

  Verify chunk splitting is unchanged by comparing `npm run build` output before
  and after: the same per-page chunks must still be emitted.
- **Acceptance criteria:**
  - `lazyPages.tsx` is under ~40 lines.
  - `npm run build` emits the same set of route chunks as before the change
    (compare the build's chunk listing).
  - `src/routes/index.test.tsx` passes unchanged.
- **Validation:** `npm run build` before/after diff of emitted chunks;
  `npm test && npm run lint`.

### FE-018 Stabilise `useDragReorder` row refs

- **Priority:** P3
- **Axis:** Performance
- **Location:** `src/Soulsjwa.Web/src/features/events/hooks/useDragReorder.ts:68-99`
- **Problem:** `getRowProps(index)` returns `ref: setRowRef(index)`, and
  `setRowRef(index)` constructs a **new** closure on every call. Because
  `getRowProps` is itself re-created whenever `dragIndex` or `overPosition`
  changes — i.e. on every `dragover` event — React sees a new `ref` callback for
  every row on every pointer move, detaching and re-attaching each row's ref
  (delete from the map, then re-set) many times per second during a drag.
- **Why:** Needless churn on the hot path of an interaction that is supposed to
  feel smooth, and it briefly empties `rowRefs` entries mid-drag.
- **Change:** Memoise the per-index ref callbacks. Keep a
  `useRef<Map<number, (el: HTMLElement | null) => void>>` of callbacks and have
  `setRowRef(index)` return the cached callback for that index, creating it only
  on first use. The callbacks close over nothing but `index` and the refs map, so
  they are safe to cache for the lifetime of the hook.
- **Acceptance criteria:**
  - `getRowProps(0).ref === getRowProps(0).ref` across re-renders.
  - Drag-and-drop reordering of games and objectives still works, including the
    downward-drag off-by-one and the drop indicator.
  - `src/features/events/hooks/useDragReorder.test.ts` and
    `src/features/events/dragReorder.test.ts` pass.
- **Validation:** `npm test`; manual drag of a game and an objective on the
  Games tab.

### FE-019 Drop the pointless `useMemo` in the audit hooks

- **Priority:** P3
- **Axis:** Maintainability
- **Location:** `src/Soulsjwa.Web/src/features/admin/hooks/useAdminAudits.ts:37-40`, `src/Soulsjwa.Web/src/features/events/hooks/useEventAudits.ts:54-57`
- **Problem:** Both hooks wrap the `types` sort in `useMemo` keyed on
  `params.types`. The *sort* is meaningful (TanStack Query hashes query keys
  structurally, so `['a','b']` and `['b','a']` would otherwise be different
  cache entries). The `useMemo` is not: the sorted array's identity never
  matters, because it is only ever fed into the structurally-hashed query key and
  into the `queryFn` payload. Meanwhile `params` itself is constructed fresh by
  the caller on every render, so the memo's dependency changes constantly anyway.
- **Why:** Memoisation that looks like it is protecting a referential-identity
  invariant when there is none — the exact pattern that teaches the next reader
  to cargo-cult `useMemo`.
- **Change:** Delete both `useMemo` wrappers; keep the sort inline. Drop the now
  unused `useMemo` import from both files.
- **Acceptance criteria:**
  - Neither hook imports `useMemo`.
  - Query keys are still order-independent: passing `['b','a']` and `['a','b']`
    hits the same cache entry and issues one request.
  - Audit filtering on `/admin/audits` and the event Activity tab is unchanged.
- **Validation:** Add an order-independence assertion to an audits hook test.
  `npm test && npm run lint`.

### FE-020 Extract `OverlayPage`'s view components

- **Priority:** P3
- **Axis:** Maintainability
- **Location:** `src/Soulsjwa.Web/src/pages/OverlayPage.tsx` (832 lines)
- **Problem:** The largest file in the frontend holds, in one module: three pure
  data helpers, a custom hook, two palette constants, an `ObjectivesView`
  component, and a ~340-line `OverlayPage` with a deeply nested inline-styled
  render. Everything is `style={{…}}` inline objects reconstructed each render.
- **Why:** It is the hardest file in the codebase to change safely — which
  matters because FE-003 and FE-008 both land in it. Splitting it makes those
  fixes (and their tests) reviewable.
- **Change:** Do this **after** FE-003 and FE-008 so the behavioural fixes land in
  small, reviewable diffs first. Then move, without changing behaviour, into a
  new `src/features/events/overlay/` folder:
  - `overlayPalette.ts` — `OverlayPalette`, `DARK_THEME`, `LIGHT_THEME`, and the
    status-mark constants.
  - `overlayScope.ts` — `buildObjectiveItems`, `chunkObjectiveItems`,
    `filterEntry`, `applyFilters`, `getCompletedObjectiveNames`,
    `formatCompletedObjectives`, and their types.
  - `useChangeHighlights.ts` — the hook (one hook per file, `AGENTS.md` §5).
  - `components/ObjectivesView.tsx` and `components/ScoreRow.tsx`.

  `OverlayPage.tsx` keeps only config parsing, the queries, page/cycle state, and
  layout composition. Keep the inline styles — this route deliberately avoids MUI
  so OBS gets predictable output; converting to `sx` is explicitly **not** wanted
  (see Deferred).
- **Acceptance criteria:**
  - `OverlayPage.tsx` is under ~250 lines.
  - The extracted pure helpers have direct unit tests.
  - `src/pages/OverlayPage.test.tsx` passes with no assertion changes.
  - Rendered overlay output is byte-identical for a given config and payload.
- **Validation:** `npm test && npm run lint && npm run build`; visual check of
  all three `view` modes.
- **Depends on:** FE-003, FE-008.

### FE-021 Collapse the mutation-hook boilerplate

- **Priority:** P3
- **Axis:** Maintainability
- **Location:** ~40 files in `src/Soulsjwa.Web/src/features/events/hooks/`
- **Problem:** Once FE-006 routes every mutation through
  `invalidateEventScope`, the remaining hooks are each ~8 lines of identical
  shape: `useMutation({ mutationFn: (vars) => eventsApi.X(eventId, …vars), onSuccess: () => invalidateEventScope(qc, eventId, scope) })`.
  Files like `useStartEvent`, `useStopEvent`, `useEnableEventGame`,
  `useDisableEventGame`, `useAddModerator`, `useRemoveModerator` differ only in
  the API function name.
- **Why:** Every new event action costs a new file, a new import, and a fresh
  chance to pick the wrong invalidation scope.
- **Change:** Add one factory in
  `src/Soulsjwa.Web/src/features/events/hooks/createEventMutation.ts`:
  ```ts
  export const createEventMutation = <TVars, TData>(
    mutationFn: (eventId: string, vars: TVars) => Promise<TData>,
    scope: EventCacheScope,
  ) => (eventId: string) => { /* useMutation + invalidateEventScope */ }
  ```
  Then each hook file becomes a single export, e.g.
  `export const useStartEvent = createEventMutation(eventsApi.start, 'listing')`.

  **Keep one hook per file and keep every existing hook name** — `AGENTS.md` §5
  mandates the file/export naming, and every call site already imports by name,
  so this must be a pure internal refactor with zero call-site churn.

  Leave the hooks with real optimistic-update logic alone: `useReorderObjectives`,
  `useReorderEventGames`, and the `setQueryData`-based trial-run hooks
  (`useEnableTrialRun`, `useStopTrialRun`, `useResetTrialRun`,
  `useDisableTrialRun`) are not boilerplate and must not be forced through the
  factory.
- **Acceptance criteria:**
  - No call site outside `features/events/hooks/` changes.
  - Every migrated hook file is ≤ 5 lines plus imports.
  - The optimistic-update hooks listed above are untouched.
  - All tests pass with no assertion changes.
- **Validation:** `npm test && npm run lint && npm run build`; confirm
  `git diff --stat` shows changes confined to `features/events/hooks/`.
- **Depends on:** FE-006.

---

## Cross-Cutting Refactors

Three underlying causes account for 14 of the 21 tasks. Treat each as one piece
of work rather than as scattered fixes.

### CC-1 — One session-refresh path (FE-001, FE-007, FE-013)

**Root cause:** the auth layer has two refresh implementations, no single-flight
guarantee, no cross-tab coordination, and exposes its state as a mutable module
variable read during render.

**Order:** FE-001 (single-flight + locking) → FE-007 (`useSyncExternalStore`) →
FE-013 (drop the now-redundant callback refresh). Doing FE-013 first would just
move the duplicate call around; doing FE-007 first conflicts with FE-001's edits
to `tokenStore.ts`.

**Done when:** exactly one module issues `POST /auth/refresh`; concurrent and
cross-tab callers share one request; React components observe auth state through
a subscription rather than a render-time read.

### CC-2 — One event-cache invalidation policy (FE-006, FE-021)

**Root cause:** `EVENTS_QUERY_KEYS.detail(id)` being a prefix of every event
sub-resource key, combined with per-hook copy-paste, produced invalidation that
is simultaneously too broad (refetch storms) and full of no-ops (misleading
code), with no two sibling hooks agreeing.

**Order:** re-key `detail` → add `invalidateEventScope` → migrate hooks in the
three groups listed in FE-006 → only then apply the FE-021 factory.

**Done when:** no mutation hook calls `invalidateQueries` directly, and a single
objective toggle refetches the scoreboard and scores and nothing else.

### CC-3 — One list-query UX contract (FE-002, FE-010)

**Root cause:** `keepPreviousData` is used in exactly one hook in the codebase,
and no text input anywhere is debounced, so every filtered or paginated list
drops to a full-page loading state on each keystroke.

**Order:** FE-002 (adds the shared `useDebouncedValue` and fixes the worst
instance) → FE-010 (reuses it).

**Done when:** every query keyed by user-typed text is debounced and uses
`placeholderData: keepPreviousData`, and no page unmounts its own filter
controls while loading. Apply the same two-line pattern to
`AdminUsersPage`/`UsersTab` and the audit tables if they are touched later —
they share the shape but are lower-traffic, so they are not separate tasks here.

---

## Verification Plan

### Baseline (captured during this review, on `claude/react-frontend-review-oidywm`)

```
cd src/Soulsjwa.Web
npm ci
npm test     # 59 files, 290 tests, all passing
```

Record the `npm run build` chunk listing before starting — FE-017 and FE-020
must not change which route chunks are emitted.

### Per-task loop

Run from `src/Soulsjwa.Web/`. Frontend-only changes need only frontend commands
(`AGENTS.md` §2). **Never run the frontend and backend builds in parallel** —
they conflict over `wwwroot/`.

```bash
npm run lint     # eslint + prettier --check
npm test         # vitest run
npm run build    # tsc -b + vite build
```

For a single file while iterating: `npx vitest run src/path/to/File.test.tsx`.

### Gates by phase

| Phase | Tasks | Gate |
|---|---|---|
| 1 | FE-001 | New `refreshSession` concurrency tests pass; manual two-tab session survives a proactive refresh with no event-id 1104 in API logs |
| 2 | FE-002, FE-003, FE-004, FE-005 | New tests for each; manual check of `/events` search focus, an overlay highlight cycle, the admin theme background, and single-request alias loads |
| 3 | FE-006, FE-007 | `eventCache.test.ts` proves the exact invalidation set; all 290 baseline tests still pass |
| 4 | FE-008 … FE-015 | Per-task tests above; full suite green |
| 5 | FE-016 … FE-021 | Pure refactors — the suite must pass with **no assertion changes**; build chunk listing unchanged |

### Tests to add (none of these exist today)

- `src/lib/axios/refreshSession.test.ts` — concurrency and cross-tab (FE-001)
- `src/lib/axios/useIsAuthenticated.test.tsx` (FE-007)
- `src/pages/EventsPage.test.tsx` — focus retention + debounce (FE-002)
- `src/features/events/overlay/useChangeHighlights.test.tsx` (FE-003)
- `src/features/events/hooks/useEvent.test.tsx` — request counts (FE-005)
- `src/features/events/api/eventCache.test.ts` (FE-006)
- `src/features/myEvents/` optimistic-vs-poll race test (FE-012)

### Tests to update

- `src/features/events/overlay/overlayConfig.test.ts` — `bg` validation (FE-008)
- `src/features/theme/siteTheme/paletteMapping.test.ts` — invalid colours (FE-014)
- `src/features/users/components/UserPicker.test.tsx` — debounce (FE-010)
- `src/features/events/components/scoreboard/EventScoreboardView.test.tsx` —
  single-layout rendering; may need `matchMedia` setup in `src/test/setup.ts` (FE-009)
- `src/features/theme/siteTheme/components/SiteThemeEditor.test.tsx` (FE-004)

### Documentation sync (`AGENTS.md` §6 — required in the same PR)

- `docs/frontend.md` — state management and hooks, after FE-006 / FE-007 / FE-021
- `docs/frontend.md` — route/lazy-loading section, after FE-017
- `docs/streamer-overlay.md` — the `bg` parameter's accepted values, after FE-008
- `docs/feature-matrix.md` — the site-background row, after FE-004
- `docs/auth.md` + `docs/system-overview.md` — the refresh flow, after FE-001
- No dependency changes are proposed, so `THIRD-PARTY-NOTICES.md` should not need
  regenerating. If any task adds a package, run
  `tools/generate_third_party_notices.sh` in the same PR or CI will fail.

---

## Deferred / Rejected Suggestions

Considered and deliberately **not** recommended. Do not "improve" these.

1. **Sanitising the Markdown pipeline / adding DOMPurify.**
   `MarkdownView.tsx:74` uses `dangerouslySetInnerHTML`, which looks alarming,
   but `renderMarkdown.ts:14` constructs markdown-it with `html: false`, so raw
   HTML in the source is escaped and there is no HTML path to sanitise. The
   default URL validator (which drops `javascript:`/`data:`) is intentionally
   left unoverridden, and `link_open` is patched to add
   `rel="noopener noreferrer nofollow"`. This is correct as written. Adding
   DOMPurify would add a dependency and imply the current design is unsafe.

2. **Enabling `strict` in `tsconfig.app.json`.**
   `strict` is absent from the config, which looks like a gap. It is not:
   TypeScript 7 enables strict mode by default, and an empirical check confirmed
   `null` is not assignable to `string` under the project's actual build
   (`node node_modules/typescript-7/bin/tsc -b`). Adding `"strict": true`
   would be a no-op.

3. **Migrating server state to Redux / Zustand / a global store.**
   TanStack Query is the right tool and is used correctly. The problems are in
   invalidation policy (FE-006), not in the choice of library.

4. **Virtualising the scoreboard lists (`react-window` etc.).**
   Realistic competitor counts are tens, not thousands. FE-009 (not rendering
   both layouts) recovers far more than virtualisation would, with no new
   dependency and no scroll-behaviour regressions.

5. **Manually chunking FullCalendar in `vite.config.ts`.**
   `CalendarPage` and `EventCalendarPage` are already route-lazy, so Rollup
   places FullCalendar in a shared chunk loaded only by those routes. The
   existing `manualChunks` config is well-reasoned; leave it.

6. **Adding `useMemo` / `useCallback` / `React.memo` broadly.**
   The existing memoisation is generally justified. FE-019 removes one that is
   not. Do not add more without a measured render problem.

7. **Replacing the hand-rolled HTML5 drag-and-drop with `dnd-kit`.**
   `useDragReorder` (114 lines) is well-documented, tested, and handles the
   drag-image offset and off-by-one index math correctly. Swapping it for a
   dependency is a large behavioural risk for no user-visible gain. FE-018 fixes
   the one real defect.

8. **Converting `OverlayPage`'s inline styles to MUI `sx`.**
   The overlay deliberately avoids MUI so OBS browser sources get predictable,
   emotion-free output, and it already has to override `CssBaseline`'s body
   background (`OverlayPage.tsx:505-508`). Keep inline styles; FE-020 only moves
   code between files.

9. **Rewriting `ErrorBoundary` on top of `react-error-boundary`.**
   The 71-line class component does exactly what is needed and is used both
   app-wide and locally (`RuleBuilderWrapper.tsx:12`). No dependency needed.

10. **Suppressing the `ErrorBoundary`'s display of `error.message`.**
    It renders `this.state.error?.message` (`ErrorBoundary.tsx:56-61`), which in
    principle could surface internals. In practice these are client-side React
    errors, not server payloads, and the message is what makes user bug reports
    actionable. Revisit only if server error bodies ever reach it.

11. **Deduplicating `permissions.ts` against the backend's `EventOwnership`.**
    The duplication is intentional and documented: the client mirrors the rules
    to hide controls, the server enforces them. Attempting to share one
    implementation across C# and TypeScript would be far worse than the
    duplication.

12. **Splitting `types/index.ts` (456 lines) into per-feature type modules.**
    It is a flat, well-ordered mirror of the wire contract, which is exactly what
    `AGENTS.md` §3.3 asks for. Splitting it would make drift between client and
    server harder to spot, not easier.

13. **Removing `refetchIntervalInBackground` from `useOverlayScoreboard`.**
    Explicitly correct as-is — an OBS browser source is never the foreground
    document. FE-011 changes only the My Events hook and adds a comment here so
    the two are not mistakenly unified later.

14. **Adding a client-side route guard for `/admin` beyond the current check.**
    `AdminLayout.tsx:17-19` renders an error for non-admins and the server
    enforces authorization on every admin endpoint. A frontend check is not a
    security boundary and a fancier one would not add any.
