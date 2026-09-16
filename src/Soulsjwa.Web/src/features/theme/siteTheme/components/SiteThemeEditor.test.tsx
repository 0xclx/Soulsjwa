import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi, beforeEach } from 'vitest'
import { SiteThemeEditor } from './SiteThemeEditor'
import type { SiteTheme } from '../../../../types'

const baseTheme: SiteTheme = {
  backgroundAssetId: null,
  backgroundUrl: null,
  backgroundTreatment: 'None',
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

const mocks = vi.hoisted(() => ({
  data: undefined as unknown,
  isLoading: false,
  isError: false,
  mutate: vi.fn(),
  isPending: false,
}))

vi.mock('../hooks/useSiteTheme', () => ({
  useSiteTheme: () => ({ data: mocks.data, isLoading: mocks.isLoading, isError: mocks.isError }),
}))

vi.mock('../hooks/useUpdateSiteTheme', () => ({
  useUpdateSiteTheme: () => ({ mutate: mocks.mutate, isPending: mocks.isPending }),
}))

// ImageUploadField pulls in useUploadMedia; the editor's own tests aren't
// about uploading (ImageUploadField.test.tsx already covers that), so stub
// it out to keep this test focused on the palette/font/treatment form.
vi.mock('../../../media/hooks/useUploadMedia', () => ({
  useUploadMedia: () => ({ mutate: vi.fn(), reset: vi.fn(), isPending: false, isError: false }),
}))

describe('SiteThemeEditor', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mocks.data = { ...baseTheme }
    mocks.isLoading = false
    mocks.isError = false
    mocks.isPending = false
  })

  it('loads the existing palette into the form', () => {
    render(<SiteThemeEditor />)
    expect(screen.getByLabelText('Light Default')).toHaveValue(baseTheme.lightDefault)
    expect(screen.getByLabelText('Light Accent')).toHaveValue(baseTheme.lightAccent)
    expect(screen.getByLabelText('Dark Default')).toHaveValue(baseTheme.darkDefault)
  })

  it('shows a live contrast readout for non-Default slots', () => {
    render(<SiteThemeEditor />)
    // 6 rows per mode x 2 modes; only the 5 non-Default rows per mode show a ratio.
    expect(screen.getAllByText(/:1/)).toHaveLength(10)
  })

  it('saves the edited palette', async () => {
    render(<SiteThemeEditor />)
    const accentField = screen.getByLabelText('Light Accent')
    await userEvent.clear(accentField)
    await userEvent.type(accentField, '#123456')
    await userEvent.click(screen.getByRole('button', { name: /^save$/i }))

    await waitFor(() =>
      expect(mocks.mutate).toHaveBeenCalledWith(
        expect.objectContaining({ lightAccent: '#123456' }),
        expect.anything(),
      ),
    )
  })

  it('surfaces the server’s failing-pair message on save error', async () => {
    mocks.mutate.mockImplementation((_req, opts) => {
      opts?.onError?.({
        response: { data: { errors: { palette: ['Light.Accent and Light.Default fail 4.5:1.'] } } },
      })
    })
    render(<SiteThemeEditor />)
    await userEvent.click(screen.getByRole('button', { name: /^save$/i }))

    expect(await screen.findByText(/fail 4\.5:1/)).toBeInTheDocument()
  })

  it('shows a loading state while fetching', () => {
    mocks.isLoading = true
    mocks.data = undefined
    render(<SiteThemeEditor />)
    expect(screen.getByText(/loading site theme/i)).toBeInTheDocument()
  })

  it('shows an error state when the fetch fails', () => {
    mocks.isError = true
    mocks.data = undefined
    render(<SiteThemeEditor />)
    expect(screen.getByText(/failed to load the site theme/i)).toBeInTheDocument()
  })
})
