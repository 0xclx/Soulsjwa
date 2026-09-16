# Build React
FROM node:26-alpine AS frontend-build
WORKDIR /app/src/Soulsjwa.Web
COPY src/Soulsjwa.Web/package*.json src/Soulsjwa.Web/.npmrc ./
RUN npm ci
COPY src/Soulsjwa.Web/ ./
# The SPA into wwwroot, and the Twitch extension bundle (docs/twitch-extension.md)
# into dist-twitch, shipped next to the API so an admin can download the zip
# with this deployment's origin written in.
RUN npm run build && npm run build:twitch

# Build Connector — Windows WPF app published self-contained, single-file for
# win-x64 from the Linux SDK image (EnableWindowsTargeting makes
# net10.0-windows projects restore/publish on non-Windows hosts). The
# resulting single .exe is later copied into the API's wwwroot/downloads so
# the running container can serve it directly (no zip/unzip round trip).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS connector-build
WORKDIR /app
COPY *.slnx ./
COPY src/Soulsjwa.Connector/*.csproj src/Soulsjwa.Connector/
COPY src/Soulsjwa.Shared/*.csproj src/Soulsjwa.Shared/
# The connector csproj derives <Version> from this file at evaluation time,
# so it must be present for restore, ahead of the rest of the sources.
COPY src/Soulsjwa.Shared/ConnectorConstants.cs src/Soulsjwa.Shared/
RUN dotnet restore src/Soulsjwa.Connector/Soulsjwa.Connector.csproj \
      -p:EnableWindowsTargeting=true -r win-x64
COPY src/Soulsjwa.Connector/ src/Soulsjwa.Connector/
COPY src/Soulsjwa.Shared/ src/Soulsjwa.Shared/
RUN dotnet publish src/Soulsjwa.Connector/Soulsjwa.Connector.csproj \
      -c Release -r win-x64 --self-contained true \
      -p:EnableWindowsTargeting=true \
      -p:PublishSingleFile=true \
      -p:IncludeNativeLibrariesForSelfExtract=true \
      -p:SatelliteResourceLanguages=en \
      -p:DebugType=none \
      -o /publish-connector
# Bake the connector's <Version> (read by the csproj from
# Soulsjwa.Shared/ConnectorConstants.cs) into the exe filename so users can
# tell which build they have. We additionally create a stable "latest" copy
# alongside the versioned file, letting the frontend link to a single URL
# that always resolves to the connector version shipped with this image.
# This is a real copy rather than a symlink: ASP.NET Core's static-file
# middleware computes Content-Length from a symlink's own (tiny) inode size
# rather than the size of the file it points to, which serves a
# truncated/corrupted download.
RUN VERSION="$(dotnet msbuild src/Soulsjwa.Connector/Soulsjwa.Connector.csproj \
        -getProperty:Version -nologo | tr -d '[:space:]')" \
 && { [ -n "$VERSION" ] || { echo "Failed to extract connector <Version> from csproj" >&2; exit 1; }; } \
 && mkdir -p /connector-dist \
 && cp /publish-connector/Soulsjwa.Connector.exe \
       "/connector-dist/Soulsjwa.Connector-${VERSION}-win-x64.exe" \
 && cp "/connector-dist/Soulsjwa.Connector-${VERSION}-win-x64.exe" \
       "/connector-dist/Soulsjwa.Connector-latest-win-x64.exe"

# Build .NET
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api-build
WORKDIR /app
COPY *.slnx ./
COPY src/Soulsjwa.Api/*.csproj src/Soulsjwa.Api/
COPY src/Soulsjwa.Shared/*.csproj src/Soulsjwa.Shared/
RUN dotnet restore src/Soulsjwa.Api/Soulsjwa.Api.csproj
COPY src/Soulsjwa.Api/ src/Soulsjwa.Api/
COPY src/Soulsjwa.Shared/ src/Soulsjwa.Shared/
COPY --from=frontend-build /app/src/Soulsjwa.Api/wwwroot src/Soulsjwa.Api/wwwroot
# The baseline legal templates are linked from /admin/legal as
# /legal-templates/<file>, served by the static-file middleware.
COPY templates/legal/ src/Soulsjwa.Api/wwwroot/legal-templates/
RUN dotnet publish src/Soulsjwa.Api/Soulsjwa.Api.csproj -c Release -o /publish
# The Twitch extension front end, served to admins as a zip by
# GET /api/v1/admin/twitch-extension/bundle (TwitchExtension:BundlePath
# defaults to this folder under the content root). Not under wwwroot: it is
# never served as static files.
COPY --from=frontend-build /app/src/Soulsjwa.Web/dist-twitch /publish/twitch-extension
# Bundle the self-contained, single-file Windows connector into the API's
# static-file root so it is served by ASP.NET's UseStaticFiles middleware at
# /downloads/Soulsjwa.Connector-<Version>-win-x64.exe and the stable
# /downloads/Soulsjwa.Connector-latest-win-x64.exe copy (linked from the
# frontend).
COPY --from=connector-build /connector-dist/ /publish/wwwroot/downloads/

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
# COPY with --chown so the published assets are readable/executable by the
# non-root `app` user (UID 1654) we drop to below.
COPY --from=api-build --chown=$APP_UID:$APP_UID /publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

# Media uploads are stored content-addressed under Media__RootPath
# (docker-compose.yml mounts a named volume here). Create it with the
# non-root user's ownership now: Docker copies an image mount point's
# existing ownership into a named volume the first time it's used, so this
# is what makes the volume writable by $APP_UID once it's mounted below.
ENV Media__RootPath=/data/media
RUN mkdir -p /data/media && chown $APP_UID:$APP_UID /data/media

# Drop to the non-root `app` user that ships with the dotnet/aspnet image
# (UID 1654) — defence in depth in case of a code-execution vulnerability.
USER $APP_UID

# Liveness probe: hit the dedicated /health/live endpoint (no-op check that just
# confirms the process is up — does not exercise the DB so we don't flap on
# transient downstream blips). wget is preinstalled in the dotnet/aspnet image.
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
  CMD wget -qO- --tries=1 http://127.0.0.1:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "Soulsjwa.Api.dll"]
