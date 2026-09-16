# Getting Started

This guide walks you through setting up Soulsjwa for local development.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Node.js 26+](https://nodejs.org/) (for the React frontend)
- [Docker & Docker Compose](https://docs.docker.com/get-docker/) (for PostgreSQL and/or full-stack deployment)
- A [Twitch Developer Application](https://dev.twitch.tv/console/apps) (for OAuth2 authentication)

## Quick Start with Docker

The fastest way to run the full application:

1. **Copy the environment file:**

   ```bash
   cp .env.example .env
   ```

2. **Fill in the required values in `.env`:**

   ```env
   POSTGRES_PASSWORD=<your-password>
   JWT_SECRET=<at-least-32-characters>
   TWITCH_CLIENT_ID=<your-twitch-app-client-id>
   TWITCH_CLIENT_SECRET=<your-twitch-app-client-secret>
   ```

   The stock `docker-compose.yml` maps those values into the API container and
   hardcodes local Docker URLs for `Twitch__RedirectUri` and `Frontend__Url`. For
   direct API runs or production overrides, use the .NET double-underscore keys
   shown in [.env.example](../.env.example), including optional
   `Admin__BootstrapTwitchLogin` for first-admin bootstrap.

3. **Start the application:**

   ```bash
   docker compose up --build
   ```

4. **Access the app at** `http://localhost:8080`

The api container runs as a deployment would (Production environment). For the
Development behaviour inside the container — relaxed CSP for a Vite dev server,
the placeholder JWT secret accepted, Swagger — add the opt-in overlay:

   ```bash
   docker compose -f docker-compose.yml -f docker-compose.dev.yml up --build
   ```

The Docker setup automatically:
- Starts PostgreSQL 16 with a health check
- Builds the React frontend and .NET API into a single container
- Runs database migrations with the dedicated `migrate` service before the API starts

## Local Development Setup

For a development workflow with hot reloading on both frontend and backend:

### 1. Start PostgreSQL

```bash
docker compose up -d postgres
```

### 2. Configure the API

Create or update `src/Soulsjwa.Api/appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=soulsjwa;Username=postgres;Password=<your-password>"
  },
  "Jwt": {
    "Secret": "<at-least-32-characters>"
  },
  "Twitch": {
    "ClientId": "<your-twitch-client-id>",
    "ClientSecret": "<your-twitch-client-secret>",
    "RedirectUri": "http://localhost:5000/api/v1/auth/twitch/callback"
  },
  "Frontend": {
    "Url": "http://localhost:5173"
  },
  "Admin": {
    "BootstrapTwitchLogin": "<optional-first-admin-twitch-login>"
  }
}
```

### 3. Run the API

```bash
dotnet run --project src/Soulsjwa.Api
```

The API starts at `http://localhost:5000` with Swagger UI available at `http://localhost:5000/swagger`.

### 4. Run the Frontend

```bash
cd src/Soulsjwa.Web
npm ci
npm run dev
```

The frontend starts at `http://localhost:5173`. API requests are automatically proxied to `localhost:5000` via the Vite dev server.

### 5. (Optional) Run the Connector

The WPF Connector is a Windows-only desktop application:

```bash
dotnet run --project src/Soulsjwa.Connector
```

## Twitch Application Setup

1. Go to the [Twitch Developer Console](https://dev.twitch.tv/console/apps)
2. Click **Register Your Application**
3. Set the **OAuth Redirect URL** to your callback URL:
   - Local dev: `http://localhost:5000/api/v1/auth/twitch/callback`
   - Docker: `http://localhost:8080/api/v1/auth/twitch/callback`
4. Set the **Category** to any applicable option
5. Copy the **Client ID** and generate a **Client Secret**
6. Add these to your configuration (see above)

## Building

```bash
# Build the entire solution
dotnet build Soulsjwa.slnx

# Build frontend only
cd src/Soulsjwa.Web && npm run build

# Build API only
dotnet build src/Soulsjwa.Api
```

> **Note:** Do not run frontend and backend builds in parallel — the frontend outputs to `src/Soulsjwa.Api/wwwroot/` and concurrent access can cause failures.

## Running Tests

```bash
# Unit tests
dotnet test tests/Soulsjwa.UnitTests/

# API tests (requires Docker for Testcontainers)
dotnet test tests/Soulsjwa.ApiTests/

# Integration tests (requires Docker for Testcontainers)
dotnet test tests/Soulsjwa.IntegrationTests/

# Frontend tests and linting
cd src/Soulsjwa.Web && npm test && npm run lint
```

See [Testing](testing.md) for more details.

## Useful Endpoints

| Endpoint | Description |
|----------|-------------|
| `http://localhost:5000/swagger` | Swagger UI (development only) |
| `http://localhost:5000/health` | Health check |
| `http://localhost:5000/api/v1/games` | List seeded games |
| `http://localhost:5000/api/v1/events` | List events |
