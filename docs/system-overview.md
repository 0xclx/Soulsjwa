# System Overview

## High-Level Architecture

```mermaid
graph TB
    subgraph "Client Layer"
        WEB["🌐 React SPA<br/>Vite + MUI v9"]
        CON["🖥️ WPF Connector<br/>.NET 10 Desktop"]
    end

    subgraph "Server Layer"
        API["⚙️ ASP.NET Minimal API<br/>.NET 10"]
    end

    subgraph "External Services"
        TW["🟣 Twitch API<br/>OAuth2 + User Info + Extension PubSub"]
        EXT["🟣 Twitch Extension<br/>viewer panel on Twitch's CDN"]
    end

    subgraph "Data Layer"
        DB[("🐘 PostgreSQL 16")]
    end

    WEB -->|"REST /api/v1/*<br/>JWT Bearer"| API
    CON -->|"REST /api/v1/connector/*<br/>X-Api-Key"| API
    API -->|"OAuth2 Code Flow"| TW
    API -->|"EF Core"| DB
    WEB -.->|"Redirect to Twitch"| TW
    EXT -->|"REST /api/v1/twitch-extension/*<br/>Twitch-issued JWT"| API
    API -.->|"PubSub 'changed' pings"| TW
```

The Twitch extension ([twitch-extension.md](twitch-extension.md)) is optional: its routes exist only when `TwitchExtension:ClientId` and `:Secret` are configured, and push pings only when `TwitchExtension:OwnerUserId` is too.

## Authentication Flow

```mermaid
sequenceDiagram
    participant U as User (Browser)
    participant W as React SPA
    participant A as API Server
    participant T as Twitch API

    U->>W: Click "Login with Twitch"
    W->>A: GET /api/v1/auth/twitch/login
    A->>A: Generate state, issue signed oauth_state cookie
    A-->>U: 302 Redirect → Twitch OAuth
    U->>T: Authorize on Twitch
    T-->>U: 302 Redirect → /api/v1/auth/twitch/callback?code=...&state=...
    U->>A: Follow redirect
    A->>T: Exchange code for access token
    T-->>A: Access token
    A->>T: GET /users (Helix API)
    T-->>A: User profile info
    A->>A: Upsert user in DB
    A->>A: Generate refresh token (hash stored in DB)
    A-->>U: 302 → /auth/callback (set HttpOnly refresh cookie)
    U->>W: /auth/callback page
    W->>A: POST /api/v1/auth/refresh (cookie)
    A-->>W: { accessToken: "..." }
    W->>W: Store JWT in memory, start refresh timer
```

## Connector Data Submission Flow

```mermaid
sequenceDiagram
    participant C as WPF Connector
    participant A as API Server
    participant DB as PostgreSQL

    Note over C: User configures Server URL + API Key

    C->>A: GET /api/v1/connector/version
    C->>A: GET /api/v1/connector/supported-games
    C->>A: GET /api/v1/connector/events (X-Api-Key)
    A-->>C: Events the key's owner competes in, with games
    C->>A: GET /api/v1/connector/games/{id}/data
    A-->>C: Data point definitions (offsets, types)

    Note over C: Connector reads live process memory<br/>(via SoulMemory) using offset definitions

    C->>A: POST /api/v1/connector/events/{id}/games/{id}/submit
    Note right of C: { data: JSON game state }

    A->>DB: Fetch pending objectives with rules
    loop For each pending objective (rules from the parsed-rule cache)
        A->>A: Evaluate JsonLogic rule against data
        alt Completion rule satisfied
            A->>DB: Insert CompletedObjective
        else Fail rule satisfied
            A->>DB: Insert FailedObjective
        end
    end
    A-->>C: { completedCount: N, failedCount: M }
    Note over C: Next tick POSTs only if the payload changed<br/>(heartbeat at least every 30 s)
```

## Token Refresh Flow

```mermaid
sequenceDiagram
    participant W as React SPA
    participant A as API Server

    Note over W: JWT expires in memory (15 min default)

    W->>W: Timer fires 60s before expiry
    W->>A: POST /api/v1/auth/refresh (httpOnly cookie)
    A->>A: Validate refresh token (hash + prefix)
    A->>A: Revoke old token, issue new pair
    A-->>W: { accessToken: "new JWT" } + new cookie
    W->>W: Update in-memory token, reschedule timer

    Note over W: On 401 response (expired before refresh)
    W->>A: POST /api/v1/auth/refresh (retry interceptor)
    A-->>W: { accessToken: "new JWT" }
    W->>W: Retry original request
```

## Deployment Architecture

```mermaid
graph TB
    subgraph "Docker Compose Stack"
        subgraph "API Container"
            RT["ASP.NET Runtime"]
            SPA["Static SPA Files<br/>(wwwroot)"]
            RT --> SPA
        end

        subgraph "Database Container"
            PG["PostgreSQL 16"]
            VOL[("postgres_data<br/>volume")]
            PG --> VOL
        end

        MVOL[("media_data<br/>volume")]
        RT -->|"content-addressed<br/>uploads"| MVOL

        RT -->|"EF Core<br/>Connection String"| PG
    end

    subgraph "External"
        USER["Browser"]
        CONN["WPF Connector"]
        TWITCH["Twitch API"]
    end

    USER -->|":8080"| RT
    CONN -->|":8080 /api/v1/connector/*"| RT
    RT -->|"OAuth2"| TWITCH
```

## Dockerfile Build Pipeline

```mermaid
graph LR
    subgraph "Stage 1: Frontend"
        N["Node 26 Alpine"]
        N --> NPM["npm ci + build"]
        NPM --> STATIC["Static files"]
    end

    subgraph "Stage 2: Connector"
        CSDK[".NET SDK 10.0 (Linux)"]
        CSDK --> CPUB["dotnet publish -r win-x64 --self-contained<br/>--single-file (EnableWindowsTargeting)"]
        CPUB --> EXE["Soulsjwa.Connector-&lt;Version&gt;-win-x64.exe<br/>+ -latest copy"]
    end

    subgraph "Stage 3: API"
        SDK[".NET SDK 10.0"]
        SDK --> RESTORE["dotnet restore"]
        RESTORE --> PUB["dotnet publish"]
    end

    subgraph "Stage 4: Runtime"
        ASP["ASP.NET 10.0, non-root UID 1654,<br/>HEALTHCHECK /health/live"]
        STATIC --> ASP
        PUB --> ASP
        EXE -->|"wwwroot/downloads/"| ASP
    end
```

## Security Architecture

| Layer | Mechanism | Details |
|-------|-----------|---------|
| **Authentication** | Twitch OAuth2 | Code flow with signed state cookie |
| **Access Tokens** | JWT Bearer | 15-min lifetime, HMAC-SHA256, stored in memory only |
| **Refresh Tokens** | httpOnly Cookie | 30-day lifetime, SHA256-hashed in DB, prefix-based lookup |
| **API Keys** | `X-Api-Key` header | `sk_` prefix, SHA256-hashed, optional expiry, revocable |
| **Rate Limiting** | Tiered | Global 100/min (auth'd) & 60/min (anon); 20/min on auth; 120/min on connector; 40/min per viewer on the Twitch extension |
| **Twitch extension tokens** | Second JWT bearer scheme | `TwitchExtension` scheme verifies the JWT Twitch issues per viewer (HS256 over the base64-decoded extension secret, several secrets allowed for rotation, 30 s skew, inbound claim mapping off); only the `/twitch-extension` group accepts it; the channel is always the token's `channel_id` claim; broadcaster routes require `role = broadcaster`; saving settings additionally requires a Soulsjwa account with the channel's Twitch id |
| **Response Compression** | Gzip + Brotli | Enabled for HTTPS responses |
| **Request Correlation** | Correlation ID middleware | Adds/propagates `X-Correlation-Id` for logs and diagnostics; an inbound value is accepted only if it is ≤128 chars and matches `^[A-Za-z0-9_.:-]+$` — anything else (oversized, malformed, or containing control characters like CR/LF) is discarded and a fresh id generated instead, so untrusted client input never reaches logs or the response header unvalidated |
| **Output Caching** | Tag-based | Score/scoreboard cached 1h; overlay scoreboard cached 5s per token; Twitch extension board cached 5s per channel (re-enabled for bearer-authenticated requests by `PerTwitchChannelCachePolicy`, safe because authentication runs before the cache) with a strong `ETag`; caches evicted on objective, live-status, and competitor-info changes; when push is configured the cache store is wrapped so a scoreboard-tag eviction also sends a PubSub ping |
| **CORS** | Configurable origin | Default: `http://localhost:5173`; a second named policy allows only the Twitch extension's `https://<clientId>.ext-twitch.tv` origin (bearer auth, no credentials) on the extension routes |
| **Headers** | Security headers | CSP, X-Content-Type-Options, X-Frame-Options, Referrer-Policy |
| **OAuth state** | Signed cookie | Short-lived (10 min) DataProtection-protected `oauth_state` cookie — no server-side session |
| **Twitch client resilience** | Timeout + retry + circuit breaker | 10s total request timeout (`Twitch:ClientTimeoutSeconds`), 4s per attempt (`Twitch:AttemptTimeoutSeconds`), up to 2 retries (`Twitch:RetryAttempts`) — retried only for the idempotent `GET /helix/users`, never for the single-use `POST /oauth2/token` code exchange; circuit breaker opens after ≥4 requests in a 30s window with a ≥50% failure ratio and stays open 15s. A failed exchange or a malformed/oversized user-info response redirects to `/auth/callback?error=twitch_unavailable` instead of a bare 500. |
| **Swagger** | Dev-only | Gated behind `IsDevelopment()` |
| **Secrets** | Environment vars | `Jwt:Secret` required; never committed to source |
| **Site Theme** | No remote resources | Background is an uploaded `MediaAsset` id, never a URL (no SSRF surface); fonts come from a server-side allowlist, no remote `@font-face`; palette colours are validated `#rrggbb` and WCAG AA (4.5:1) contrast-checked against each mode's `Default` slot; delivered to the frontend as MUI palette values only — never an injected `<style>` or `dangerouslySetInnerHTML`; every `PUT /theme` is audit-logged with the full before/after theme |
