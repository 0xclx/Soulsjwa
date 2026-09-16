#!/usr/bin/env bash
# Regenerates THIRD-PARTY-NOTICES.md at the repo root from two sources:
#   - npm dependencies of src/Soulsjwa.Web, via generate-license-file
#   - NuGet dependencies of every .NET project, via the dotnet-project-licenses
#     local tool (see .config/dotnet-tools.json)
# The npm generator is deterministic. dotnet-project-licenses is not quite:
# when one package appears at two versions it lists them in whatever order it
# walked the project assets files, which differs between machines, so the
# NuGet table rows are sorted here before merging. The result is byte-identical
# for the same inputs, and CI fails the build on any diff (see
# .github/workflows/ci.yml).
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

tmp_dir="$(mktemp -d)"
trap 'rm -rf "$tmp_dir"' EXIT

echo "==> Restoring local dotnet tools"
dotnet tool restore

echo "==> Generating NuGet license report"
dotnet tool run --allow-roll-forward dotnet-project-licenses -- \
  --input . \
  --include-transitive \
  --use-project-assets-json \
  --unique \
  --md \
  --output-directory "$tmp_dir" \
  --log-level Warning

# Keep the two-line markdown header, sort the rows beneath it.
{
  head -n 2 "$tmp_dir/licenses.md"
  tail -n +3 "$tmp_dir/licenses.md" | LC_ALL=C sort -f
} > "$tmp_dir/licenses.sorted.md"
mv "$tmp_dir/licenses.sorted.md" "$tmp_dir/licenses.md"

echo "==> Generating npm license report (src/Soulsjwa.Web)"
(
  cd src/Soulsjwa.Web
  npx generate-license-file \
    --input package.json \
    --output "$tmp_dir/npm-licenses.txt" \
    --overwrite --ci
)

echo "==> Merging into THIRD-PARTY-NOTICES.md"
{
  echo "# Third-Party Notices"
  echo
  echo "This file is generated — do not edit by hand. Regenerate with:"
  echo
  echo '```bash'
  echo "tools/generate_third_party_notices.sh"
  echo '```'
  echo
  echo "CI regenerates this file and fails the build if it differs from what's"
  echo "committed (a dependency changed without regenerating notices in the"
  echo "same PR)."
  echo
  echo "## NuGet dependencies (every .NET project, direct and transitive)"
  echo
  cat "$tmp_dir/licenses.md"
  echo
  echo "## npm dependencies (src/Soulsjwa.Web, direct and transitive)"
  echo
  cat "$tmp_dir/npm-licenses.txt"
} > THIRD-PARTY-NOTICES.md

echo "==> Done: THIRD-PARTY-NOTICES.md"
