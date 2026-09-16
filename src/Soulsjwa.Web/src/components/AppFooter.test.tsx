import { MemoryRouter } from 'react-router-dom'
import { render, screen } from '@testing-library/react'
import { describe, expect, it, vi, beforeEach } from 'vitest'
import { AppFooter } from './AppFooter'
import type { LegalDocument } from '../types'

const mocks = vi.hoisted(() => ({
  impressum: { content: null, updatedAt: null } as LegalDocument,
  datenschutz: { content: null, updatedAt: null } as LegalDocument,
}))

vi.mock('../features/legal/hooks/useLegalDocument', () => ({
  useLegalDocument: (kind: 'Impressum' | 'Datenschutz') => ({
    data: kind === 'Impressum' ? mocks.impressum : mocks.datenschutz,
  }),
}))

const renderFooter = () =>
  render(
    <MemoryRouter>
      <AppFooter />
    </MemoryRouter>,
  )

describe('AppFooter', () => {
  beforeEach(() => {
    mocks.impressum = { content: null, updatedAt: null }
    mocks.datenschutz = { content: null, updatedAt: null }
  })

  it('renders no footer at all when both documents are empty', () => {
    const { container } = renderFooter()
    expect(container.querySelector('footer')).toBeNull()
    expect(screen.queryByRole('link')).not.toBeInTheDocument()
  })

  it('renders one link when only Impressum is set', () => {
    mocks.impressum = { content: 'Operator info.', updatedAt: '2026-01-01T00:00:00Z' }
    renderFooter()
    expect(screen.getByRole('link', { name: 'Impressum' })).toHaveAttribute('href', '/impressum')
    expect(screen.queryByRole('link', { name: 'Datenschutz' })).not.toBeInTheDocument()
  })

  it('renders both links when both documents are set', () => {
    mocks.impressum = { content: 'Operator info.', updatedAt: '2026-01-01T00:00:00Z' }
    mocks.datenschutz = { content: 'We log nothing.', updatedAt: '2026-01-01T00:00:00Z' }
    renderFooter()
    expect(screen.getByRole('link', { name: 'Impressum' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Datenschutz' })).toHaveAttribute(
      'href',
      '/datenschutz',
    )
  })
})
