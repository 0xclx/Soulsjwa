import '@testing-library/jest-dom/vitest'
import { afterEach } from 'vitest'
import { cleanup } from '@testing-library/react'

// jsdom doesn't implement matchMedia. MUI's useMediaQuery falls back to its
// `defaultMatches` option when it's missing, but components that don't pass
// one (a bare `useMediaQuery(query)`) need a stub to avoid throwing. Default
// to "no match" (narrow viewport); tests that care about the wide/desktop
// branch override `window.matchMedia` for that one query.
if (!window.matchMedia) {
  window.matchMedia = (query: string): MediaQueryList =>
    ({
      matches: false,
      media: query,
      onchange: null,
      addListener: () => {},
      removeListener: () => {},
      addEventListener: () => {},
      removeEventListener: () => {},
      dispatchEvent: () => false,
    }) as MediaQueryList
}

// Node 25+ defines `localStorage` / `sessionStorage` globals of its own that
// read as undefined (or throw, on Node 22 with --experimental-webstorage)
// unless node is started with --localstorage-file. vitest's jsdom environment
// keeps a global that already exists instead of copying the window's, so on
// those Node versions `window.localStorage` is unusable in every test. Put
// jsdom's implementation back; older Node versions never hit this branch.
const dom = (globalThis as { jsdom?: { window: Window } }).jsdom
const storageIsUnusable = (key: 'localStorage' | 'sessionStorage'): boolean => {
  try {
    return window[key] === undefined
  } catch {
    return true
  }
}
if (dom) {
  for (const key of ['localStorage', 'sessionStorage'] as const) {
    if (storageIsUnusable(key)) {
      Object.defineProperty(globalThis, key, {
        value: dom.window[key],
        configurable: true,
        writable: true,
      })
    }
  }
}

afterEach(() => {
  cleanup()
})
