/**
 * Cache Storage bucket names used by the service worker (`public/sw.js`).
 * `sw.js` is a plain public asset and cannot import from `src/`, so the
 * literal is duplicated there — `serviceWorkerCacheNames.test.ts` reads
 * `sw.js`'s source text and asserts the two never drift apart.
 */
export const SCOREBOARD_CACHE_NAME = 'soulsjwa-scoreboards-v1'
