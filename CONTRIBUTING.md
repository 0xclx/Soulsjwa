# Contributing to Soulsjwa

Thanks for your interest in contributing! This guide covers the essentials for
getting a pull request merged.

---

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Node.js 26+](https://nodejs.org/)
- [Docker & Docker Compose](https://docs.docker.com/get-docker/) (for
  PostgreSQL and full-stack testing)
- A [Twitch Developer Application](https://dev.twitch.tv/console/apps)
  (needed only for OAuth flows)

See [`docs/getting-started.md`](docs/getting-started.md) for a full
walkthrough.

## Repo Layout

```
src/Soulsjwa.Api/         ASP.NET Minimal API (net10.0)
src/Soulsjwa.Web/         React 19 SPA (Vite 8 + MUI v9)
src/Soulsjwa.Connector/   WPF desktop connector (net10.0-windows)
src/Soulsjwa.Shared/      Shared DTOs
tests/                    xUnit + Testcontainers + Vitest
docs/                     Architecture & API docs
```

## Build & Test

```bash
# Backend
dotnet build Soulsjwa.slnx
dotnet test tests/Soulsjwa.UnitTests/
dotnet test tests/Soulsjwa.ApiTests/       # needs Docker (Testcontainers)
dotnet format Soulsjwa.slnx --verify-no-changes

# Frontend (run from src/Soulsjwa.Web/)
npm ci
npm run lint          # ESLint + Prettier
npm run build
npm test
```

All checks above must pass before a PR can be merged.

## Coding Conventions

### Backend (.NET)

- **Minimal API endpoints** — no controllers. Feature-based folder structure
  under `Features/`.
- **`dotnet format`** is the single source of formatting truth. CI rejects
  PRs that produce a diff.
- Use `EventOwnership.RequireOwner()` for event-owner authorization;
  `EventOwnership.GetUserId()` to read the `NameIdentifier` claim.
- All routes use the `/api/v1/` prefix.
- Local secrets go in `appsettings.Development.json` (git-ignored) or
  environment variables — never in `appsettings.json`. Deployments use
  environment variables only. `Jwt:Secret` must be ≥ 32 characters.

### Frontend (React / TypeScript)

- **One TanStack Query hook per file.** File name matches export
  (e.g. `useAllowlist.ts` → `useAllowlist`), located under
  `src/features/<area>/hooks/`.
- MUI v9 — system props (`alignItems`, `gap`, `mt`, …) go through `sx`,
  not as direct props on `Stack`/`Typography`.
- `noUncheckedIndexedAccess` is enabled; handle `T | undefined` from
  indexed access explicitly.
- Prettier (single quotes, no semis, trailing commas) is enforced via
  `npm run lint`.

### Connector (WPF)

- Game data definitions live in `GameDataDefinitions.cs`, **not** in the
  database. Bump `ConnectorConstants.Version` when definitions change.
- `SoulMemoryCatalogData.cs` is generated — do not hand-edit; see its header
  comment for the upstream source and how to regenerate.

## Pull Request Checklist

Use the [PR template](.github/PULL_REQUEST_TEMPLATE.md). At minimum:

- [ ] `dotnet build Soulsjwa.slnx` is clean
- [ ] `dotnet format Soulsjwa.slnx --verify-no-changes` passes
- [ ] `dotnet test tests/Soulsjwa.UnitTests/` passes
- [ ] Frontend: `npm run lint && npm run build && npm test` pass
- [ ] Docs updated if the change touches endpoints, schema, auth, routes,
      or feature matrix

## Reporting Issues

- **Bugs / features** — use the
  [issue templates](https://github.com/0xclx/Soulsjwa/issues/new/choose).
- **Security vulnerabilities** — see
  [SECURITY.md](.github/SECURITY.md) for private disclosure.

## Code of Conduct

This project follows the [Contributor Covenant](CODE_OF_CONDUCT.md). By
participating you agree to abide by its terms.

## License

Soulsjwa is released under the [GNU GPL v3 or later](LICENSE) (because the
connector links against the GPL-3 `SoulMemory` NuGet from FrankvdStam/SoulSplitter).
