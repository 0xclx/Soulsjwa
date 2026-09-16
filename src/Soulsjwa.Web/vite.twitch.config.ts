import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import basicSsl from '@vitejs/plugin-basic-ssl'
import { resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

/**
 * The Twitch-hosted extension bundle — a second build of this package, kept
 * apart from the SPA (see `docs/adr/0008-twitch-extension-bundle.md`):
 * Twitch serves the files from its own CDN out of a zip we upload, under a
 * CSP that allows no third-party script host, so the bundle must be
 * self-contained, use relative asset paths, and know the API's host at build
 * time. `src/twitch-extension/` is the source; `twitch-extension/` holds the
 * five entry pages Twitch's console points at (panel, video component,
 * mobile, config, live config).
 *
 * The bundle contains nothing deployment-specific: the API origin it calls
 * lives in `extension-config.js` (`twitch-extension/public/`, copied into the
 * build as is), which the API rewrites with the real origin when an admin
 * downloads the zip. So one build — the one the Docker image makes — serves
 * any host. `npm run dev:twitch` serves https://localhost:8080/ with a
 * self-signed certificate, which is what Twitch's Local Test mode loads, and
 * proxies `/api` to the local backend (`docs/twitch-extension.md`, "Testing
 * on localhost"). `TWITCH_DEV_PORT` and `TWITCH_DEV_API_URL` override the
 * port and the proxy target, e.g. when the backend runs from Docker Compose
 * and already holds port 8080.
 */
const here = fileURLToPath(new URL('.', import.meta.url))
const entries = ['panel', 'video_component', 'mobile', 'config', 'live_config'] as const

const DEFAULT_DEV_PORT = 8080
const DEFAULT_DEV_API_URL = 'http://localhost:5000'
const devPort = Number(process.env.TWITCH_DEV_PORT ?? DEFAULT_DEV_PORT)
const devApiUrl = process.env.TWITCH_DEV_API_URL ?? DEFAULT_DEV_API_URL

export default defineConfig(({ command }) => {
  return {
    root: resolve(here, 'twitch-extension'),
    // Twitch serves the zip from a hashed path on its CDN, so every asset
    // reference has to be relative to the page.
    base: './',
    publicDir: resolve(here, 'twitch-extension', 'public'),
    plugins: [react(), ...(command === 'serve' ? [basicSsl()] : [])],
    build: {
      outDir: resolve(here, 'dist-twitch'),
      emptyOutDir: true,
      rollupOptions: {
        input: Object.fromEntries(
          entries.map((e) => [e, resolve(here, 'twitch-extension', `${e}.html`)]),
        ),
      },
    },
    server: {
      port: devPort,
      strictPort: true,
      proxy: {
        '/api': {
          target: devApiUrl,
          changeOrigin: true,
        },
      },
    },
  }
})
