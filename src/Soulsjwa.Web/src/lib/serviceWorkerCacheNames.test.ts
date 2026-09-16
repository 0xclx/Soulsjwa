import { describe, it, expect } from 'vitest'
// Vite's `?raw` suffix imports a module's source as a plain string — the
// same technique `features/theme/siteTheme/noRawHtml.test.ts` uses to read
// real source text without `fs`.
import swSource from '../../public/sw.js?raw'
import { SCOREBOARD_CACHE_NAME } from './serviceWorkerCacheNames'

describe('SCOREBOARD_CACHE_NAME', () => {
  it('matches the literal cache name duplicated in public/sw.js', () => {
    expect(swSource).toContain(`const SCOREBOARD_CACHE = '${SCOREBOARD_CACHE_NAME}'`)
  })
})
