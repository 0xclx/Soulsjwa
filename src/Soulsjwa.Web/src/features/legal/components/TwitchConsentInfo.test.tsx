import { MemoryRouter } from 'react-router-dom'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi, beforeEach } from 'vitest'
import { TwitchConsentInfo } from './TwitchConsentInfo'
import type { LegalDocument } from '../../../types'

const mocks = vi.hoisted(() => ({
  datenschutz: { content: null, updatedAt: null } as LegalDocument,
}))

vi.mock('../hooks/useLegalDocument', () => ({
  useLegalDocument: () => ({ data: mocks.datenschutz }),
}))

const renderInfo = () =>
  render(
    <MemoryRouter>
      <TwitchConsentInfo />
    </MemoryRouter>,
  )

const openNotice = async () => {
  await userEvent.click(screen.getByRole('button', { name: /what we store when you sign in/i }))
}

describe('<TwitchConsentInfo />', () => {
  beforeEach(() => {
    mocks.datenschutz = { content: null, updatedAt: null }
  })

  it('reveals the notice only after the help icon is activated', async () => {
    renderInfo()
    expect(screen.queryByText(/Mit der Anmeldung stimmst du zu/)).not.toBeInTheDocument()

    await openNotice()

    expect(
      screen.getByRole('dialog', { name: /what we store when you sign in/i }),
    ).toBeInTheDocument()
    expect(screen.getByText(/Mit der Anmeldung stimmst du zu/)).toBeInTheDocument()
  })

  it('states in both languages what is stored', async () => {
    renderInfo()
    await openNotice()

    expect(screen.getByText(/Twitch-ID, deinen Twitch-Namen und die/)).toBeInTheDocument()
    expect(screen.getByText(/Twitch ID, your Twitch name and the email/)).toBeInTheDocument()
  })

  it('links to the Datenschutz page in both languages once that document exists', async () => {
    mocks.datenschutz = { content: 'We store your Twitch ID.', updatedAt: '2026-01-01T00:00:00Z' }
    renderInfo()
    await openNotice()

    expect(screen.getByRole('link', { name: 'Datenschutzerklärung' })).toHaveAttribute(
      'href',
      '/datenschutz',
    )
    expect(screen.getByRole('link', { name: 'privacy policy' })).toHaveAttribute(
      'href',
      '/datenschutz',
    )
  })

  it('names the privacy policy as plain text when no document is published', async () => {
    renderInfo()
    await openNotice()

    expect(screen.queryByRole('link')).not.toBeInTheDocument()
    expect(
      screen.getByText(/Details findest du in unserer Datenschutzerklärung/),
    ).toBeInTheDocument()
  })
})
