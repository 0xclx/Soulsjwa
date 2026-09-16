import { defineConfig, type Plugin } from 'vite'
import react from '@vitejs/plugin-react'
import { createReadStream, existsSync } from 'node:fs'
import { basename, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

/**
 * Serves `/legal-templates/<file>` from the repo's `templates/legal/` during
 * development; the Dockerfile copies the same folder into `wwwroot` for
 * production. See `features/legal/legalTemplates.ts`.
 */
const legalTemplatesDevPlugin = (): Plugin => ({
  name: 'soulsjwa-legal-templates',
  configureServer(server) {
    const dir = resolve(fileURLToPath(new URL('.', import.meta.url)), '../../templates/legal')
    server.middlewares.use('/legal-templates', (req, res, next) => {
      const file = basename(req.url?.split('?')[0] ?? '')
      const path = resolve(dir, file)
      if (!file || !path.startsWith(dir) || !existsSync(path)) return next()
      res.setHeader('Content-Type', 'text/markdown; charset=utf-8')
      createReadStream(path).pipe(res)
    })
  },
})

export default defineConfig({
  plugins: [react(), legalTemplatesDevPlugin()],
  build: {
    outDir: '../Soulsjwa.Api/wwwroot',
    emptyOutDir: true,
    // Warn when any single chunk exceeds 750 kB after minification.
    // The Blockly library alone is ~700 kB; this threshold catches
    // accidental new-dependency bloat without blocking the build.
    chunkSizeWarningLimit: 750,
    rollupOptions: {
      output: {
        // Split vendor code into separate, cache-friendly chunks so the
        // main application bundle stays small and loads are parallelised.
        manualChunks(id: string) {
          if (id.includes('node_modules/react-dom') || id.includes('node_modules/react/'))
            return 'react'
          if (id.includes('node_modules/@mui/') || id.includes('node_modules/@emotion/'))
            return 'mui'
          if (id.includes('node_modules/@tanstack/react-query')) return 'query'
          if (id.includes('node_modules/react-router')) return 'router'
          if (id.includes('node_modules/blockly')) return 'blockly'
        },
      },
    },
  },
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: 'http://localhost:5000',
        changeOrigin: true,
      },
    },
  },
})
