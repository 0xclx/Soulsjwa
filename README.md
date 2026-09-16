# Soulsjwa

Soulsjwa is a Souls-like game event and objective tracking platform. It combines
an ASP.NET API, a React web application, PostgreSQL persistence, Twitch OAuth,
and a Windows desktop connector that can submit supported game-state data for
live event scoring.

The repository is intended for contributors who want to run the application
locally, operate a containerized deployment, or work on the API, frontend,
connector, tests, and documentation.

## Contents

- [Project status](#project-status)
- [Technology stack](#technology-stack)
- [Repository layout](#repository-layout)
- [Prerequisites](#prerequisites)
- [Configuration](#configuration)
- [Quick start with Docker Compose](#quick-start-with-docker-compose)
- [Local development](#local-development)
- [Desktop connector](#desktop-connector)
- [Database migrations](#database-migrations)
- [Build, lint, and test](#build-lint-and-test)
- [Health checks](#health-checks)
- [Observability (OpenTelemetry)](#observability-opentelemetry)
- [Deployment notes](#deployment-notes)
- [Legal Compliance (GDPR & DACH Region)](#-legal-compliance-gdpr--dach-region)
- [Documentation](#documentation)
- [Security and licensing](#security-and-licensing)
- [AI-assisted development](#ai-assisted-development)
- [Third-party attributions](#third-party-attributions)

## Project status

Soulsjwa is under active development. Public APIs, seeded game definitions, and
connector data contracts may evolve. See [`docs/feature-matrix.md`](docs/feature-matrix.md)
for the current feature status and known gaps.

## Technology stack

| Area | Technology |
| --- | --- |
| Backend | .NET 10, ASP.NET Minimal APIs, EF Core, Npgsql |
| Frontend | React 19, TypeScript 7, Vite 8, MUI v9, TanStack Query |
| Connector | .NET 10 WPF desktop application |
| Database | PostgreSQL 16 |
| Authentication | Twitch OAuth2, JWT access tokens, refresh-token cookies, API keys |
| Deployment | Multi-stage Dockerfile and Docker Compose |
| Testing | xUnit, Testcontainers, Vitest, ESLint, Prettier |

## Repository layout

```text
Soulsjwa.slnx
├── src/
│   ├── Soulsjwa.Api/          ASP.NET Minimal API, EF Core, auth, migrations
│   ├── Soulsjwa.Web/          React/Vite web application
│   ├── Soulsjwa.Connector/    Windows desktop connector
│   └── Soulsjwa.Shared/       DTOs and constants shared by API/connector
├── tests/
│   ├── Soulsjwa.UnitTests/
│   ├── Soulsjwa.ApiTests/         API tests backed by Testcontainers PostgreSQL
│   ├── Soulsjwa.IntegrationTests/ Integration-style tests
│   └── Soulsjwa.ConnectorTests/   Windows-targeted connector tests
├── docs/                     Architecture, API, auth, deployment, and testing docs
├── templates/legal/          Impressum + privacy policy templates (DE/EN) for operators
├── Dockerfile                Production container image for API + built frontend
├── docker-compose.yml        PostgreSQL + migrate job + application (production-style)
└── docker-compose.dev.yml    Opt-in overlay that runs the api container in Development
```

All API routes are versioned under `/api/v1/...`. Swagger UI is available only
in development.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Node.js 26+](https://nodejs.org/)
- [Docker and Docker Compose](https://docs.docker.com/get-docker/)
- A [Twitch Developer Application](https://dev.twitch.tv/console/apps) for OAuth
  sign-in flows
- Windows, if you need to run the WPF connector application or execute connector
  tests locally

## Configuration

Start by copying the example environment file:

```bash
cp .env.example .env
```

Important settings:

| Setting | Purpose |
| --- | --- |
| `POSTGRES_PASSWORD` | Password for the Docker Compose PostgreSQL service |
| `ConnectionStrings__DefaultConnection` | PostgreSQL connection string for the API |
| `JWT_SECRET` / `Jwt__Secret` | JWT signing key; use a unique value of at least 32 characters |
| `TWITCH_CLIENT_ID` / `Twitch__ClientId` | Twitch application client ID |
| `TWITCH_CLIENT_SECRET` / `Twitch__ClientSecret` | Twitch application client secret |
| `Twitch__RedirectUri` | Twitch OAuth callback URL |
| `Frontend__Url` | Public frontend origin used for CORS and redirects |
| `API_PORT` | Host port the Docker Compose `api` service is published on (default `8080`) |
| `APPLY_MIGRATIONS` | Set to `true` to apply EF Core migrations at API startup |
| `Admin__BootstrapTwitchLogin` | Optional first-admin bootstrap Twitch login |
| `OpenTelemetry__Enabled` | Off (`false`) by default in Docker Compose — set to `true` to opt in to tracing/metrics/log export; see [Observability](#observability-opentelemetry) |

For local `dotnet run` development, place development-only secrets in
`src/Soulsjwa.Api/appsettings.Development.json` or environment variables. Do not
commit real secrets.

## Quick start with Docker Compose

Docker Compose runs PostgreSQL and the production-style application container.
The container serves both the API and the built frontend.

1. Copy and edit `.env`.
2. Set at least `JWT_SECRET`, `TWITCH_CLIENT_ID`, and `TWITCH_CLIENT_SECRET`.
3. Start the stack:

```bash
docker compose up --build
```

The stack runs the api container as a deployment would (`ASPNETCORE_ENVIRONMENT`
unset, so Production): strict CSP and HSTS, the placeholder JWT secret refused,
no Development-only routes. To run the container in Development instead, add the
opt-in overlay: `docker compose -f docker-compose.yml -f docker-compose.dev.yml up --build`.

The application is available at `http://localhost:8080` by default. Set `API_PORT`
in `.env` to publish it on a different host port instead — the Compose file also
derives the default `Twitch__RedirectUri` and `Frontend__Url` from it, so update
your Twitch redirect URI accordingly if you change it. The Compose file runs a
dedicated `migrate` service first — it applies any pending EF Core migrations and
exits cleanly before the `api` service starts. No .NET SDK or `dotnet-ef` is needed
on the host.

Useful endpoints:

| Endpoint | Description |
| --- | --- |
| `http://localhost:8080/health` | Aggregate health check |
| `http://localhost:8080/health/live` | Liveness probe |
| `http://localhost:8080/health/ready` | Readiness probe, including downstream checks |
| `http://localhost:8080/api/v1/...` | Versioned API routes |

## Local development

Use this workflow when changing backend or frontend code and you want hot
reloading.

### 1. Start PostgreSQL

```bash
docker compose up -d postgres
```

### 2. Configure the API

Create or update `src/Soulsjwa.Api/appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=soulsjwa;Username=postgres;Password=postgres"
  },
  "Jwt": {
    "Secret": "local-dev-jwt-secret-32-chars!!!"
  },
  "Twitch": {
    "ClientId": "<your-twitch-client-id>",
    "ClientSecret": "<your-twitch-client-secret>",
    "RedirectUri": "http://localhost:5000/api/v1/auth/twitch/callback"
  },
  "Frontend": {
    "Url": "http://localhost:5173"
  }
}
```

### 3. Run the API

```bash
dotnet run --project src/Soulsjwa.Api
```

The API listens on `http://localhost:5000` in the default development profile.
Swagger UI is available at `http://localhost:5000/swagger`.

### 4. Run the frontend

```bash
cd src/Soulsjwa.Web
npm install
npm run dev
```

The Vite dev server listens on `http://localhost:5173` and proxies API requests
to the local API.

### 5. Configure Twitch redirects

In the Twitch Developer Console, add the redirect URI that matches how you are
running Soulsjwa:

- Local development: `http://localhost:5000/api/v1/auth/twitch/callback`
- Docker Compose: `http://localhost:8080/api/v1/auth/twitch/callback`

To try the Twitch extension against this local setup (no deployment needed),
follow [Testing on localhost](docs/twitch-extension.md#testing-on-localhost).

## Desktop connector

`src/Soulsjwa.Connector` is a Windows WPF desktop application for users who need
to submit local game-state data to the Soulsjwa API. It is not a production web
client and is not required to host the web application.

Run it on Windows with:

```bash
dotnet run --project src/Soulsjwa.Connector
```

Connector endpoints also use the `/api/v1/connector/...` prefix. The connector
reads supported game data and submits it through API-key authenticated requests;
it does not replace the hosted API/frontend deployment.

### Supported games (automatic tracking)

The connector currently supports live, automatic objective tracking for:

- Dark Souls: Remastered
- Dark Souls II: Scholar of the First Sin
- Dark Souls III
- Sekiro: Shadows Die Twice
- Elden Ring

Automatic tracking works by reading process memory of the running game while
you play — no save-file uploads or manual reporting needed. This is powered by
**[SoulMemory](https://www.nuget.org/packages/SoulMemory/)**, from
**[FrankvdStam/SoulSplitter](https://github.com/FrankvdStam/SoulSplitter)**,
which knows how to locate and decode the relevant flags/counters in each of
these games' memory (boss kills, bonfires/graces, key event flags, etc.).
Predefined objectives for each game are defined against these same data
points — see [`docs/connector/README.md`](docs/connector/README.md) for the
full protocol and how to add new data points/objectives.

> **Elden Ring requires disabling Easy Anti-Cheat.** Elden Ring ships with
> EAC, which blocks external processes (including the connector) from reading
> its memory. To play with automatic tracking:
>
> 1. Install the **[Anti-cheat Toggler](https://www.nexusmods.com/eldenring/mods/90?tab=description)**
>    mod from NexusMods.
> 2. Use it to disable EAC, then launch Elden Ring **offline**
>    (`start_protected_game.exe` is bypassed — do not use Steam's "play
>    online" path while EAC is disabled).
> 3. Start the connector as usual; it will pick up game state once the
>    process is running.
>
> Disabling EAC takes the game offline, which also disables online/multiplayer
> features for that session — re-enable EAC through the same mod when you
> want online play back.

## Database migrations

Soulsjwa uses EF Core migrations stored in
`src/Soulsjwa.Api/Infrastructure/Data/Migrations/`.

### How migrations are applied

| Environment | Migration behavior |
| --- | --- |
| Development (`dotnet run`) | Applied automatically on startup |
| Docker Compose | A dedicated `migrate` service applies migrations and exits before the `api` starts |
| Other deployments | Pass `--migrate` (apply and exit), set `APPLY_MIGRATIONS=true`, or apply manually |

> **Docker Compose operators:** no .NET SDK or `dotnet-ef` tool is needed on the host.
> The `docker-compose.yml` runs a `migrate` service that applies pending migrations and
> exits cleanly, then the `api` starts only after that completes successfully.
> Just run `docker compose up -d --build` — migrations are handled for you.

### Developer tasks (creating new migrations)

The commands below require the .NET SDK and are only needed by contributors who
change the data model — they are **not** required to run or deploy the application.

Install the EF Core CLI tool once:

```bash
dotnet tool install --global dotnet-ef
```

Create a new migration after changing entities or `AppDbContext.OnModelCreating`:

```bash
dotnet ef migrations add <MigrationName> \
  --project src/Soulsjwa.Api \
  --startup-project src/Soulsjwa.Api \
  --output-dir Infrastructure/Data/Migrations
```

Apply migrations manually (development only, SDK required):

```bash
dotnet ef database update \
  --project src/Soulsjwa.Api \
  --startup-project src/Soulsjwa.Api
```

Apply migrations and exit (no web server started — useful for scripted pre-deploy steps):

```bash
dotnet run --project src/Soulsjwa.Api -- --migrate
```

Generate an idempotent SQL script for review or controlled deployments:

```bash
dotnet ef migrations script \
  --project src/Soulsjwa.Api \
  --startup-project src/Soulsjwa.Api \
  --idempotent \
  -o migrations.sql
```

## Build, lint, and test

Backend and solution checks:

```bash
dotnet build Soulsjwa.slnx
dotnet format Soulsjwa.slnx --verify-no-changes
dotnet test tests/Soulsjwa.UnitTests/
```

Additional backend tests that require Docker/Testcontainers:

```bash
dotnet test tests/Soulsjwa.ApiTests/
dotnet test tests/Soulsjwa.IntegrationTests/
```

Frontend checks from `src/Soulsjwa.Web/`:

```bash
npm install
npm run lint
npm run build
npm test
npm run test:coverage
```

Connector tests target Windows:

```bash
dotnet test tests/Soulsjwa.ConnectorTests/
```

Before opening a pull request, run the checks relevant to the area you changed.
For more detail, see [`CONTRIBUTING.md`](CONTRIBUTING.md) and
[`docs/testing.md`](docs/testing.md).

## Health checks

The API exposes ASP.NET Core health check endpoints, unauthenticated and not
versioned under `/api/v1`:

| Endpoint | Purpose | Checks |
| --- | --- | --- |
| `GET /health` | Aggregate health | All registered checks |
| `GET /health/live` | Liveness probe | None — only confirms the process is up and serving requests |
| `GET /health/ready` | Readiness probe | Downstream dependencies tagged `"ready"` (currently: PostgreSQL connectivity) |

Each returns `200 OK` with a short JSON status payload when healthy, or
`503 Service Unavailable` when a check fails. Use `/health/live` for
liveness — it should only fail if the process itself is stuck or deadlocked
— and `/health/ready` for readiness, so orchestrators stop routing traffic to
an instance whose database connection is down without restarting it.

**Docker** — the image already declares a liveness `HEALTHCHECK` against
`/health/live` (`wget`, which the `aspnet` base image ships; `curl` does not
exist in it). To gate on readiness instead, override it in Compose:

```yaml
healthcheck:
  test: ["CMD", "wget", "-qO-", "--tries=1", "http://127.0.0.1:8080/health/ready"]
  interval: 30s
  timeout: 5s
  retries: 3
  start_period: 20s
```

**Kubernetes** — wire liveness and readiness to the two dedicated endpoints
(not `/health`, which also reflects downstream state and would cause an
unnecessary restart loop if the database briefly hiccups):

```yaml
livenessProbe:
  httpGet:
    path: /health/live
    port: 8080
  initialDelaySeconds: 10
  periodSeconds: 15
readinessProbe:
  httpGet:
    path: /health/ready
    port: 8080
  initialDelaySeconds: 5
  periodSeconds: 10
```

## Observability (OpenTelemetry)

The API is instrumented with [OpenTelemetry](https://opentelemetry.io/) for
traces, metrics, and logs, configured in
[`src/Soulsjwa.Api/Extensions/ObservabilityExtensions.cs`](src/Soulsjwa.Api/Extensions/ObservabilityExtensions.cs).
This is opt-in from an infrastructure standpoint — nothing extra needs to run
for the app to work, but pointing it at a collector gets you:

- **Traces** — incoming HTTP requests, outgoing `HttpClient` calls, EF Core
  and every Npgsql command (with exceptions recorded on the span).
- **Metrics** — ASP.NET Core request metrics, `HttpClient` metrics, .NET
  runtime metrics (GC, thread pool, etc.), and process metrics (CPU, memory).
- **Logs** — structured Serilog logs, also exported through the OpenTelemetry
  logging pipeline (with formatted message, scopes, and parsed state values).

All three signals export via OTLP. Configure the destination with:

| Setting | Purpose | Default |
| --- | --- | --- |
| `OpenTelemetry__Enabled` | Set to `false` to disable OpenTelemetry entirely | `true` (`false` in Docker Compose — see below) |
| `OpenTelemetry__Endpoint` | OTLP collector endpoint | `http://localhost:4317` |
| `OpenTelemetry__ApiToken` | Optional bearer/API token | none |
| `OpenTelemetry__ServiceName` | Service name reported on spans/metrics | `Soulsjwa.Api` |
| `OpenTelemetry__ServiceVersion` | Service version reported on spans/metrics | `1.0.0` |

Setting `OpenTelemetry__Enabled=false` skips tracing, metrics, and the
OpenTelemetry logging pipeline entirely — no OTLP exporters are registered and
no export attempts (successful or failed) happen. Console/structured Serilog
logging is unaffected either way.

The stock `docker-compose.yml` sets `OpenTelemetry__Enabled` to `false` by
default for both the `migrate` and `api` services, so a fresh Compose
deployment never attempts to reach a collector unless you opt in. Set
`OpenTelemetry__Enabled=true` in `.env` to enable it there.

By default (no `ApiToken`), the exporter uses OTLP/gRPC against
`localhost:4317` — a standard local [OpenTelemetry
Collector](https://opentelemetry.io/docs/collector/) endpoint. If
`OpenTelemetry__ApiToken` is set, the exporter switches to OTLP/HTTP
(protobuf) and sends `Authorization: Api-Token <token>`, which many managed
observability backends accept directly without a local collector in front of
them. Point `OpenTelemetry__Endpoint` at that backend's OTLP/HTTP ingest URL
in that case.

In `Development`, traces and metrics are additionally written to the console
regardless of collector configuration, so you can see instrumentation working
without standing up a collector.

If you don't configure a real collector, the app still runs fine — failed
OTLP exports are logged and otherwise harmless.

## Deployment notes

The supported production deployment path is the Docker image built by the root
`Dockerfile`, typically orchestrated with Docker Compose or a comparable
container platform. The image builds the frontend, publishes the API, and serves
static frontend assets from the API container.

Production operators should:

- Set `ASPNETCORE_ENVIRONMENT=Production`.
- Use a strong, unique `Jwt__Secret`/`JWT_SECRET` generated by a secure random
  source.
- Configure Twitch OAuth callback URLs for the deployed domain.
- Configure `Frontend__Url` to the deployed frontend origin.
- Use HTTPS at the edge and protect database access.
- Decide whether migrations are applied automatically (`APPLY_MIGRATIONS=true`)
  or through a reviewed SQL/manual process.
- Monitor `/health`, `/health/live`, and `/health/ready` — see
  [Health checks](#health-checks).
- Optionally point [OpenTelemetry](#observability-opentelemetry) at a
  collector for traces, metrics, and logs.

See [`docs/deployment.md`](docs/deployment.md) for a fuller deployment and
security checklist.

## ⚖️ Legal Compliance (GDPR & DACH Region)

If you host this platform publicly, especially within the EU (or targeting
users in Germany, Austria, and Switzerland), you are legally required to
provide legal and privacy notices.

By default, these links are hidden. Once an admin configures them in the
dashboard (`/admin/legal`), the links will automatically appear in the global
website footer.

### 📋 What you need to prepare:

1. **Impressum (Legal Notice):** Required in Germany (§ 5 DDG) and Austria
   (§ 5 ECG).
   - *Minimum info needed:* Your full legal name, a physical address (no P.O.
     Box), and a direct email address.
2. **Datenschutzerklärung (Privacy Policy):** Required by the GDPR.
   - *Why you need it:* Even without cookies or public logins, your web
     server processes visitor IP addresses in its access logs.
   - *What to include:* Mention who hosts the site, how server logs are
     handled, and how data from the streamer leaderboard is processed.

### 📄 Baseline templates

[`templates/legal/`](templates/legal/) ships a starting point for both
documents in **German and English**, written against what this codebase
actually does — Twitch OAuth (`user:read:email`) and the allowlist, the two
strictly necessary cookies, server logs and rate limiting, the public
scoreboard data, overlay tokens, image uploads, the audit log, and the desktop
connector. The admin editor at `/admin/legal` links them directly.

Every identifying detail is a `<placeholder>` you must replace, and the
templates cover the **application** only. Anything you put in front of or
around it is yours to declare — in particular:

- **Cloudflare or any other CDN, reverse proxy, load balancer, or WAF.** It
  terminates TLS and sees every visitor IP, so it is a separate recipient (and
  usually a third-country transfer). The privacy templates ship a clearly
  marked section for exactly this — fill it in or delete it.
- Your hosting provider (plus an Art. 28 GDPR data processing agreement),
  analytics or error tracking, and an OpenTelemetry backend if you enabled one
  (it is off by default in the stock `docker-compose.yml`).

*Note: As the software creator, I provide the tools and a template to add these
pages, but ensuring the accuracy of the content and maintaining compliance is
the sole responsibility of the person hosting the instance. The templates are
not legal advice.*

## Documentation

The `docs/` directory contains deeper references:

| Document | Description |
| --- | --- |
| [`docs/getting-started.md`](docs/getting-started.md) | Local setup walkthrough |
| [`docs/system-overview.md`](docs/system-overview.md) | Architecture and data flow |
| [`docs/api-reference.md`](docs/api-reference.md) | Endpoint reference |
| [`docs/auth.md`](docs/auth.md) | Twitch OAuth, JWT, refresh tokens, API keys, authorization model |
| [`docs/database-design.md`](docs/database-design.md) | Schema and relationships |
| [`docs/frontend.md`](docs/frontend.md) | Web application structure |
| [`docs/connector/README.md`](docs/connector/README.md) | Connector behavior and protocol |
| [`docs/deployment.md`](docs/deployment.md) | Deployment guide |
| [`docs/testing.md`](docs/testing.md) | Test strategy and commands |
| [`docs/feature-matrix.md`](docs/feature-matrix.md) | Feature status and known gaps |
| [`docs/streamer-overlay.md`](docs/streamer-overlay.md) | OBS browser-source overlay |
| [`docs/twitch-extension.md`](docs/twitch-extension.md) | The viewer-facing Twitch extension and its backend |
| [`docs/connector/contract.md`](docs/connector/contract.md) | The connector ↔ API contract and the tests that guard it |
| [`docs/adr/`](docs/adr/README.md) | Decision records for the non-obvious choices |
| [`docs/history/`](docs/history/README.md) | Completed review backlogs and superseded documents, kept for their reasoning |

## Security and licensing

- Report security vulnerabilities using the private process in
  [`.github/SECURITY.md`](.github/SECURITY.md).
- Never commit production secrets, real Twitch client secrets, JWT signing keys,
  database passwords, or generated migration scripts containing sensitive data.
- Soulsjwa is licensed under GPL-3.0-or-later; see [`LICENSE`](LICENSE).
- The connector binary links against
  [`SoulMemory`](https://www.nuget.org/packages/SoulMemory/) (GPL-3), so the
  repository is distributed under GPL-3-compatible terms.


## AI-assisted development

Parts of this project — including code, tests, and documentation — have been
developed with the assistance of AI tools, including Anthropic's Claude
(Claude Code). AI-assisted contributions are reviewed by maintainers before
being merged, but if you notice anything that looks off, please open an
issue or pull request.

## Third-party attributions

The connector depends on
**[FrankvdStam/SoulSplitter](https://github.com/FrankvdStam/SoulSplitter)**'s
[`SoulMemory`](https://www.nuget.org/packages/SoulMemory/) NuGet for live
in-process reading of Dark Souls Remastered, Dark Souls II: Scholar of the First
Sin, Dark Souls III, Sekiro: Shadows Die Twice, and Elden Ring.
The complete pinned-source capability audit is in
[`docs/connector/soulmemory-capability-audit.md`](docs/connector/soulmemory-capability-audit.md).
SoulMemory is
**GPL-3 licensed**; including it in the connector binary is the reason this
repository as a whole is GPL-3-licensed.
