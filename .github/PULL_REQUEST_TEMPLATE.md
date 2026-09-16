<!--
  Thanks for contributing to Soulsjwa! Please fill in the sections below.
  Delete sections that don't apply.
-->

## Summary

<!-- One or two sentences describing the change. -->

## Motivation

<!-- Why is this change needed? Link related issues with "Closes #123" or "Refs #123". -->

## Changes

<!-- Bullet list of the user-visible / structural changes in this PR. -->

-
-

## Type of change

- [ ] Bug fix (non-breaking change which fixes an issue)
- [ ] New feature (non-breaking change which adds functionality)
- [ ] Breaking change (fix or feature that would cause existing functionality to not work as expected)
- [ ] Documentation update
- [ ] Refactor / internal cleanup
- [ ] Chore / dependency update

## How has this been tested?

<!-- Describe the tests you ran. Include any relevant configuration / steps. -->

- [ ] `dotnet build Soulsjwa.slnx` is clean
- [ ] `dotnet test tests/Soulsjwa.UnitTests/` passes
- [ ] `dotnet test tests/Soulsjwa.ApiTests/` passes
- [ ] `dotnet format Soulsjwa.slnx --verify-no-changes` is clean
- [ ] `npm run lint` (in `src/Soulsjwa.Web/`) is clean
- [ ] `npm test` (in `src/Soulsjwa.Web/`) passes
- [ ] `npm run build` (in `src/Soulsjwa.Web/`) succeeds

## Documentation

<!--
  If your change touches any of the areas below, the corresponding doc in
  `docs/` MUST be updated as part of this PR. See `.github/copilot-instructions.md`.
-->

- [ ] `docs/database-design.md` — entities / migrations / seed data
- [ ] `docs/api-reference.md` — endpoints / request-response shapes
- [ ] `docs/frontend.md` — pages / routes / components / hooks
- [ ] `docs/connector/README.md` — connector services / view models
- [ ] `docs/system-overview.md` — auth / deployment / cross-cutting infra
- [ ] `docs/feature-matrix.md` — feature status (✅ / 🟡 / ❌)
- [ ] `docs/README.md` — project layout / tech stack / major areas
- [ ] N/A

## Screenshots (UI changes only)

<!-- Drag-and-drop screenshots here if your change affects the frontend. -->

## Checklist

- [ ] My code follows the conventions in `.github/copilot-instructions.md`
- [ ] I have updated docs where relevant (see above)
- [ ] I have added tests for new behaviour
- [ ] No secrets, credentials, or production hostnames are committed
