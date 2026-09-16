import { describe, it, expect } from 'vitest'
// Vite's `?raw` suffix imports a module's source as a plain string, so this
// check reads real source text without needing Node's `fs` (which the app
// tsconfig deliberately excludes from its `types`).
import paletteMappingSource from './paletteMapping.ts?raw'
import themeSource from '../../../theme/theme.ts?raw'
import themeModeProviderSource from '../../../theme/ThemeModeProvider.tsx?raw'

const FILES: ReadonlyArray<[name: string, source: string]> = [
  ['paletteMapping.ts', paletteMappingSource],
  ['theme.ts', themeSource],
  ['ThemeModeProvider.tsx', themeModeProviderSource],
]

describe('site theme never reaches raw-markup injection', () => {
  // The site theme is delivered as MUI palette VALUES, never as
  // a CSS string injected into the page — no raw-markup injection API, no
  // injected <style> built from theme data. Every file that touches site
  // theme data must never regress into that pattern.
  it.each(FILES)(
    '%s contains no raw-markup injection and no <style> string building',
    (_name, source) => {
      expect(source).not.toMatch(/dangerouslySetInnerHTML/)
      expect(source).not.toMatch(/<style/i)
    },
  )
})
