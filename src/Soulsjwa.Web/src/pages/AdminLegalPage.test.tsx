import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { AdminLegalPage } from './AdminLegalPage'

const mocks = vi.hoisted(() => ({ editorKinds: [] as string[] }))

vi.mock('../features/legal/components/LegalDocumentEditor', () => ({
  LegalDocumentEditor: ({ kind }: { kind: string }) => {
    mocks.editorKinds.push(kind)
    return <div data-testid="editor">{kind} editor</div>
  },
}))

describe('AdminLegalPage', () => {
  it('shows the Impressum editor by default and switches to Datenschutz on tab click', async () => {
    render(<AdminLegalPage />)

    expect(screen.getByText('Impressum editor')).toBeInTheDocument()

    await userEvent.click(screen.getByRole('tab', { name: 'Datenschutz' }))

    expect(screen.getByText('Datenschutz editor')).toBeInTheDocument()
    expect(screen.queryByText('Impressum editor')).not.toBeInTheDocument()
  })
})
