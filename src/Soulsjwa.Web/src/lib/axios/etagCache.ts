/**
 * Per-URL cache of the last `ETag` a `GET` saw, so a later `PUT` to the same
 * URL can carry it back as `If-Match`. Module-level and in-memory:
 * it only needs to survive for the lifetime of the page, and every consumer
 * imports the same module instance.
 */
const cache = new Map<string, string>()

export const etagCache = {
  get: (url: string): string | undefined => cache.get(url),
  set: (url: string, etag: string): void => {
    cache.set(url, etag)
  },
}
