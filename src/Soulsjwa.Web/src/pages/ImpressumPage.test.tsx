import { render, screen } from '@testing-library/react'
import { createMemoryRouter, RouterProvider } from 'react-router-dom'
import { describe, expect, it, vi, beforeEach } from 'vitest'
import { ImpressumPage } from './ImpressumPage'
import { RouteErrorElement } from '../components/RouteErrorElement'
import type { LegalDocument } from '../types'

const mocks = vi.hoisted(() => ({
  data: { content: null, updatedAt: null } as LegalDocument | undefined,
  isLoading: false,
  isError: false,
}))

vi.mock('../features/legal/hooks/useLegalDocument', () => ({
  useLegalDocument: () => ({
    data: mocks.data,
    isLoading: mocks.isLoading,
    isError: mocks.isError,
  }),
}))

const renderPage = () => {
  const router = createMemoryRouter(
    [{ path: '/', element: <ImpressumPage />, errorElement: <RouteErrorElement /> }],
    { initialEntries: ['/'] },
  )
  return render(<RouterProvider router={router} />)
}

describe('ImpressumPage', () => {
  beforeEach(() => {
    mocks.data = { content: null, updatedAt: null }
    mocks.isLoading = false
    mocks.isError = false
  })

  it('renders the app 404 page when the document is empty', () => {
    renderPage()
    expect(screen.getByText('Page not found')).toBeInTheDocument()
  })

  it('renders the document content through MarkdownView when set', () => {
    mocks.data = { content: '# Operator details\n\nSome legal text.', updatedAt: null }
    renderPage()
    expect(screen.getByRole('heading', { name: 'Operator details' })).toBeInTheDocument()
    expect(screen.getByText('Some legal text.')).toBeInTheDocument()
  })

  it('renders an error message when the query fails', () => {
    mocks.isError = true
    renderPage()
    expect(screen.getByText('Failed to load this document.')).toBeInTheDocument()
  })
})
