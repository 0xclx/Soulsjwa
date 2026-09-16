# Connector ↔ API contract

The desktop connector and the API are built from the same repository but
ship separately: a connector installed months ago talks to whatever the
server runs today. This page is the contract between them and how it is
guarded.

## Shared types

Every response the connector reads is declared once, in
[`src/Soulsjwa.Shared/ConnectorContracts.cs`](../../src/Soulsjwa.Shared/ConnectorContracts.cs),
and compiled into **both** `Soulsjwa.Api` (which serializes them) and
`Soulsjwa.Connector` (which deserializes them). A field renamed or removed
on one side is a compile error on the other. The connector keeps no private
copies of these records — it once did, one drifted (an events list that no
longer carried games), and no test noticed until the connector could not
select a game against any server.

| Type | Route |
|---|---|
| `ConnectorVersionResponse` | `GET /api/v1/connector/version` |
| `ConnectorSupportedGameResponse[]` | `GET /api/v1/connector/supported-games` |
| `ConnectorEventResponse[]` (with `ConnectorEventGameResponse[]`) | `GET /api/v1/connector/events` |
| `ConnectorGameDataResponse` (with `GameDataPoint[]`) | `GET /api/v1/connector/games/{gameId}/data` |
| `ConnectorSubmissionPayload` → `ConnectorDataSubmissionResult` | `POST /api/v1/connector/events/{eventId}/games/{eventGameId}/submit` |

Wire format: System.Text.Json web defaults (camelCase). `ApiServiceTests`
deserializes a literal camelCase payload so a serializer-option change on
either side fails a test.

## Connect sequence

1. `GET /connector/version` (anonymous). The connector refuses to continue if
   `ConnectorConstants.Version` is older than `requiredVersion`.
2. `GET /connector/supported-games` (anonymous). Which known games can be
   monitored.
3. `GET /connector/events` (**authenticated** — `X-Api-Key`). The events the
   key's owner competes in, each with its games. This is the first
   authenticated call, so an invalid or revoked key fails here with
   "Unauthorized". Archived events are never listed; `isStarted: false`
   events are listed so the connector can explain why submissions are
   refused.
4. On game selection: `GET /connector/games/{knownGameId}/data` for the
   data-point definitions the adapter reads.
5. While watching: `POST …/submit` with the serialized state. Only when the
   payload changed since the last accepted submission, plus a heartbeat at
   least every 30 s. The response's `completedCount`/`failedCount` are what
   the status line reports.

## Rules that must hold

- **Every connector route exists in every environment.** Nothing the
  connector calls may be mapped conditionally on `IsDevelopment()`.
  `tests/Soulsjwa.ApiTests/ConnectorRouteAvailabilityTests.cs` boots a
  Production host and calls each route; a `404` fails it. Add a row there
  for every new connector route.
- **The user is the API key's owner.** Submissions carry no user id; the
  server attributes them to the key. A competitor cannot submit for
  another.
- **Only competitors can submit** (`403` otherwise), only to started events
  (`403` "not started") and, for official records, only to the event's
  enabled game — a competitor's own running trial run lifts that last rule.
- **Versioning.** `ConnectorConstants.Version` is the only version. The
  connector csproj derives its assembly version and the download's zip
  name from it; `games.json`'s per-game `requiredConnectorVersion` may not
  exceed it (unit-tested). Bump it whenever data definitions, predefined
  objectives or the submission contract change.

## Changing the contract

1. Change the record in `ConnectorContracts.cs`; fix both compile errors.
2. If a route is added: map it in `ConnectorEndpoint`, add it to
   `ConnectorRouteAvailabilityTests`, and to the tables above and in
   [api-reference.md](../api-reference.md#connector-endpoints).
3. If an old connector would misread the new shape, bump
   `ConnectorConstants.Version` so it refuses to connect until updated.
