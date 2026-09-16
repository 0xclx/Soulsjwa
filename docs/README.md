# Soulsjwa — Documentation

Soulsjwa is a **Souls-like game event & objective tracking platform** with Twitch authentication, real-time game-state evaluation via a desktop connector, and a competitive scoreboard system.

## Quick Links

| Document                                                        | Description                                                                                    |
| --------------------------------------------------------------- | ---------------------------------------------------------------------------------------------- |
| [System Overview](system-overview.md)                           | High-level architecture, deployment, and data-flow diagrams                                    |
| [Database Design](database-design.md)                           | Complete schema, ER diagram, relationships, indexes, and seed data                             |
| [API Reference](api-reference.md)                               | Every endpoint with method, route, auth, request/response shapes                               |
| [Frontend](frontend.md)                                         | All web pages, components, features, and state management                                      |
| [Auth](auth.md)                                                 | Twitch OAuth2, JWT tokens, API keys, refresh flow, authorization model                         |
| [Connector](connector/README.md)                                | WPF desktop app — supported games, workflow, and game data protocol                            |
| [Connector — SoulMemory audit](connector/soulmemory-capability-audit.md) | Pinned upstream file/capability audit, catalogs, and intentional exclusions              |
| [Feature Matrix](feature-matrix.md)                             | What's done, what's partial, and what's missing                                                |
| [Getting Started](getting-started.md)                           | Prerequisites, local setup, running with Docker                                                |
| [Testing](testing.md)                                           | Testing strategy, running tests, CI/CD pipeline                                                |
| [Deployment](deployment.md)                                     | Docker, environment variables, production configuration                                        |
| [Streamer Overlay](streamer-overlay.md)                         | OBS browser-source overlay route and the URL params that configure it                          |
| [Twitch Extension](twitch-extension.md)                         | The viewer-facing Twitch extension: what it shows, the backend routes, configuration, build and Twitch console setup |
| [Connector — API contract](connector/contract.md)               | The routes and shared response types the desktop connector depends on, and the tests that guard them |
| [Decision records](adr/README.md)                               | Short records of the non-obvious choices (dual TypeScript packages, static handlers, compose overlay, …) |
| [Agent Conventions](agent-conventions/README.md)                | Convention files for AI coding agents                                                          |
| [History](history/README.md)                                    | Completed review backlogs (`BE-nnn`/`FE-nnn` ids cited elsewhere), an unimplemented design proposal, and old UI walkthroughs — rationale, not current state |

## Tech Stack

```
┌─────────────────────────────────────────────────────────────┐
│                        Tech Stack                           │
├─────────────────┬───────────────────────────────────────────┤
│ Backend API     │ .NET 10 · Minimal API · EF Core · Npgsql │
│ Frontend        │ React 19 · TypeScript ~7.0 · Vite 8 · MUI v9 │
│ Connector       │ .NET 10 WPF · CommunityToolkit.Mvvm      │
│ Database        │ PostgreSQL 16                             │
│ Auth            │ Twitch OAuth2 · JWT · API Keys            │
│ Deployment      │ Docker Compose · Multi-stage Dockerfile   │
│ Testing         │ xUnit · Testcontainers · Vitest           │
└─────────────────┴───────────────────────────────────────────┘
```

## Project Layout

```
Soulsjwa.slnx                        # SDK-style solution
├── src/
│   ├── Soulsjwa.Api/                 # ASP.NET Minimal API (net10.0)
│   │   ├── Common/                   # ApiRoutes, AdminAccess, ConcurrencyToken (ETag), UtcTime, cache tags, rate-limit policies
│   │   ├── Features/                 # One folder per area: Endpoints/, Entities/, Services/
│   │   │   ├── Admin/                # Allowlist, user roles, feature flags
│   │   │   ├── Audits/               # Per-event and admin activity logs
│   │   │   ├── Auth/                 # Twitch OAuth callback, refresh/revoke
│   │   │   ├── Calendar/             # Event calendar entries, planned runs, the global calendar
│   │   │   ├── Connector/            # Connector version, supported games, events, data submission
│   │   │   ├── Events/               # Events, competitors, games, objectives, outcomes, scoreboard, trial runs, overlay tokens, rules
│   │   │   ├── Games/                # Game catalog, data definitions, generated SoulMemory catalogs, rule evaluator, seeder
│   │   │   ├── Legal/                # Impressum / Datenschutz documents
│   │   │   ├── Media/                # Content-addressed image uploads
│   │   │   ├── Theme/                # Admin-defined site theme
│   │   │   ├── TwitchExtension/      # Twitch extension backend: viewer token scheme, channel settings, push notifier
│   │   │   └── Users/                # Profile, API keys, user search
│   │   └── Infrastructure/
│   │       ├── Auth/                 # JWT service, Twitch service, API key handler, overlay token auth, OAuth state cookie
│   │       ├── Data/                 # AppDbContext, migrations, seed data, retention service
│   │       └── Diagnostics/          # ActivitySource, meters, log event ids
│   ├── Soulsjwa.Web/                 # React SPA (Vite + MUI)
│   │   └── src/
│   │       ├── pages/                # Route components (Home, Events, event child pages, Calendar, My Events, Admin, Legal, Overlay, …)
│   │       ├── features/             # admin, audits, auth, calendar, events, legal, markdown, media, myEvents, theme, users
│   │       ├── twitch-extension/     # The Twitch-hosted extension bundle (second Vite build, see docs/twitch-extension.md)
│   │       ├── components/           # AppShell, EventLayout, AdminLayout, ProtectedRoute, shared UI primitives
│   │       ├── lib/                  # Axios client + token/ETag interceptors, React Query client
│   │       ├── routes/               # React Router route definitions
│   │       └── test/                 # Vitest setup
│   ├── Soulsjwa.Connector/          # WPF desktop app (net10.0-windows)
│   │   ├── Services/                # ApiService, ConfigurationService (DPAPI-protected key), GameDataReaderFactory, Adapters/ (one SoulMemory adapter per game)
│   │   └── ViewModels/              # MainViewModel (MVVM)
│   └── Soulsjwa.Shared/             # Compiled into both API and connector: ConnectorConstants (the version), GameIds, GameDataPoint, ConnectorContracts (every /connector/* response type)
├── tests/
│   ├── Soulsjwa.UnitTests/          # No I/O
│   ├── Soulsjwa.IntegrationTests/   # Handlers called directly against a real Postgres (Testcontainers or SOULSJWA_TEST_POSTGRES)
│   ├── Soulsjwa.ApiTests/           # HTTP contract via WebApplicationFactory; also boots Production hosts and the --migrate path
│   └── Soulsjwa.ConnectorTests/     # Windows-only
├── docs/                             # Documentation (you are here)
│   ├── agent-conventions/            # Convention files for AI agents
│   ├── adr/                          # Decision records
│   ├── connector/                    # Connector docs incl. the API contract
│   └── history/                      # Completed backlogs, an unimplemented proposal, old flow screenshots
├── templates/legal/                  # Impressum + Datenschutz/privacy templates (DE/EN) linked from /admin/legal
├── tools/                            # generate_soulmemory_catalog.py, generate_soulmemory_boss_data.py, generate_third_party_notices.sh
├── .config/dotnet-tools.json         # Local .NET tool manifest (dotnet-ef, dotnet-project-licenses)
├── THIRD-PARTY-NOTICES.md            # Generated npm + NuGet license attribution — regenerate via tools/generate_third_party_notices.sh
├── Dockerfile                        # Multi-stage: Node → connector publish → API publish → ASP.NET runtime
├── docker-compose.yml                # Postgres + migration job + API orchestration
└── docker-compose.dev.yml            # Opt-in overlay: api container in Development
```

Identifiers like `BE-025` or `FE-013` in docs and code comments refer to items of the completed review backlogs under [history/](history/README.md).
