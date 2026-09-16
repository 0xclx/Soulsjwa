const APP_SHELL_CACHE = 'soulsjwa-shell-v2'
// Must match SCOREBOARD_CACHE_NAME in src/lib/serviceWorkerCacheNames.ts —
// this file can't import from src/, so the literal is duplicated; a test
// there asserts the two stay in sync.
const SCOREBOARD_CACHE = 'soulsjwa-scoreboards-v1'
const APP_SHELL_URLS = ['/', '/theme-init.js', '/manifest.webmanifest', '/favicon.svg']
const SCOREBOARD_PATTERN = /\/api\/v1\/events\/[^/]+\/scoreboard/

self.addEventListener('install', (event) => {
  event.waitUntil(caches.open(APP_SHELL_CACHE).then((cache) => cache.addAll(APP_SHELL_URLS)))
  self.skipWaiting()
})

self.addEventListener('activate', (event) => {
  const keep = new Set([APP_SHELL_CACHE, SCOREBOARD_CACHE])
  event.waitUntil(
    caches
      .keys()
      .then((keys) =>
        Promise.all(keys.filter((key) => !keep.has(key)).map((key) => caches.delete(key))),
      )
      .then(() => self.clients.claim()),
  )
})

self.addEventListener('fetch', (event) => {
  const request = event.request
  if (request.method !== 'GET') return

  const url = new URL(request.url)
  if (url.origin !== self.location.origin) return

  if (SCOREBOARD_PATTERN.test(url.pathname)) {
    event.respondWith(networkFirst(request, SCOREBOARD_CACHE))
    return
  }

  if (request.mode === 'navigate') {
    event.respondWith(fetch(request).catch(() => caches.match('/')))
    return
  }

  if (['style', 'script', 'image', 'font'].includes(request.destination)) {
    event.respondWith(staleWhileRevalidate(request, APP_SHELL_CACHE))
  }
})

const networkFirst = async (request, cacheName) => {
  const cache = await caches.open(cacheName)
  try {
    const response = await fetch(request)
    if (response.ok) await cache.put(request, response.clone())
    return response
  } catch {
    const cached = await cache.match(request)
    if (cached) return cached
    throw new Error('No cached response available.')
  }
}

const staleWhileRevalidate = async (request, cacheName) => {
  const cache = await caches.open(cacheName)
  const cached = await cache.match(request)
  const fresh = fetch(request).then((response) => {
    if (response.ok) cache.put(request, response.clone())
    return response
  })
  return cached ?? fresh
}
