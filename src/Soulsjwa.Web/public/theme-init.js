;(() => {
  const storageKey = 'soulsjwa.themeMode'
  let mode = 'dark'
  try {
    const stored = localStorage.getItem(storageKey)
    if (stored === 'light' || stored === 'dark') mode = stored
    if (stored === 'system') {
      mode = matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light'
    }
  } catch {
    // Storage can be unavailable in privacy-restricted browser contexts.
  }
  document.documentElement.dataset.colorScheme = mode
  document.documentElement.style.colorScheme = mode
  document.documentElement.style.backgroundColor = mode === 'dark' ? '#121212' : '#ffffff'
})()
