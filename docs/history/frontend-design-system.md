> **Historical.** This rebuild proposal was never implemented as written: the
> SPA uses stock MUI with the admin-defined site theme, and files it names
> (`theme/tokens.ts`, `EventDetailPage`) do not exist. Kept for the reasoning.
> The current frontend is described in [../frontend.md](../frontend.md).

# UI Overhaul — Design System Rebuild Specification

> **Status:** Approved product decisions recorded for implementation planning.
> This document specifies a future rebuild; it does not include implementation.

## 1. Purpose and scope

Soulsjwa needs a coherent, accessible design system for its public, competitor,
event-owner, administrator, and broadcast experiences. The rebuild will replace
the current visual layer while preserving product behavior and API contracts.

This specification covers the current-state audit, information architecture,
tokens, components, layouts, MUI theming, quality requirements, and a two-phase
delivery plan. Logo creation, final brand artwork, backend changes, and
implementation are out of scope.

### Design principles

1. **Task clarity before decoration.** Competition state, permissions, and next
   actions must be obvious.
2. **Accessible by default.** WCAG 2.1 AA is a release requirement for in-scope
   experiences, not a later polish pass.
3. **Progressive disclosure.** Keep frequent competitor actions close at hand;
   move destructive and advanced administration behind clear secondary flows.
4. **Responsive by composition.** Change information hierarchy at breakpoints
   rather than merely shrinking desktop layouts.
5. **Theme parity.** Light and dark modes are equal products; neither is a
   generated afterthought.
6. **Stock MUI first.** Prefer MUI behavior and accessibility over custom
   styling. Add branded behavior only when a documented need justifies it.

## 2. Current-state audit

The audit used the route definitions, feature code, API reference, frontend
documentation, and [feature matrix](../feature-matrix.md). The current system is
more developed than an unthemed prototype, but its implementation and
documentation do not yet provide a reliable foundation for continued growth.

### What exists

- React 19, MUI 9, React Router, and TanStack Query form a sensible base.
- `theme/tokens.ts` defines brand, semantic, surface, radius, spacing, type
  weight, gradient, and motion values.
- `theme/theme.ts` provides light/dark palettes, global focus and reduced-motion
  rules, and overrides for common MUI components.
- A light/dark/system mode control persists the user's preference.
- Shared `PageHeader`, `Surface`, state, skeleton, metric, and pagination
  primitives exist under `components/ui`.
- The app already has mobile navigation, responsive event cards, separate
  mobile/desktop scoreboard presentations, and horizontally scrollable dense
  tables.

### What is broken or inconsistent

- The token file is described as a complete source of truth, but OBS overlay
  palettes, Blockly chrome, a JSON preview, and one tooltip color still use
  local color literals. Overlay colors may legitimately be a separate theme,
  but they are not modeled or documented as one.
- Tokens mix primitives and usage decisions in one flat module. There is no
  explicit primitive → semantic → component-token hierarchy.
- Typography only names a font family and weights; sizes, line heights, and
  responsive heading behavior remain implicit MUI defaults. Some weights are
  unnamed literals.
- Spacing has only an 8 px base value. Elevation, z-index, focus-ring,
  icon-size, control-height, and breakpoint usage are not defined as design
  decisions.
- Custom glass surfaces, gradients, pill controls, large shadows, and broad
  component overrides make it difficult to distinguish stable product
  requirements from visual experimentation. This conflicts with the requested
  stock-MUI Phase 1.
- Color contrast is asserted rather than recorded. There is no checked matrix
  for text, icons, controls, focus indicators, status colors, and translucent
  surfaces in both modes.
- Page imports are eager. The production build places Blockly in a separate
  vendor chunk, but loading a route that imports its wrapper can still fetch the
  heavy dependency; route-level boundaries are absent.
- Event detail combines public reading, competitor action, owner management,
  delegation, audit, and OBS setup. Tabs help, but permission-dependent actions
  need a consistent hierarchy and explanation.
- The route `/events/:id/tokens` exists but is missing from the documented route
  diagram. The admin page also has routed Users and Audits sections that the
  diagram presents only as internal tabs.
- Accessibility helpers exist, but there is no documented keyboard journey,
  screen-reader verification, error-summary pattern, table sorting semantics,
  or complete labelling/loading/error treatment for Blockly.

### What is missing

- A governance model for adding, changing, deprecating, and testing tokens and
  shared components.
- Documented component states: default, hover, active, focus-visible, disabled,
  loading, empty, error, read-only, success, and destructive confirmation.
- A standalone admin browser/editor for predefined objective templates, despite
  API support.
- A dedicated "My events / my tasks" view. Competitors currently discover work
  through the public event list and event detail.
- A dedicated score-history timeline. Completion timestamps exist, but the UI
  only presents current breakdowns.
- Measured accessibility and performance baselines, budgets, and a device/test
  matrix.

## 3. Information architecture

### Existing route inventory

| Route                     | Audience                | Primary task                      | Notes                                                   |
| ------------------------- | ----------------------- | --------------------------------- | ------------------------------------------------------- |
| `/`                       | Public                  | Understand Soulsjwa; sign in      | Landing page and Twitch entry point                     |
| `/events`                 | Public                  | Find an event                     | Search, status filters, pagination; admin create action |
| `/events/:id`             | Public/member           | Understand event status           | Overview, score preview, role-aware actions             |
| `/events/:id/competitors` | Public/member/owner     | View or manage roster             | Self-join, assignment, streamer flag, delegation        |
| `/events/:id/games`       | Public/competitor/owner | View games and track objectives   | Game management, objective CRUD, Blockly, completion    |
| `/events/:id/tokens`      | Owner/competitor        | Configure OBS access              | Token mint/revoke and overlay link builder              |
| `/events/:id/activity`    | Event members           | Review event audit history        | Member authorization enforced by API/UI                 |
| `/events/:id/scoreboard` | Public                  | Follow rankings and details       | Mobile cards, desktop table, expandable breakdown       |
| `/events/:id/overlay`     | Broadcaster             | Render OBS browser source         | Chrome-free, query-configured, token-gated              |
| `/profile`                | Signed-in user          | View identity and manage API keys | Also logout                                             |
| `/auth/callback`          | OAuth return            | Complete or explain sign-in       | Transitional state, not global navigation               |
| `/admin`                  | Admin                   | Manage allowlist                  | Admin index/default section                             |
| `/admin/users`            | Admin                   | Manage user roles                 | Routed tab                                              |
| `/admin/audits`           | Admin                   | Review system activity            | Routed tab                                              |

### Core journeys

**Public:** landing → event discovery → event overview → scoreboard → expanded
competitor/game detail. Authentication should never be required for these
read-only paths.

**Competitor:** sign in → open My Events → select an assigned event or outstanding
objective → complete/uncomplete objectives → add clip/link/note metadata in
scoreboard details → inspect progress. My Events is a Phase 1 product slice.

**Delegated moderator:** sign in → open the streamer's event → select an allowed
competitor → update objectives, live status, clips, links, and notes. Every
on-behalf-of action must name the target and preserve auditability.

**Owner/admin:** create event → configure details/tie-break → assign or create
games → import/create objectives → assign competitors and delegates → start
event → monitor/correct completion → configure overlay → stop/archive event.

**Broadcaster:** event Tokens section → mint a scoped token → configure and
preview a URL → copy to OBS → render the token-gated overlay without app chrome.
Token values must not appear in general navigation, logs, or analytics.

### Backend capability versus UI coverage

| Capability                                      |     API      | Current UI           | Rebuild treatment                                          |
| ----------------------------------------------- | :----------: | -------------------- | ---------------------------------------------------------- |
| Event list/detail/create/edit/lifecycle/archive |     Yes      | Complete             | Preserve; clarify owner actions                            |
| Competitor add/remove/self-join/streamer flag   |     Yes      | Complete             | Responsive roster and explicit permissions                 |
| Competitor moderator delegation                 |     Yes      | Complete             | Dedicated delegate management pattern                      |
| On-behalf-of objective completion               |     Yes      | Partial/embedded     | Explicit target selector and confirmation context          |
| Known/custom event games                        |     Yes      | Complete             | Preserve; separate catalog choice from event override      |
| Predefined objective list/create                |     Yes      | **No standalone UI** | Add global Admin template browser/editor product slice      |
| Predefined objective edit/delete                |    **No**    | No                   | Add API support with the Admin template product slice       |
| Assign one predefined objective                 |     Yes      | **No direct UI**     | Add from template browser; bulk import remains available   |
| Import predefined objectives                    |     Yes      | Complete             | Search/filter/select workflow                              |
| Custom objective CRUD and Blockly rule          |     Yes      | Complete             | Accessible wrapper and lazy loading                        |
| Completion/uncompletion/time correction         |     Yes      | Complete             | Role-aware checklist and correction dialog                 |
| Death clip/link/note metadata CRUD              |     Yes      | Complete             | URL editor + safe player/link preview; no file upload      |
| Assigned events/outstanding objectives query    |    **No**    | No                   | Add authenticated summary endpoint for Phase 1 My Events   |
| Live status and scoreboard breakdown           |     Yes      | Complete             | Mobile cards, desktop data grid/table                      |
| Full score timeline                             | Partial data | **No dedicated UI**  | Defer pending product/API definition                       |
| Overlay tokens and overlay scoreboard          |     Yes      | Complete             | Separate control panel and broadcast canvas                |
| Event/admin audits                              |     Yes      | Complete             | Shared filter and detail patterns                          |
| Profile and API keys                            |     Yes      | Complete             | Preserve one-time secret disclosure pattern                |
| Invite links, global game CRUD, hard delete     |    **No**    | No                   | Do not design as available actions                         |

The API stores competitor information as typed URLs/text; it does not expose
binary clip upload. The rebuild must not imply local upload until a backend
contract, storage, moderation, and privacy model are approved.

My Events requires a new authenticated `GET /api/v1/me/events` endpoint because
the existing public event list cannot query by the current competitor or return
outstanding objectives. The response should summarize each event assignment and
its incomplete objectives so the client does not assemble private dashboard data
through per-event requests.

### Proposed navigation

- **Global desktop:** Home, Events, then authenticated My Events and Profile;
  Admin appears only for admins. Keep event operations out of global navigation.
- **Global mobile:** the same destinations in a labelled drawer with visible
  current-page state and reliable focus return.
- **Event local navigation:** Overview, Competitors, Games & objectives,
  Scoreboard, Activity (members), and Broadcast (authorized users). Use real
  links, not state-only tabs, so deep links and browser history work. Introduce
  a shared event route layout for this navigation and event context;
  `EventDetailPage` and the currently separate `EventScoreboardPage` render as its
  children rather than duplicating or omitting the event shell.
- **Page actions:** one primary action at most; secondary actions in a nearby
  group; destructive actions in an overflow menu plus confirmation.
- **Approved additions:** `/my-events` belongs in authenticated global navigation
  and is a Phase 1 product slice. Predefined templates belong at
  `/admin/objectives` in a global Admin subsection delivered as a separate
  feature slice; it may use Phase 1 patterns but does not block the theme
  migration.

## 4. Token specification

Use three layers. Components may consume semantic or component tokens, never
raw primitive values. Names describe purpose rather than appearance.

```text
primitives (color.violet.700, space.3)
  → semantic (color.action.primary, color.surface.canvas)
    → component (button.primary.background, overlay.panel.background)
```

### Color

Phase 1 should configure only MUI palette roles and let stock components derive
states. Candidate values require automated contrast verification before use:

| Role                              | Light mode             | Dark mode              | Requirement                                    |
| --------------------------------- | ---------------------- | ---------------------- | ---------------------------------------------- |
| `primary.main`                    | `#6D28D9`              | `#C4B5FD`              | 4.5:1 for normal text where used as foreground |
| `primary.contrastText`            | `#FFFFFF`              | `#111827`              | 4.5:1 against primary                          |
| `secondary.main`                  | `#0369A1`              | `#7DD3FC`              | Distinct from primary; not status-only         |
| `background.default`              | MUI light default      | MUI dark default       | Stable canvas, no fixed decorative gradient    |
| `background.paper`                | MUI light default      | MUI dark default       | Opaque enough for predictable contrast         |
| `text.primary/secondary/disabled` | MUI-derived            | MUI-derived            | Verify every surface pairing                   |
| `success/warning/error/info`      | MUI defaults initially | MUI defaults initially | Always pair color with icon/text               |

Phase 2 may add branded ramps and semantic roles for canvas, surface, raised
surface, border, selected, focus, live, offline, completed, and destructive
actions. OBS tokens form an explicit `overlay.*` group because transparency and
capture contrast differ from the app. User-supplied overlay background remains
configuration data, not a design token.

No state may be conveyed by color alone. Translucent colors must be tested over
every supported background, including OBS transparency.

### Typography

Phase 1 uses the system font stack to avoid font downloads and follows the MUI
type scale:

| Token / MUI variant         | Size / line height                   | Use                               |
| --------------------------- | ------------------------------------ | --------------------------------- |
| `display` / responsive `h1` | 2.5 rem / 1.15 desktop; 2 rem mobile | Landing hero only                 |
| `h1`                        | 2 rem / 1.2                          | One page title                    |
| `h2`                        | 1.5 rem / 1.25                       | Major page sections               |
| `h3`                        | 1.25 rem / 1.3                       | Cards/dialog sections             |
| `body1`                     | 1 rem / 1.5                          | Default content                   |
| `body2`                     | 0.875 rem / 1.5                      | Supporting content                |
| `caption`                   | 0.75 rem / 1.5                       | Metadata, never essential actions |
| `button`                    | 0.875 rem / 1.75                     | MUI control label                 |

Use weights 400, 500, 600, and 700 only. Body text must support 200% zoom
without clipping. Phase 2 may introduce a bundled brand typeface only after
licensing, subset size, rendering, and fallback behavior are approved.

### Spacing, sizing, and density

- Keep MUI's 8 px spacing base in Phase 1. Use scale steps `0, 0.5, 1, 1.5, 2,
3, 4, 6, 8`; do not add arbitrary pixel gaps.
- Minimum pointer target is 44×44 CSS px; use 48 px for primary mobile controls.
- Default content measure is 70 characters for prose.
- Phase 2 can name semantic aliases such as `page.gutter`, `section.gap`, and
  `control.height` once repeated usage is measured.
- Dense mode is limited to admin/scoreboard desktop tables and must remain
  operable at 200% zoom.

### Elevation, borders, and radii

- Phase 1 uses stock MUI elevation and shape. Use elevation 0 for canvas, 1 for
  contained sections, 3 for sticky navigation, and 8 for transient dialogs or
  menus. Prefer borders over shadows in dark mode.
- Phase 2 may expose `radius.sm/md/lg/full` (4/8/12/999 px) and `elevation.
surface/sticky/modal` aliases. Full radius is for chips/badges, not every
  button.
- Focus indicators are not decorative borders and must remain visible when
  components are selected or in an error state.

### Breakpoints and motion

Retain MUI defaults: `xs 0`, `sm 600`, `md 900`, `lg 1200`, `xl 1536`. Design
content-first within these values; do not add device-specific breakpoints.

Use MUI transition durations in Phase 1. Phase 2 may name `fast 150 ms`, `base
200 ms`, and `slow 300 ms`. All non-essential motion must stop under
`prefers-reduced-motion`; no layout-affecting animation is allowed.

## 5. Component inventory

### Stock MUI foundation

Use `CssBaseline`, `Container`, `Box`, `Stack`, `Grid`, `AppBar`, `Toolbar`,
`Drawer`, `Breadcrumbs`, `Tabs`, `Link`, `Typography`, `Button`, `IconButton`,
`Menu`, `Card`, `Paper`, `Divider`, `Chip`, `Avatar`, `Badge`, `Alert`,
`Skeleton`, `CircularProgress`, `Dialog`, `Snackbar`, `Tooltip`, `TextField`,
`Select`, `Autocomplete`, `Checkbox`, `Switch`, `FormControl`, `FormLabel`,
`FormHelperText`, `Table`, `TableContainer`, `Accordion`, and `Pagination`.

Do not wrap a stock component merely to rename it. Create a shared component
only when it encodes a recurring accessibility, domain, or composition rule.

### Shared application components

| Component            | Responsibility                                                              |
| -------------------- | --------------------------------------------------------------------------- |
| `AppShell`           | Skip link, responsive global navigation, main landmark, footer              |
| `PageHeader`         | Breadcrumbs, one page heading, description, role-aware actions              |
| `Section`            | Heading association and consistent vertical rhythm; visual surface optional |
| `StatusBadge`        | Label + icon for live, stopped, archived, enabled, completed states         |
| `AsyncState`         | Stable-size loading, empty, error, retry, and stale states                  |
| `ConfirmDialog`      | Named consequence, safe initial focus, destructive action                   |
| `FormActions`        | Responsive submit/cancel placement and pending state                        |
| `ResponsiveDataView` | Table at `md+`, cards/list below `md`, equivalent content/actions           |
| `PermissionNotice`   | Explains unavailable or on-behalf-of actions without leaking data           |

### Domain components

| Component                    | Key behavior                                                                                       |
| ---------------------------- | -------------------------------------------------------------------------------------------------- |
| `EventCard` / `EventSummary` | Status, schedule/state, participant/game counts, clear link                                        |
| `CompetitorRoster`           | Responsive roster, add/remove, streamer flag, self-join                                            |
| `DelegateManager`            | Names competitor scope; add/remove delegated users                                                 |
| `GamePanel`                  | Availability, connector support, objectives, owner actions                                         |
| `ObjectiveChecklist`         | Native checkbox semantics, score, completion metadata, target context                              |
| `ObjectiveTemplatePicker`    | Search/filter/select predefined templates; global admin browser                                    |
| `RuleBuilderWrapper`         | Lazy Blockly workspace, proper labelling, loading state, and error boundary                         |
| `ScoreboardView`            | Semantic table on desktop and equivalent cards on mobile                                           |
| `ScoreboardDetails`         | Per-game/objective disclosure with predictable focus/expanded state                                |
| `CompetitorInfoEditor`       | Death-clip URL, link, and note validation; safe external preview/player                            |
| `AuditLog`                   | Filterable table/list with human-readable change disclosure                                        |
| `OverlayTokenManager`        | One-time token disclosure, revoke, copy status, security warning                                   |
| `OverlayConfigurator`        | Labelled controls, live preview, copyable URL                                                      |
| `OverlayCanvas`              | Capture-safe objectives/scores/games views with bounded animation                                  |

Improve Blockly in place with proper labelling, loading states, and an error
boundary. A non-visual fallback or structured rule editor is explicitly out of
scope for both phases.

## 6. Layout system

- **App shell:** sticky but non-obstructive global header, skip link, `main`
  landmark, and optional footer. Content uses `maxWidth="xl"`; reading forms use
  a narrower measure.
- **Page rhythm:** page header → optional status/summary → primary content →
  secondary/help content. Use spacing, not decorative empty containers.
- **Grid:** 4 columns on small screens, 8 on tablet, 12 on desktop, implemented
  with MUI Grid. Forms are one column on mobile and at most two related fields
  per row on larger screens.
- **Event shell:** a shared parent route owns event context and routable,
  horizontally scrollable local tabs on small screens. Overview/management and
  scoreboard pages are child routes with equivalent navigation and loading,
  error, and permission states. Side navigation is a Phase 2 option only if
  usability testing shows tabs no longer scale.
- **Tables:** card/list alternative below `md`; preserve labels, values, actions,
  and expanded state. Desktop tables scroll within their labelled region rather
  than the entire page.
- **Dialogs:** use for short, reversible tasks. Use a page or stepper for long
  event/objective workflows. Full-screen dialogs are acceptable on mobile.
- **Overlay:** independent shell with no app navigation and fixed 1920×1080
  capture-safe regions. Design and test content, typography, and layout at that
  Full HD OBS Browser Source baseline; 4K and responsive scaling are out of scope.

At 320 CSS px and at 400% zoom, content must reflow without two-dimensional
scrolling except for inherently two-dimensional content such as Blockly and
data tables, which receive labelled scroll regions.

## 7. MUI theming approach

Phase 1 will create a small theme factory around MUI `ThemeProvider` and
`CssBaseline`. It should contain:

- `colorSchemes` or equivalent light/dark palette definitions;
- the typography settings in this specification;
- a persisted `light | dark | system` preference with no first-paint flash; and
- no component `styleOverrides` unless required to fix a verified WCAG defect.

Use MUI palette augmentation for app semantic roles only after Phase 2 tokens
are approved. Prefer `theme.vars`/palette references and `theme.spacing` in
components. Token names use lower camel case in TypeScript and dot notation in
documentation, with category and purpose: `color.action.primary`, not
`purpleButton`.

The new foundation should be built beside the old theme, verified in a route or
component harness, then migrated by feature. Remove old tokens and compatibility
aliases when the final consumer moves; do not maintain two permanent systems.

### Governance

- Every token/component change includes rationale, light/dark examples, all
  interaction states, and accessibility evidence.
- Removing or renaming a public token/component requires a migration note.
- Feature components own domain behavior; the theme owns visual defaults.
- Shared components stay dependency-light and must not import feature modules.
- Documentation and visual examples are updated in the same change.

## 8. Quality requirements

### Accessibility — P0

- Meet WCAG 2.1 AA across all in-scope experiences in both modes and at supported
  responsive sizes. The visual Blockly rule-editing workspace is the explicit
  exception; its labelling, loading, and error handling must still improve.
- Verify 4.5:1 normal text, 3:1 large text and meaningful UI graphics, and 3:1
  focus indicators against adjacent colors. Record results; do not infer them.
- Support keyboard-only journeys, visible focus, logical order, skip links,
  focus restoration, Escape behavior, and no keyboard traps.
- Use native elements first. Icon-only actions need accessible names. Dynamic
  completion/copy/save results use appropriately restrained live regions.
- Associate labels, instructions, helper/error text, and error summaries with
  fields. Do not clear user input after validation failures.
- Tables use captions or labelled regions and correct row/column headers.
  Expanders expose `aria-expanded` and `aria-controls`.
- Status never relies on color alone. Clips require descriptive link text and
  accessible player controls; autoplay is prohibited.
- Clips, links, and notes are visible on public scoreboards without sign-in.
  Twitch clips and YouTube URLs use inline players created only from parsed,
  allowlisted HTTPS origins and platform identifiers; never inject a submitted
  URL as an arbitrary iframe source. Unknown URLs render as styled external
  links that open in a new tab with opener access disabled.
- Test with automated tooling plus keyboard, 200%/400% zoom, forced colors,
  reduced motion, and at least NVDA/Firefox and VoiceOver/Safari.

### Performance — P0

- Lazy-load each routed page. Load Blockly only when the rule builder opens and
  isolate it behind `Suspense` and an error boundary. Keep OBS code out of the
  normal app-shell entry path.
- Preserve direct MUI imports and analyze production chunks. First record the
  current production app-shell and route-chunk gzip baseline. Treat ≤200 kB for
  the app shell and ≤150 kB per route chunk (excluding the user-invoked Blockly
  chunk) as provisional targets, then confirm or adjust them from that evidence
  before enforcing hard gates.
- Reserve dimensions for skeletons, avatars, media, and overlay panels. Animate
  transform/opacity only; batch DOM reads/writes and avoid resize loops.
- Use TanStack Query for server state and deduplication. Poll only while a live
  view is visible; stop or reduce polling in background tabs.
- Paginate existing long lists. Consider virtualization only after measurement
  and accessibility review; never virtualize small lists by default.
- Gate each phase on a production build report and Lighthouse measurements for
  representative mobile routes. Performance tests must use throttled,
  reproducible settings.

### Responsive — P1

Validate at 320, 360, 390, 600, 900, 1200, and 1536 CSS px, portrait and
landscape where relevant. Priority scenarios are objective completion,
delegation, event management, scoreboard expansion, clip/note editing, and
overlay configuration. Touch targets, on-screen keyboard behavior, safe-area
insets, long names, localization expansion, and empty/error states are included.

### Dark mode — P1

Light, dark, and system modes ship in Phase 1. Preference persists, follows
system changes only in system mode, and is applied before paint. Every
component state and data visualization is checked in both modes. OBS theme is
URL-controlled and independent of the operator's app preference.

## 9. State, routing, and bundle architecture

- Keep TanStack Query as the single server-state layer. Keep short-lived form,
  disclosure, and dialog state local. Do not add a global state library for the
  design system.
- Put shareable event tabs, filters, pagination, and overlay configuration in
  route/query state. Validate and default query parameters at one boundary.
- Route-level permission checks improve presentation but never replace API
  authorization. Avoid showing destructive controls before identity/role data
  resolves.
- Use route modules and dynamic imports for page splitting. Place Blockly in a
  nested dynamic boundary. Prefetch only likely next routes and never tokenized
  overlay URLs.
- Error boundaries exist at application, route, and heavy-widget levels.
  Preserve enough context for retry without losing form data.

## 10. Delivery phases

### Phase 1 — Stock MUI foundation

**Goal:** establish a quiet, consistent, accessible baseline without defining
final brand identity.

1. Capture route screenshots, keyboard journeys, contrast results, production
   chunks, and Lighthouse baselines. Confirm or adjust the provisional bundle
   targets before enforcing them.
2. Introduce the minimal light/dark MUI theme: palette and typography only.
3. Remove all custom gradients, glass/blur effects, nebula backgrounds,
   decorative shadows, pill-everything styling, and nonessential component
   overrides. Phase 1 ships only clean, stock MUI surfaces.
4. Establish the app shell, page/section rhythm, responsive navigation, and
   stock form/dialog/table patterns.
5. Migrate shared async, confirmation, status, and responsive data patterns.
6. Add the shared event parent layout before migrating the standalone
   scoreboard into event-local navigation.
7. Migrate by journey: public/auth → competitor → owner/admin → scoreboard →
   overlay. Preserve behavior and authorization.
8. Add the authenticated My Events view and its assignment/objective summary API.
9. Add route and Blockly lazy boundaries and enforce the confirmed bundle budgets.
10. Run the complete accessibility, responsive, theme, and regression matrix;
   remove the old theme and obsolete primitives.

**Exit criteria:** all current routes and actions remain available; light/dark
parity and WCAG 2.1 AA evidence exist for in-scope experiences; priority mobile
journeys pass; no unapproved literal design values remain; confirmed production
budgets pass; the old theme has no consumers.

### Phase 2 — Brand and refinement

**Goal:** add a distinctive Soulsjwa identity on top of the proven foundation.

1. Design and approve brand direction and any licensed font from scratch; do not
   evolve the removed nebula/glass language.
2. Expand primitive, semantic, component, and overlay token layers.
3. Refine elevation, radii, density, motion, data visualization, and component
   variants based on usability findings.
4. Add only justified component overrides and branded illustrations/assets.
5. Test high-density scoreboards, broadcast capture, and long-running live
   events; compare against Phase 1 accessibility and performance baselines.
6. Publish component guidance and governance examples.

**Exit criteria:** brand review is approved; no Phase 1 accessibility,
performance, responsive, or theme regression exists; every override has a
documented use case and state matrix.

Predefined-objective administration is a separate, non-blocking product slice
under Admin for browsing, creating, editing, and deleting global templates; it
requires edit/delete API support. Score history remains a separate future slice.

## 11. Validation and acceptance

Each implementation slice must include:

- unit tests for token/theme factories and state logic;
- component tests for keyboard interaction, names, focus, errors, and async
  states;
- route-level tests for role-dependent navigation and critical journeys;
- automated accessibility checks, followed by the manual checks listed above;
- screenshots or visual regression cases for both modes at mobile and desktop;
- production build/chunk comparison and layout-shift observation; and
- regression verification for public access, API authorization, polling, token
  secrecy, and reduced-motion behavior.

The design system is successful when users can complete the same task with a
keyboard or touch, at mobile or desktop width, in either color mode, without
needing to understand their authorization model or the implementation.

## 12. Assumptions and approved decisions

### Assumptions

- Existing API behavior and authorization remain unchanged during the theme
  migration except for separately approved product slices such as My Events.
- Twitch remains the only interactive web sign-in provider.
- Competitor "clip submission" means storing an approved external URL; binary
  uploads are not currently supported.
- English is the only current locale, but layouts must tolerate longer future
  translations.
- MUI remains the component foundation, and React Router/TanStack Query remain
  the routing and server-state choices.
- Overlay transparency and URL-controlled themes are required for OBS.

### Approved decisions

1. Phase 1 removes the nebula/glass visual language entirely and uses stock MUI
   surfaces. Phase 2 brand work starts from scratch.
2. My Events is approved for Phase 1 and requires a new authenticated assignment
   and outstanding-objective summary endpoint.
3. Predefined-objective administration is a separate global Admin feature slice
   with browse, create, edit, and delete flows.
4. Clips, links, and notes are publicly visible. Twitch and YouTube use
   origin-allowlisted inline players; other URLs use safe external links.
5. The OBS overlay baseline is fixed at 1920×1080, with no current 4K or
   responsive-scaling requirement.
6. Blockly receives labelling, loading, and error-boundary improvements; a
   non-visual editing alternative is out of scope.
7. Bundle targets remain provisional until Phase 1 records the production
   baseline and confirms or adjusts them.
