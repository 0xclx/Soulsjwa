# Testing

## The three backend layers

| Project | What belongs there |
|---------|--------------------|
| `tests/Soulsjwa.UnitTests/` | One unit, collaborators doubled, **no I/O**. Pure functions, validators, predicates, ranking maths, URL/regex/format handling. Fastest loop. |
| `tests/Soulsjwa.IntegrationTests/` | Real components wired together across the persistence boundary against a real Postgres, with **no web host and no HTTP**. Constraints, indexes, cascades, transactions, advisory locks, concurrency, EF query translation, keyset pagination, `timestamptz`/`jsonb` round-trips — plus multi-component business logic, which is most of it. Uses `IntegrationTestBase`. |
| `tests/Soulsjwa.ApiTests/` | Drives the public HTTP surface as a client: routing, status codes, authorization, payload shape, headers, middleware, wire contracts like `ETag`/`If-Match`. A couple of tests per endpoint proving the contract and that the logic is wired — **not** an enumeration of business rules. Uses `ApiTestBase`. |
| `tests/Soulsjwa.ConnectorTests/` | WPF connector, `net10.0-windows`. Builds on Linux (for `dotnet format`) but only EXECUTES on the `windows-latest` CI job. |
| `src/Soulsjwa.Web/` (Vitest) | Frontend unit + component tests. |

## Where does my test go?

Two questions, in order:

1. **Would it still be meaningful without the HTTP pipeline?** If yes, it does
   not belong in `ApiTests`. The minimal-API handlers are static methods with
   explicit dependencies, so an integration test can call them directly;
   running them through a web host only makes the test slower and worse at
   saying what broke.
2. **Does it need a database at all?** If yes, it is an integration test. The
   unit project has no database provider (the EF InMemory provider was
   retired: it had no constraints, no transactions and no SQL translation, so
   a test that depended on relational behaviour and ran on it asserted
   nothing, and production code carried `IsRelational()` guards just to run
   on it). `TestHelper.CreateUnusedDbContext()` only satisfies a constructor
   in a test that never touches the context.

The pyramid is the goal: many unit tests, a solid integration layer, a thin
API/E2E cap. A business rule with six branches gets six integration tests and
at most one API test.

## Integration tests
- Inherit from `IntegrationTestBase` to get `CreateDbContext()` (a fresh
  `AppDbContext` on this test's own cloned database, disposed for you) and
  `ConnectionString` for the rare raw-connection case.
- **Call the endpoint handlers directly.** They are `internal static` rather
  than `private` for exactly this reason, and each endpoint class says so in
  its own doc comment. Pass `Audit` (a real `AuditService`), `Cache` (a
  `RecordingCacheStore`, which also records which tags a handler evicted),
  `NullLogger<T>.Instance`, and `user.Principal()` for the caller.
- Read the answer with the `HandlerHarness` extensions: `result.Status()`,
  `result.Detail()`, `result.ValidationErrors()`, `result.Value<T>()`. They
  read an `IResult` the way an assertion used to read an
  `HttpResponseMessage`, so a moved test stays as legible as it was.
- `Fixtures.AddEventAsync(db, …)` seeds owner, competitor, event, game and
  objective in one call, with the preconditions the handlers check
  (`started`, `enabled`, `allowTrialRuns`) as named dials. Use it rather than
  arranging the same state through five endpoint calls.
- `CreateDbContext(interceptor)` attaches EF interceptors, for the tests whose
  subject is the SQL itself — how many commands a read issues, or which kind.
- Call `CreateDbContext()` **more than once** when the point of the test needs
  separate change trackers — proving a constraint is enforced by the database
  rather than by EF's identity map, or reading back what another context
  wrote. A duplicate rejected by the identity map proves nothing about
  Postgres.
- `CreateServiceProvider()` gives a minimal container with `AppDbContext`
  registered, for the components that take an `IServiceScopeFactory` because
  they own their own unit of work (background services).
- The schema comes from `Database.MigrateAsync()` into a template database
  that every test's database is cloned from. Not `EnsureCreated`: that replays
  only the model snapshot, so raw SQL in a migration — `CREATE EXTENSION
  pg_trgm`, the functional index on `LOWER("TwitchLogin")` — never runs.
  Migration-seeded rows (the `Games` catalogue, the `SiteTheme` singleton,
  feature flags) are therefore already present; don't insert your own with
  colliding keys.

## API tests
- Inherit from `ApiTestBase` to get `Factory` (a `WebApplicationFactory<Program>`)
  and `Client` (an unauthenticated `HttpClient`).
- Authenticate via `TestAuth.CreateUserWithApiKeyAsync(Factory.Services)` then
  `TestAuth.CreateAuthenticatedClient(Factory, apiKey)` — uses the `X-Api-Key`
  header.
- Seed data through a fresh `IServiceScope` and `AppDbContext`.
- One shared Postgres server for the assembly; each test gets its own database
  cloned from a migrated template. Don't add a container.

## Anti-patterns
- ❌ Removing or weakening a failing test to make CI green. Fix the cause.
- ❌ Asserting a business rule over HTTP when the rule doesn't involve HTTP.
  That is the slowest possible way to learn a predicate is wrong, and it puts
  the failure four layers from the cause.
- ❌ Adding a fake database provider to the unit project. Relational
  behaviour — a unique index, a cascade, a transaction — is asserted on
  Postgres in `Soulsjwa.IntegrationTests`, nowhere else.
- ❌ Bare string literals for enum values:
  `body.Status.Should().Be("Active")` — use `nameof(MyEnum.Active)`.
- ❌ Shared mutable state between tests. Each test should seed what it needs.
- ❌ `await Task.Delay(...)` to wait for async work. Await the actual operation
  or poll a condition.

## Running

```bash
# Backend — fast loop, no Docker
dotnet test tests/Soulsjwa.UnitTests/

# Backend — integration layer (needs Docker)
dotnet test tests/Soulsjwa.IntegrationTests/

# Backend — full (skip ConnectorTests on Linux, they require Windows)
dotnet test Soulsjwa.slnx

# Frontend
cd src/Soulsjwa.Web && npm test -- --run
```

No Docker? Point both Postgres-backed suites at a server you already have:

```bash
export SOULSJWA_TEST_POSTGRES="Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=postgres"
```

It must be an admin connection string — the user needs `CREATE DATABASE`, and
it should point at the `postgres` maintenance database. Nothing else changes:
the schema template is still built and every test still gets its own cloned
database. Leave it unset and Testcontainers starts a container as before.

If you only changed code in one area, run only that area's tests during your
inner loop. Run the full suite once before finalizing.

CI runs the integration suite **before** the API suite in the same job:
anything that breaks the schema, the migrations or the shared container breaks
both, and the cheap suite is the one that should report it.
