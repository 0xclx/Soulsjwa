# Frontend components

## Layout
- Pages live in `src/Soulsjwa.Web/src/pages/`.
- Feature modules live in `src/features/<area>/` with subfolders `api/`,
  `hooks/`, `components/`.
- Shared types live in `src/types/`.

## Hooks
- **One TanStack Query hook per file.** File name matches the export
  (e.g. `useAllowlist.ts` exports `useAllowlist`). Located under
  `src/features/<area>/hooks/`.
- Group query keys per feature in a `*Keys` object — never inline string arrays.

## MUI v9 (the most common foot-guns)
- System props (`alignItems`, `justifyContent`, `gap`, `textAlign`, `fontStyle`,
  `mt`, `py`, etc.) **must** go through `sx`, not as direct props on `Stack` /
  `Typography`. They were removed at the prop level in v9.
- TextField: use `slotProps={{ htmlInput: {...} }}` — `inputProps` is gone.
- Select inside a TextField: use `slotProps={{ select: { native: true } }}` for
  native `<option>` rendering.

## Strings, enums, labels
- See [`no-magic-strings.md`](./no-magic-strings.md). Iterate `*_MODES` tuples;
  read display strings from `*_LABELS` maps.

## State you put in URLs
- Use `react-router-dom` `useParams` / `useSearchParams`. Don't recreate URL
  state in React state.

## Tests
- Vitest, config in `vitest.config.ts` (intentionally separate from
  `vite.config.ts` to avoid Vite version mismatch).
- Setup file: `src/test/setup.ts` (jest-dom matchers + auto cleanup).
- Run `npm test` for a one-shot run, `npm run test:watch` for the watch loop, `npm run test:coverage` for CI-style.

## Before committing
```bash
cd src/Soulsjwa.Web
npm run lint        # eslint + prettier --check
npm run build       # tsc -b && vite build
npm test            # one-shot test run
```
