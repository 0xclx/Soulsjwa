# Backend endpoints

Quick checklist for adding or changing an endpoint.

- Minimal API (`MapGet` / `MapPost` / `MapPatch` / `MapDelete`) — **not**
  controllers. Group by feature under `src/Soulsjwa.Api/Features/<Area>/Endpoints/`.
- All routes mount under `/api/v1/` — never invent a new prefix.
- Auth:
  - Pull the caller's id with `EventOwnership.GetUserId(principal)`.
  - For event-scoped writes: `EventOwnership.RequireOwner(ev, principal, action)`
    returns `null` on success or a 403 `IResult` you can `return` directly.
- DB: use `AppDbContext` via DI; soft-delete is automatic via the `IsArchived`
  global query filter — call `.IgnoreQueryFilters()` only when you really need
  archived rows.
- Cache: scoreboard responses use `OutputCache` with `CacheTags.Scoreboard`.
  Anything that changes scoreboard order MUST evict that tag.
- Validation: reject bad input with `TypedResults.ValidationProblem(...)` and
  return early. For enums on the wire, use `Enum.TryParse<T>(s, ignoreCase: false, out _)`.
- DTOs: define request/response records in the endpoint file. Don't leak EF
  entities through the API surface.
- Error responses (BE-028): every non-2xx result is either
  `Results.Problem(detail: "…", statusCode: …)` (RFC 7807
  `application/problem+json`) or an empty body. Never `Results.NotFound(string)` /
  `Results.BadRequest(string)` — those return a bare JSON string, a shape no
  other endpoint produces. The only empty-body exceptions are `401`
  (`Results.Unauthorized()`) and `204` (`Results.NoContent()`); an empty `401`
  is deliberate so a missing, malformed, revoked, and expired token are all
  indistinguishable to the caller — never add a `detail` to it.

## Read paths and list endpoints (XC-3)

> A read endpoint that returns a projection must `Select` into that
> projection in the query. `Include` is for endpoints that return the full
> graph. Every list endpoint is bounded — by pagination, by a required
> filter, or by an explicit cap.

`Include` materialises the whole object graph, tracked, then the handler
maps it down to a smaller response — that's one query per `Include` chain
(`AsSplitQuery()` doesn't reduce this) for data the response throws away.
`Select` straight into the response record issues one query with only the
columns/aggregates it actually needs (`entity.Children.Count`, a scalar
sub-select, etc.) and needs no mapping step. Add `.AsNoTracking()` too —
nothing on a read path is mutated.

Reach for `Include` only when the endpoint's contract genuinely is the full
graph (e.g. `GetEvent`'s detail response) — never for a list/summary view.

If a list endpoint's result set can grow without an inherent limit, pick
one: paginate (`PaginatedResponse<T>`, the `EventsEndpoint.ListEvents`
pattern), require a filter that bounds it (an owning id, an event id), or
apply an explicit `.Take(n)` when the other two don't fit the endpoint's
real callers (check the frontend before choosing — a filter that breaks a
page that genuinely needs the unfiltered set is worse than the endpoint
being unbounded).

## When you change schema

1. Edit the entity in `Features/.../Entities/`.
2. Map it in `Infrastructure/Data/AppDbContext.cs` (don't forget
   `HasConversion<int>()` for enums you want stored as ints).
3. Generate a migration:
   ```bash
   dotnet ef migrations add MyChange \
     --project src/Soulsjwa.Api \
     --output-dir Infrastructure/Data/Migrations
   ```
4. Update `docs/database-design.md` in the same PR.

## Tests

- A new endpoint's **HTTP contract** (route, status codes, auth, payload
  shape) → `tests/Soulsjwa.ApiTests/` using `ApiTestBase` +
  `TestAuth.CreateUserWithApiKeyAsync` + `TestAuth.CreateAuthenticatedClient`.
  A couple of tests, not one per branch.
- The **rules the handler enforces** → `tests/Soulsjwa.IntegrationTests/` using
  `IntegrationTestBase`. The handlers are static methods with explicit
  dependencies, so call the handler directly against a real `AppDbContext`;
  that is also where anything depending on a constraint, index, cascade or
  transaction has to live.
- Pure logic helpers → `tests/Soulsjwa.UnitTests/`.
- Full placement rules: [`testing.md`](./testing.md).
- Use `nameof(MyEnum.Value)` in tests, never the bare string literal.
