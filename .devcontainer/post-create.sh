#!/usr/bin/env bash
set -euo pipefail

# ── Security: verify we are NOT running as root ──────────────────────────────
if [ "$(id -u)" -eq 0 ]; then
  echo "ERROR: post-create.sh must not run as root. Check remoteUser in devcontainer.json." >&2
  exit 1
fi

echo "==> Restoring .NET dependencies..."
dotnet restore Soulsjwa.slnx

echo "==> Installing frontend dependencies..."
cd src/Soulsjwa.Web
# --ignore-scripts prevents arbitrary post-install scripts from npm packages
npm ci --ignore-scripts
cd ../..

echo "==> Applying EF Core migrations..."
dotnet ef database update --project src/Soulsjwa.Api

echo "==> Dev container ready!"
