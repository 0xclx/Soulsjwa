# Deployment

Soulsjwa is designed for containerized deployment using Docker with a multi-stage build.

## Docker Architecture

The application uses a multi-stage Dockerfile that produces a single container serving the API, the frontend, and the connector download:

```
Stage 1: frontend-build (Node 26 Alpine)
  └── Builds React app with Vite → outputs to wwwroot/

Stage 2: connector-build (.NET 10 SDK)
  └── Publishes self-contained Windows connector zip to wwwroot/downloads/

Stage 3: api-build (.NET 10 SDK)
  ├── Restores NuGet dependencies
  ├── Copies built frontend from Stage 1
  ├── Copies connector download from Stage 2
  └── Publishes .NET app to /publish

Stage 4: runtime (ASP.NET 10 Runtime)
  ├── Copies published output
  ├── Exposes port 8080
  └── Runs the API (serves API + frontend + connector download)
```

## Docker Compose

The `docker-compose.yml` defines three services:

### PostgreSQL

```yaml
postgres:
  image: postgres:16-alpine
  environment:
    POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
    POSTGRES_DB: soulsjwa
  volumes:
    - postgres_data:/var/lib/postgresql/data
  healthcheck:
    test: ["CMD-SHELL", "pg_isready -U postgres"]
    interval: 5s
    timeout: 5s
    retries: 5
```

### Migration job

```yaml
migrate:
  build: .
  image: soulsjwa-api
  command: ["--migrate"]
  depends_on:
    postgres:
      condition: service_healthy
  environment:
    ConnectionStrings__DefaultConnection: "Host=postgres;Database=soulsjwa;..."
```

### API (+ Frontend + connector download)

```yaml
api:
  image: soulsjwa-api
  ports:
    - "8080:8080"
  volumes:
    - media_data:/data/media
  depends_on:
    postgres:
      condition: service_healthy
    migrate:
      condition: service_completed_successfully
  environment:
    ConnectionStrings__DefaultConnection: "Host=postgres;Database=soulsjwa;..."
    Jwt__Secret: "${JWT_SECRET}"
    Twitch__ClientId: "${TWITCH_CLIENT_ID}"
    Twitch__RedirectUri: "http://localhost:8080/api/v1/auth/twitch/callback"
```

`media_data` persists uploaded images (`MediaAsset`/`MediaStore`), stored content-addressed by SHA-256 under `/data/media` — `Media__RootPath` in the Dockerfile. The runtime image creates that directory owned by the non-root `app` user at build time; Docker copies that ownership onto the named volume the first time it's mounted, which is what keeps it writable after the volume takes over the mount point.

## Environment Variables

### Required for the stock Docker Compose file

| Variable | Description |
|----------|-------------|
| `POSTGRES_PASSWORD` | PostgreSQL password; defaults to `postgres` if omitted |
| `JWT_SECRET` | JWT signing key that Compose maps to `Jwt__Secret` (minimum 32 characters) |
| `TWITCH_CLIENT_ID` | Twitch OAuth2 application client ID; Compose maps to `Twitch__ClientId` |
| `TWITCH_CLIENT_SECRET` | Twitch OAuth2 application client secret; Compose maps to `Twitch__ClientSecret` |
| `ALLOWED_HOSTS` | ASP.NET Core host filtering; Compose maps to `AllowedHosts`. Defaults to `*` (accepts any `Host` header) — **must** be set to the deployment's real hostname(s) in production (BE-037). Leaving it at `*` outside `Development` logs a startup warning. |

The stock Compose file sets `Twitch__RedirectUri=http://localhost:8080/api/v1/auth/twitch/callback` and `Frontend__Url=http://localhost:8080` for local Docker runs. Override those service environment values for production domains.

### Required .NET configuration keys for other deployment styles

| Variable | Description |
|----------|-------------|
| `ConnectionStrings__DefaultConnection` | Full PostgreSQL connection string |
| `Jwt__Secret` | JWT signing key (minimum 32 characters) |
| `Twitch__ClientId` | Twitch OAuth2 application client ID |
| `Twitch__ClientSecret` | Twitch OAuth2 application client secret |
| `Twitch__RedirectUri` | OAuth2 callback URL |
| `Frontend__Url` | Frontend URL for CORS configuration and auth redirects |

### Optional

| Variable | Default | Description |
|----------|---------|-------------|
| `Admin__BootstrapTwitchLogin` | unset | First matching Twitch login becomes Admin + allowlisted, only while no admin exists |
| `TwitchExtension__ClientId` | unset | Client ID of the Twitch extension this server backs; together with a secret it turns the extension backend on. Compose: `TWITCH_EXTENSION_CLIENT_ID`. See [twitch-extension.md](twitch-extension.md) |
| `TwitchExtension__Secret` (or `__Secrets__0`, `__1`, …) | unset | The extension secret as shown in the Twitch console (base64); list several during a rotation. Compose: `TWITCH_EXTENSION_SECRET` |
| `TwitchExtension__OwnerUserId` | unset | Numeric Twitch user id of the extension's owner; enables push updates to viewers through Extension PubSub. Compose: `TWITCH_EXTENSION_OWNER_USER_ID` |
| `TwitchExtension__LocalTestOrigin` | unset | Extra CORS origin (e.g. `https://localhost:8080`) for Twitch's Local Test mode; development only |
| `TwitchExtension__BundlePath` | `twitch-extension` | Directory with the built extension pages, relative to the API's content root; the Docker image ships them there |
| `TwitchExtension__ApiUrl` | `Frontend__Url` | Origin written into the extension zip an admin downloads; set it when the API is reached on a different host than the frontend |
| `RateLimits__TwitchExtensionPerViewerPerMinute` | `40` | Requests per minute per extension viewer |
| `APPLY_MIGRATIONS` | `false` | Apply EF Core migrations on startup; prefer the `--migrate` flag or the Docker Compose `migrate` service instead |
| `Jwt__Issuer` | `soulsjwa` | JWT token issuer |
| `Jwt__Audience` | `soulsjwa` | JWT token audience |
| `Jwt__AccessTokenMinutes` | `15` | Access token lifetime |
| `Jwt__RefreshTokenDays` | `30` | Refresh token lifetime |
| `ASPNETCORE_ENVIRONMENT` | `Production` | .NET environment |
| `AllowedHosts` | `*` | ASP.NET Core host filtering (BE-037); set to the deployment's real hostname(s) outside `Development` — see the Compose table above for the equivalent `ALLOWED_HOSTS` mapping |
| `Retention__IntervalHours` | `24` | How often the retention background service runs a pass |
| `Retention__RefreshTokenGraceDays` | `7` | Days past `ExpiresAt` before a refresh token is deleted; a revoked-but-unexpired token is never deleted early, so reuse detection can still fire for it |
| `Retention__BatchSize` | `5000` | Rows deleted per batch, per table, so a first run against a large table doesn't hold one long delete |
| `Retention__AuditRetentionDays` | unset (keep forever) | Delete `AuditLogs` rows older than this many days; audit retention is a policy the operator opts into, not a default the code imposes |

Retention runs safely with more than one API instance: it takes a Postgres
advisory lock (`pg_try_advisory_lock`, non-blocking) for the duration of a
pass, so only one instance performs a given pass and the others skip it.

## Running in Production

### With Docker Compose

1. Create a `.env` file from `.env.example`
2. Fill in all required values; for production domains, override the API service values for `Twitch__RedirectUri` and `Frontend__Url`
3. Run:

```bash
docker compose up -d --build
```

The stock `docker-compose.yml` runs the `api` container with no
`ASPNETCORE_ENVIRONMENT` set, i.e. Production: strict CSP, HSTS, the placeholder
JWT secret refused, Swagger and other Development-only routes absent. The
`docker-compose.dev.yml` overlay switches the container to Development and is only
merged when named explicitly (`-f docker-compose.yml -f docker-compose.dev.yml`);
never use it for a deployment.

The `migrate` service needs only the connection string. `--migrate` skips the
JWT-secret and Twitch-credential checks, since that process never signs a token
or talks to Twitch; the `api` service still enforces them at startup.

### Database Migrations

**Docker Compose deployments:** no .NET SDK or `dotnet-ef` is required on the host.
The `docker-compose.yml` runs a dedicated `migrate` service before the API starts:
the service applies any pending EF Core migrations and exits with code 0, then the
`api` service starts via `depends_on: condition: service_completed_successfully`.
There is nothing extra to run — just `docker compose up -d --build`.

This is the recommended approach: migrations are reviewed and applied as a discrete
step, separate from the running API, so a migration failure is isolated and does not
cause the API to crash on startup.

For other deployment styles:

- **Dedicated pre-deploy step (recommended):** Run the image with `--migrate` before
  rolling out the new API version (e.g. as a Kubernetes init container, a CI deploy
  step, or `docker run <image> --migrate`). The process applies migrations and exits
  with code 0.
- **Automatic on startup:** Set `APPLY_MIGRATIONS=true` in the container environment.
  Convenient but risky with multiple replicas or CI/CD pipelines where migrations
  should be gated; only suitable for single-instance deployments where you accept
  the trade-off.
- **Manual (SDK required):** Run `dotnet ef database update --project src/Soulsjwa.Api`
  from a machine with the .NET SDK before deploying.
- **SQL script (no SDK at deploy time):** Generate an idempotent SQL script during CI
  and apply it directly against the database. See the
  [Database migrations](../README.md#database-migrations) section of the README.

### Health Checks

| Route | Purpose | Checks |
|---|---|---|
| `GET /health/live` | Liveness | None — the process is up |
| `GET /health/ready` | Readiness | PostgreSQL connectivity (checks tagged `ready`) |
| `GET /health` | Aggregate | Everything |

The image's own `HEALTHCHECK` hits `/health/live` with `wget` (the `aspnet`
base image has no `curl`). Wire orchestrator liveness to `/health/live` and
readiness to `/health/ready`, never liveness to `/health`: a database blip
would then restart a perfectly healthy process. A `503` from `/health/ready`
means the API cannot reach Postgres — check the `ConnectionStrings__DefaultConnection`
value and the database container's health first.

### DataProtection key ring

The signed OAuth-state cookie is protected with ASP.NET Core DataProtection.
The default key ring is a file under the container's home directory, which is
fine for one instance. With more than one instance every node must share the
ring (a mounted volume, Redis, or blob storage via
`AddDataProtection().PersistKeysTo…`), or a login that started on one node
fails its state check on another. Not configured today; see Scaling.

### Operational notes

- **Rotating `Jwt__Secret`** invalidates every access token at once; users are
  signed in again transparently by their refresh cookie on the next request,
  which is the intended way to force re-authentication.
- **`--migrate` needs only the connection string.** The serving process
  additionally requires `Jwt__Secret` and the Twitch credentials at startup.
- **Audit retention** (`Retention__AuditRetentionDays`) deletes old audit rows;
  nothing user-facing is reconstructed from them any more (event status lives
  on the event row), so the setting is purely a storage policy.
- **The connector talks to production routes only.** Every route it uses is
  checked to exist on a Production host by `ConnectorRouteAvailabilityTests`.

## Production Security Checklist

- [ ] Use a strong, unique `Jwt__Secret` (32+ random characters)
- [ ] Set `ASPNETCORE_ENVIRONMENT=Production` (disables Swagger and dev features)
- [ ] Use HTTPS in production (HSTS is enabled automatically)
- [ ] Configure `Frontend__Url` to your actual frontend domain for CORS
- [ ] Set `Twitch__RedirectUri` to your production callback URL
- [ ] If the Twitch extension is enabled: the API must be reachable over HTTPS with a CA-issued certificate (Twitch refuses anything else), `TwitchExtension__LocalTestOrigin` must be unset, and `Frontend__Url` (or `TwitchExtension__ApiUrl`) must be this deployment's public origin, since it is written into the extension zip downloaded from the admin tab
- [ ] Set `AllowedHosts` (`ALLOWED_HOSTS` for the stock Compose file) to your real hostname(s) — never `*` in production
- [ ] Use a managed PostgreSQL instance with SSL
- [ ] Restrict database access to the API container only
- [ ] Set `Admin__BootstrapTwitchLogin` before first sign-in if you need first-admin bootstrap, then remove it after an admin exists
- [ ] Rotate API keys and refresh tokens periodically
- [ ] Monitor the `/health` endpoint

## Scaling Considerations

- The API is stateless (JWT-based auth) — it can be horizontally scaled behind a load balancer
- Refresh token cookies use `SameSite=Strict` and a specific path (`/api/v1/auth`) — ensure your reverse proxy preserves these
- Database connection pooling is handled by EF Core / Npgsql
- Static frontend assets are served directly by the .NET API from `wwwroot/` — consider a CDN for production
- **Output caching is single-instance.** The scoreboard/overlay output cache uses the default in-memory store, and `EvictByTagAsync` only clears the node it runs on. In a multi-instance deployment, a write on one node does not evict the cached scoreboard on the others, so they can serve a stale scoreboard for up to the cache policy's expiry (1 hour for `Scoreboard`, 5 seconds for `OverlayScoreboard`). A shared output-cache store (e.g. Redis) is a prerequisite for running more than one instance; not configured today (see [DataProtection key ring](#dataprotection-key-ring) for the same class of constraint).
- The scoreboard cache tag is scoped per event (`CacheTags.Scoreboard(eventId)`), so a write in one event does not evict another event's cached scoreboard even on a single instance.
