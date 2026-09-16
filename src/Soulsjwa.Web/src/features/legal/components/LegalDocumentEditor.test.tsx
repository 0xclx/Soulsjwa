import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi, beforeEach } from 'vitest'
import { LegalDocumentEditor } from './LegalDocumentEditor'
import type { LegalDocument } from '../../../types'

const mocks = vi.hoisted(() => ({
  data: { content: 'Old content', updatedAt: '2026-01-01T00:00:00Z' } as LegalDocument,
  isLoading: false,
  isError: false,
  mutate: vi.fn(),
  isPending: false,
}))

vi.mock('../hooks/useLegalDocument', () => ({
  useLegalDocument: () => ({
    data: mocks.data,
    isLoading: mocks.isLoading,
    isError: mocks.isError,
  }),
}))

vi.mock('../hooks/useUpdateLegalDocument', () => ({
  useUpdateLegalDocument: () => ({ mutate: mocks.mutate, isPending: mocks.isPending }),
}))

describe('LegalDocumentEditor', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mocks.data = { content: 'Old content', updatedAt: '2026-01-01T00:00:00Z' }
    mocks.isLoading = false
    mocks.isError = false
    mocks.isPending = false
  })

  it('loads the existing content into the editor', () => {
    render(<LegalDocumentEditor kind="Impressum" />)
    expect(screen.getByRole('textbox')).toHaveValue('Old content')
  })

  it('saves edited content', async () => {
    render(<LegalDocumentEditor kind="Impressum" />)
    const textbox = screen.getByRole('textbox')
    await userEvent.clear(textbox)
    await userEvent.type(textbox, 'New content')
    await userEvent.click(screen.getByRole('button', { name: /^save$/i }))

    await waitFor(() => expect(mocks.mutate).toHaveBeenCalledWith('New content', expect.anything()))
  })

  it('links the German and English Impressum templates plus the usage guide', () => {
    render(<LegalDocumentEditor kind="Impressum" />)

    expect(screen.getByRole('link', { name: 'Impressum — Deutsch' })).toHaveAttribute(
      'href',
      '/legal-templates/impressum.de.md',
    )
    expect(screen.getByRole('link', { name: 'Impressum — English' })).toHaveAttribute(
      'href',
      '/legal-templates/impressum.en.md',
    )
    expect(screen.getByRole('link', { name: /how to use these templates/i })).toBeInTheDocument()
  })

  it('links the Datenschutz templates and tells the operator to declare their own CDN', () => {
    render(<LegalDocumentEditor kind="Datenschutz" />)

    expect(screen.getByRole('link', { name: 'Datenschutz — Deutsch' })).toHaveAttribute(
      'href',
      '/legal-templates/datenschutzerklaerung.de.md',
    )
    expect(screen.getByRole('link', { name: 'Datenschutz — English' })).toHaveAttribute(
      'href',
      '/legal-templates/privacy-policy.en.md',
    )
    expect(screen.getByText(/Cloudflare/)).toBeInTheDocument()
  })

  it('saves an empty draft as null', async () => {
    render(<LegalDocumentEditor kind="Datenschutz" />)
    await userEvent.clear(screen.getByRole('textbox'))
    await userEvent.click(screen.getByRole('button', { name: /^save$/i }))

    await waitFor(() => expect(mocks.mutate).toHaveBeenCalledWith(null, expect.anything()))
  })
})
