# AGENTS.md

Conventions and operating manual for AI coding agents (Claude Code, GitHub Copilot,
Cursor, Codex, etc.) working on this repository. Human contributors should read
[`CONTRIBUTING.md`](./CONTRIBUTING.md) instead — this file is tuned for agents.

This is the single source of truth. `.github/copilot-instructions.md` is a thin
pointer to this file so we never drift.

---

## 1. Project map

- **Backend** — ASP.NET minimal API, `net10.0`, at `src/Soulsjwa.Api/`
- **Frontend** — React 19 + TypeScript 7 (build) + Vite 8 + MUI v9, at `src/Soulsjwa.Web/` — see `docs/adr/0001-dual-typescript-packages.md` for the two TypeScript packages in `package.json`
- **Connector** — WPF app, `net10.0-windows`, at `src/Soulsjwa.Connector/`
- **Shared** — Wire/contract types, at `src/Soulsjwa.Shared/`
- **Tests** — `tests/Soulsjwa.{UnitTests,IntegrationTests,ApiTests,ConnectorTests}/`
  — a pyramid, in that order: `UnitTests` (no I/O), `IntegrationTests` (real
  Postgres, **no HTTP**, where most backend behaviour belongs), `ApiTests`
  (the HTTP contract only, a couple of tests per endpoint). Placement rules in
  [`docs/agent-conventions/testing.md`](./docs/agent-conventions/testing.md)
- **Solution** — `Soulsjwa.slnx` (SDK-style)

Deeper architectural docs live under [`docs/`](./docs/). Convention details that
would bloat this file live under [`docs/agent-conventions/`](./docs/agent-conventions/) —
**read those on demand**, not eagerly.

---

## 2. Build, lint, test

```bash
# Backend
dotnet build Soulsjwa.slnx
dotnet test  Soulsjwa.slnx                      # full suite (Connector tests are Windows-only)
dotnet test  tests/Soulsjwa.UnitTests/          # fast loop
dotnet format Soulsjwa.slnx --verify-no-changes

# Frontend  (run from src/Soulsjwa.Web/)
npm ci
npm run lint
npm run build
npm test

# Third-party notices — regenerate in the same PR as any dependency change
tools/generate_third_party_notices.sh
```

> **Important:** Do **not** run frontend build and backend build in parallel —
> static web assets conflict via concurrent `wwwroot/` updates.

Always run the appropriate lint/build/test before pushing. If you only touched
backend code, you only need backend commands; same for frontend. If you touched
shared API contracts, run **both**.

---

## 3. Agent operating principles

These are the rules an agent should not break, even under pressure to "just ship".

1. **Make the smallest change that fully solves the problem.** Surgical, complete,
   no drive-by refactors of unrelated code.
2. **No magic strings or magic numbers.** If a literal carries meaning, extract a
   named constant, enum, or type alias. See §4.
3. **Reuse types across the wire.** Shared contracts go in `Soulsjwa.Shared` (C#)
   and `src/Soulsjwa.Web/src/types/` (TS). Both ends must reference the same
   shape — never duplicate field names as ad-hoc objects in components.
4. **Prefer ecosystem tools over hand-rolled scripts.** `dotnet ef migrations add`,
   `npm install`, `npx prettier`, the LSP, refactoring tools — use them.
5. **Validate before reporting done.** Run targeted tests after every meaningful
   change; run the full suite before final commit.
6. **Don't break existing behavior.** If a test fails, fix the cause, not the test.
   Removing or weakening a test to make CI green is a regression.
7. **Don't introduce or ignore security issues.** Take CodeQL/code-review feedback
   seriously; suppress only with a written justification.
8. **Don't commit generated artifacts, `node_modules`, `bin/`, `obj/`, secrets,
   or temp files.** When in doubt, check `git status` before `report_progress`.
9. **Update docs the same PR.** See the doc-sync table in §6.
10. **Capture durable knowledge as memories**, not as files. One-shot task notes
    belong in the PR description, not as new markdown.
11. **No deviations, workarounds, or shortcuts to dodge scope.** If a feature
    needs a backend change to be implemented correctly (e.g. a new query
    parameter shape, a new endpoint, a schema column), do it. Don't paper over
    a backend limitation with client-side filtering, fake data, hidden state,
    "TODO: properly" notes, or in-UI "this is partially supported" disclaimers.
    Implement the feature end-to-end as specified. If genuine scope ambiguity
    blocks you, stop and ask — don't ship a half-solution.

---

## 4. No magic strings — the rule and the recipe

Magic strings/numbers are the #1 source of agent-introduced bugs (typos in one
of three call sites, drift between client and server, broken refactors).

**Rule:** if a literal value appears more than once, or carries domain meaning,
or maps to a server-side enum, it MUST be named.

### Recipes

- **C# enum on the wire** — define the enum in `Soulsjwa.Api` (or `Soulsjwa.Shared`
  if the connector needs it), expose it as its `nameof` string in DTOs, validate
  with `Enum.TryParse<T>(value, ignoreCase: false, out _)`. In tests, use
  `nameof(MyEnum.Value)` — never `"Value"`.
- **TypeScript mirror** — model as a `const` tuple + derived type so the list is
  iterable AND type-safe:
  ```ts
  export const TIE_BREAK_MODES = ['ByTime', 'SharedPlace'] as const
  export type TieBreakMode = (typeof TIE_BREAK_MODES)[number]
  export const TIE_BREAK_MODE_LABELS: Record<TieBreakMode, string> = { … }
  ```
  Then iterate `TIE_BREAK_MODES.map(...)` in selectors and cast `as TieBreakMode`
  exactly **once**, at the input boundary.
- **Repeated UI strings** — pull into a `*_LABELS` map adjacent to the type.
- **API route paths** — already centralized via `apiClient` baseURL (`/api/v1`)
  and per-feature `*Api.ts` modules. Don't hardcode full URLs in components.
- **Query keys** — group under a feature `*Keys` object (TanStack convention).

If you add a new enum/union, search the whole repo (`rg "'YourLiteral'"`) for
existing magic-string occurrences and fix them in the same PR.

---

## 5. Stack-specific conventions

These are the conventions most often tripped up by agents. Full convention set
in [`docs/agent-conventions/`](./docs/agent-conventions/).

### Backend (.NET 10)
- Minimal API endpoints (not controllers), feature-foldered under `Features/`.
- All routes under `/api/v1/`.
- Event-owner authorization: `EventOwnership.RequireOwner(ev, principal, action)`
  returns `null` on success or a 403 `IResult` to return directly.
- EF Core global query filter on `IsArchived` (soft-delete). Use
  `.IgnoreQueryFilters()` to access archived rows.
- Secrets via env vars only; `Jwt:Secret` must be set or `Program.cs` throws.

### Frontend (React 19, MUI v9, TanStack Query)
- **One hook per file.** File name matches the export
  (e.g. `useAllowlist.ts` exports `useAllowlist`). Under
  `src/features/<area>/hooks/`.
- MUI v9 system props (`alignItems`, `gap`, `mt`, `py`, `textAlign`, …) MUST
  go through `sx`, not as direct props on `Stack`/`Typography`.
- MUI v9 TextField: use `slotProps={{ htmlInput: {...} }}`, not `inputProps`.
- Vitest for tests (`vitest.config.ts` is intentionally separate from
  `vite.config.ts` to avoid Vite version mismatch).

### Connector
- Game data definitions (offset, dataType) live in C# only
  (`GameDataDefinitions.cs`), **not** in the DB.
- Bump `ConnectorConstants.Version` when definitions change.
- `Soulsjwa.ConnectorTests` builds on Linux (for `dotnet format`) but only
  executes on the `windows-latest` CI job.

### Security
- Never commit secrets, API keys, or production credentials.
- Dev-only secrets in `appsettings.Development.json` or devcontainer env vars.
- Swagger is gated behind `IsDevelopment()`.

---

## 6. Documentation sync table

When your changes touch an area below, update the listed doc in the **same PR**.

| Document | Update when you… |
|----------|------------------|
| `docs/database-design.md` | add/remove/alter entities, properties, relationships, indexes, migrations, or seed data |
| `docs/api-reference.md` | add/remove/change API endpoints, request/response shapes, auth requirements, or error codes |
| `docs/frontend.md` | add/remove pages, routes, components, feature modules, hooks, or change state management |
| `docs/connector/README.md` | change connector services, view models, commands, or API usage |
| `docs/connector/contract.md` | add/change anything under `src/Soulsjwa.Shared/ConnectorContracts.cs` or a `/connector/*` route — and add the route to `ConnectorRouteAvailabilityTests` |
| `docs/adr/` | make a choice a future reader would question (a dependency with a catch, a deliberate duplication, an environment-dependent behaviour) — one short record per decision |
| `docs/system-overview.md` | change auth flows, deployment topology, security architecture, or cross-cutting infrastructure |
| `docs/feature-matrix.md` | implement, partially implement, or remove any feature listed in the matrix |
| `docs/README.md` | change project layout, tech stack versions, or add/remove major project areas |
| `THIRD-PARTY-NOTICES.md` | add, remove, or upgrade any npm (`src/Soulsjwa.Web`) or NuGet dependency — run `tools/generate_third_party_notices.sh` in the same PR; CI fails if it's stale |

Rules:
- Keep Mermaid diagrams in sync with code; do not leave stale diagrams.
- New top-level feature area → add to every relevant document.
- Completed a feature marked ❌ / 🟡 in `feature-matrix.md` → flip to ✅ (or 🟡 if partial).

---

## 7. Where to look first

- **Architecture / how features hang together** → `docs/`
- **Detailed conventions per area** → `docs/agent-conventions/`
- **Existing similar code** → `rg`, `glob`, or semantic search. There is almost
  always an existing example; mimic it rather than invent.
- **Stored repository memories** (Copilot Memory) — these are surfaced into the
  agent's prompt automatically; trust the citations, not the summary.
