/**
 * Stub `window.matchMedia` so MUI's `useMediaQuery(theme.breakpoints.up('md'))`
 * resolves to `wide`. Every query answers the same, which is all the
 * scoreboard's single md breakpoint needs.
 */
export const stubViewportWidth = (wide: boolean) => {
  window.matchMedia = (query: string) =>
    ({
      matches: wide,
      media: query,
      onchange: null,
      addListener: () => {},
      removeListener: () => {},
      addEventListener: () => {},
      removeEventListener: () => {},
      dispatchEvent: () => false,
    }) as MediaQueryList
}
