import { describe, it, expect, beforeEach, vi } from 'vitest'
import { render, screen, act } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { ThemeModeProvider } from './ThemeModeProvider'
import { useThemeMode } from './useThemeMode'
import { createAppTheme } from './theme'
import type { SiteTheme } from '../types'

const mocks = vi.hoisted(() => ({
  siteTheme: undefined as SiteTheme | undefined,
}))

vi.mock('../features/theme/siteTheme/hooks/useSiteTheme', () => ({
  useSiteTheme: () => ({ data: mocks.siteTheme }),
}))

vi.mock('./theme', async (importOriginal) => {
  const actual = await importOriginal<typeof import('./theme')>()
  return { ...actual, createAppTheme: vi.fn(actual.createAppTheme) }
})

const SITE_THEME_FIXTURE: SiteTheme = {
  backgroundAssetId: 'asset-1',
  backgroundUrl: 'https://cdn.example.test/background.png',
  backgroundTreatment: 'Cover',
  font: 'SystemSansSerif',
  lightDefault: '#f5f5f5',
  lightAccent: '#6d28d9',
  lightDanger: '#b91c1c',
  lightInfo: '#0369a1',
  lightSuccess: '#15803d',
  lightHighlight: '#b45309',
  darkDefault: '#1e1e1e',
  darkAccent: '#c4b5fd',
  darkDanger: '#f87171',
  darkInfo: '#38bdf8',
  darkSuccess: '#4ade80',
  darkHighlight: '#fbbf24',
  updatedAt: '2026-01-01T00:00:00Z',
}

const Probe = () => {
  const { mode, resolvedMode, cycleMode, setMode } = useThemeMode()
  return (
    <div>
      <span data-testid="mode">{mode}</span>
      <span data-testid="resolved">{resolvedMode}</span>
      <button onClick={cycleMode}>cycle</button>
      <button onClick={() => setMode('light')}>set-light</button>
      <button onClick={() => setMode('dark')}>set-dark</button>
    </div>
  )
}

// useSiteTheme() fetches from the API; a fresh no-retry client per test keeps
// the (unmocked, always-failing in this test environment) request from
// retrying, and its failure is harmless — ThemeModeProvider falls back to
// the built-in default palette when no theme has loaded.
const renderWithProviders = (children: React.ReactNode) => {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <ThemeModeProvider>{children}</ThemeModeProvider>
    </QueryClientProvider>,
  )
}

describe('ThemeModeProvider', () => {
  beforeEach(() => {
    window.localStorage.clear()
    mocks.siteTheme = undefined
    document.documentElement.style.backgroundImage = ''
    document.documentElement.style.backgroundSize = ''
    document.documentElement.style.backgroundRepeat = ''
    document.documentElement.style.backgroundPosition = ''
    vi.mocked(createAppTheme).mockClear()
  })

  it('defaults to dark mode when no preference is stored', () => {
    renderWithProviders(<Probe />)
    expect(screen.getByTestId('mode')).toHaveTextContent('dark')
  })

  it('falls back to dark when device preference cannot be determined', () => {
    renderWithProviders(<Probe />)
    expect(screen.getByTestId('resolved')).toHaveTextContent('dark')
  })

  it('cycles light → dark → system → light', async () => {
    const user = userEvent.setup()
    renderWithProviders(<Probe />)

    await user.click(screen.getByText('set-light'))
    expect(screen.getByTestId('mode')).toHaveTextContent('light')
    expect(screen.getByTestId('resolved')).toHaveTextContent('light')

    await user.click(screen.getByText('cycle'))
    expect(screen.getByTestId('mode')).toHaveTextContent('dark')

    await user.click(screen.getByText('cycle'))
    expect(screen.getByTestId('mode')).toHaveTextContent('system')

    await user.click(screen.getByText('cycle'))
    expect(screen.getByTestId('mode')).toHaveTextContent('light')
  })

  it('persists the selected mode to localStorage', async () => {
    const user = userEvent.setup()
    renderWithProviders(<Probe />)

    await user.click(screen.getByText('set-light'))
    expect(window.localStorage.getItem('soulsjwa.themeMode')).toBe('light')

    await user.click(screen.getByText('set-dark'))
    expect(window.localStorage.getItem('soulsjwa.themeMode')).toBe('dark')
  })

  it('restores mode from localStorage on mount', () => {
    window.localStorage.setItem('soulsjwa.themeMode', 'light')
    renderWithProviders(<Probe />)
    expect(screen.getByTestId('mode')).toHaveTextContent('light')
    expect(screen.getByTestId('resolved')).toHaveTextContent('light')
  })

  it('ignores invalid values in localStorage', () => {
    window.localStorage.setItem('soulsjwa.themeMode', 'neon-pink')
    renderWithProviders(<Probe />)
    expect(screen.getByTestId('mode')).toHaveTextContent('dark')
  })

  it('applies the site background image and treatment to the document root', () => {
    mocks.siteTheme = SITE_THEME_FIXTURE
    renderWithProviders(<Probe />)

    expect(document.documentElement.style.backgroundImage).toBe(
      'url("https://cdn.example.test/background.png")',
    )
    expect(document.documentElement.style.backgroundSize).toBe('cover')
    expect(document.documentElement.style.backgroundRepeat).toBe('no-repeat')
    expect(document.documentElement.style.backgroundPosition).toBe('center center')
  })

  it('applies a distinct style per background treatment', () => {
    mocks.siteTheme = { ...SITE_THEME_FIXTURE, backgroundTreatment: 'Tile' }
    renderWithProviders(<Probe />)

    expect(document.documentElement.style.backgroundSize).toBe('auto')
    expect(document.documentElement.style.backgroundRepeat).toBe('repeat')
  })

  it('clears any previously applied background when no site background is set', () => {
    document.documentElement.style.backgroundImage = 'url("https://cdn.example.test/old.png")'
    mocks.siteTheme = undefined
    renderWithProviders(<Probe />)

    expect(document.documentElement.style.backgroundImage).toBe('')
  })

  it('falls back to the default theme instead of crashing when the site theme has a malformed colour', () => {
    mocks.siteTheme = { ...SITE_THEME_FIXTURE, darkAccent: 'not-a-color' }
    const spy = vi.spyOn(console, 'error').mockImplementation(() => {})

    expect(() => renderWithProviders(<Probe />)).not.toThrow()
    expect(screen.getByTestId('resolved')).toHaveTextContent('dark')

    spy.mockRestore()
  })

  it('degrades to the no-custom-palette theme instead of crashing when createAppTheme itself throws', () => {
    mocks.siteTheme = SITE_THEME_FIXTURE
    vi.mocked(createAppTheme).mockImplementationOnce(() => {
      throw new Error('MUI could not parse this palette')
    })

    expect(() => renderWithProviders(<Probe />)).not.toThrow()
    expect(screen.getByTestId('resolved')).toHaveTextContent('dark')
    // The retry (fallback) call passes no custom palette.
    expect(createAppTheme).toHaveBeenLastCalledWith('dark')
  })

  it('throws when useThemeMode is used outside the provider', () => {
    const Broken = () => {
      useThemeMode()
      return null
    }
    // Suppress React's error log to keep test output clean.
    const spy = vi.spyOn(console, 'error').mockImplementation(() => {})
    expect(() =>
      act(() => {
        render(<Broken />)
      }),
    ).toThrow(/ThemeModeProvider/)
    spy.mockRestore()
  })
})
