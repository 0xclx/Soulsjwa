# Soulsjwa.Web

The React 19 + TypeScript + Vite + MUI single-page app. Everything about it —
routes, feature modules, state, theming, build and tooling — is documented in
[`docs/frontend.md`](../../docs/frontend.md); conventions for changing it are
in [`docs/agent-conventions/frontend-components.md`](../../docs/agent-conventions/frontend-components.md).

```bash
npm ci
npm run dev      # Vite dev server on :5173, /api proxied to :5000
npm run lint     # eslint + prettier --check
npm test         # vitest, one shot (npm run test:watch for the loop)
npm run build    # tsc -b (TypeScript 7) + vite build → ../Soulsjwa.Api/wwwroot
```
