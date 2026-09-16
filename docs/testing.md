# Testing

Soulsjwa uses a test pyramid with xUnit, FluentAssertions, and Testcontainers.
**Which layer a test belongs to, and how to write one, is defined in
[agent-conventions/testing.md](agent-conventions/testing.md)** — that file is
the single source for the three backend layers, the "where does my test go"
questions, and the integration-test helpers (`IntegrationTestBase`,
`HandlerHarness`, `Fixtures`). This page covers running the suites and CI.

| Project | Layer | Database | Web host |
|---------|-------|----------|----------|
| `Soulsjwa.UnitTests` | Unit | none | no |
| `Soulsjwa.IntegrationTests` | Integration | PostgreSQL (Testcontainers) | no |
| `Soulsjwa.ApiTests` | API / E2E | PostgreSQL (Testcontainers) | yes |
| `Soulsjwa.ConnectorTests` | Unit (Windows) | n/a | n/a |
| `Soulsjwa.Web` (Vitest) | Frontend unit + component | n/a | n/a |

## Running Tests

```bash
# All tests
dotnet test Soulsjwa.slnx

# Unit tests only (fast, no Docker required)
dotnet test tests/Soulsjwa.UnitTests/

# Integration tests (requires Docker for Testcontainers)
dotnet test tests/Soulsjwa.IntegrationTests/

# API tests (requires Docker for Testcontainers)
dotnet test tests/Soulsjwa.ApiTests/

# Frontend tests and linting
cd src/Soulsjwa.Web && npm test && npm run lint
```

### Without Docker

Both Postgres-backed suites will use an existing server instead of starting a
container if `SOULSJWA_TEST_POSTGRES` is set:

```bash
export SOULSJWA_TEST_POSTGRES="Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=postgres"
dotnet test tests/Soulsjwa.IntegrationTests/
```

It has to be an admin connection string — the user needs `CREATE DATABASE`,
and it should point at the `postgres` maintenance database, since
`CREATE`/`DROP DATABASE` cannot run from inside the database being changed.
Everything else is unchanged: the schema template is rebuilt on each run and
every test still gets its own cloned database. CI leaves the variable unset
and uses Testcontainers.

## Test Dependencies

| Package | Purpose |
|---------|---------|
| `xunit` | Test framework |
| `FluentAssertions` | Fluent assertion library |
| `Microsoft.AspNetCore.Mvc.Testing` | `WebApplicationFactory` — API tests only |
| `Testcontainers.PostgreSql` | Disposable PostgreSQL containers |
| `Moq` | Collaborator doubles in unit tests |

## Frontend Tests (`Soulsjwa.Web`)

The frontend uses **Vitest** for unit and component tests.

### Running

```bash
cd src/Soulsjwa.Web
npm test              # one-shot test run
npm run test:watch    # watch mode
npm run test:coverage # one-shot with coverage report
```

### Setup

- Config in `vitest.config.ts` (intentionally separate from `vite.config.ts` to avoid Vite version mismatch)
- Setup file: `src/test/setup.ts` (jest-dom matchers, auto cleanup, a `matchMedia` stub, and a shim that restores jsdom's `localStorage`/`sessionStorage` on Node 25+, whose own Web Storage globals would otherwise shadow them)
- Coverage reports uploaded as CI artifacts
- `npx tsc --noEmit` is a **no-op** here — the root `tsconfig.json` is
  solution-style (`files: []` + project references). The real typecheck is
  `npm run build`, which runs `tsc -b`

## CI/CD Pipeline

Tests run automatically on every push and pull request via GitHub Actions (`.github/workflows/ci.yml`).

### Jobs

| Job | Runner | What it does |
|---|---|---|
| `backend-lint` | `ubuntu-latest` | `dotnet format --verify-no-changes` — enforces C# formatting; also `tools/generate_soulmemory_catalog.py --check`, which fails if the generated SoulMemory catalog files were hand-edited |
| `backend-unit` | `ubuntu-latest` | Builds the solution, runs `Soulsjwa.UnitTests` with TRX + coverage |
| `backend-api` | `ubuntu-latest` | Runs `Soulsjwa.IntegrationTests`, then `Soulsjwa.ApiTests`, via Testcontainers |
| `connector` | `windows-latest` | Builds the WPF connector and runs `Soulsjwa.ConnectorTests` |
| `third-party-notices` | `ubuntu-latest` | Regenerates `THIRD-PARTY-NOTICES.md` and fails on a diff |
| `frontend` | `ubuntu-latest` | `npm ci` → `lint` → `test:coverage` → `build` |

The integration suite runs **before** the API suite, deliberately: anything
that breaks the schema, the migrations or the shared container breaks both, and
the cheap suite is the one that should report it. A failure there stops the job
before the expensive suite runs.

Each `dotnet test` invocation emits a `.trx` log which is published as a GitHub check via `dorny/test-reporter`, so per-test pass/fail status is visible directly on every PR.

### Recommended required status checks

For branch protection on `main`, mark all six CI jobs as **required** status checks so a PR can't merge unless lint, build, and tests are all green:

- `Backend lint (dotnet format)`
- `Backend unit tests`
- `Backend HTTP/integration tests`
- `Connector build & tests (Windows)`
- `Third-party notices up to date`
- `Frontend lint / test / build`

### Notes

- Backend and frontend CI jobs run as **separate jobs** (not in parallel steps within the same job)
- `Soulsjwa.ConnectorTests` targets `net10.0-windows` (because the connector itself does), so it only executes on the Windows job; `EnableWindowsTargeting=true` lets it still restore/build on Linux for `dotnet format`
- `ApiTests` also boots a **Production** host in two places — `ConnectorRouteAvailabilityTests` (every connector route must exist outside Development) and `MigrateStartupTests` (`--migrate` as a child process with only a connection string) — so a route mapped conditionally on the environment, or a startup check that would break the migrate job, fails in CI rather than on a server
- Frontend and backend builds should not run concurrently locally due to shared `wwwroot/` output directory
