import { render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { AdminThemePage } from './AdminThemePage'

vi.mock('../features/theme/siteTheme/components/SiteThemeEditor', () => ({
  SiteThemeEditor: () => <div data-testid="editor">site theme editor</div>,
}))

describe('AdminThemePage', () => {
  it('renders the site theme editor', () => {
    render(<AdminThemePage />)
    expect(screen.getByText('Site theme')).toBeInTheDocument()
    expect(screen.getByTestId('editor')).toBeInTheDocument()
  })
})
