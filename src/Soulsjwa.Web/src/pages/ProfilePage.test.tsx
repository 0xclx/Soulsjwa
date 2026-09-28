import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ProfilePage } from './ProfilePage'
import { usersApi } from '../features/users/api/usersApi'
import type { User } from '../types'

vi.mock('../lib/axios', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../lib/axios')>()
  return { ...actual, useIsAuthenticated: () => true }
})

vi.mock('../features/users/api/usersApi', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../features/users/api/usersApi')>()
  return {
    ...actual,
    usersApi: {
      ...actual.usersApi,
      getMe: vi.fn(),
      getApiKeys: vi.fn(),
      updateMyDisplayName: vi.fn(),
    },
  }
})

const user: User = {
  id: 'u1',
  twitchLogin: 'solaire',
  displayName: 'SolaireOfAstora',
  createdAt: '2026-01-01T00:00:00Z',
  role: 'User',
  isAllowlisted: true,
  twitchDisplayName: 'SolaireOfAstora',
  displayNameOverride: null,
}

const renderPage = () =>
  render(
    <QueryClientProvider
      client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}
    >
      <MemoryRouter>
        <ProfilePage />
      </MemoryRouter>
    </QueryClientProvider>,
  )

const section = () => screen.getByRole('region', { name: 'Display name' })

describe('ProfilePage display name', () => {
  beforeEach(() => {
    vi.mocked(usersApi.getMe).mockResolvedValue(user)
    vi.mocked(usersApi.getApiKeys).mockResolvedValue([])
    vi.mocked(usersApi.updateMyDisplayName).mockReset()
  })

  it('saves a new name and shows it in the header', async () => {
    vi.mocked(usersApi.updateMyDisplayName).mockResolvedValue({
      ...user,
      displayName: 'Solaire',
      displayNameOverride: 'Solaire',
    })
    renderPage()
    const field = await within(
      await screen.findByRole('region', { name: 'Display name' }),
    ).findByRole('textbox', { name: 'Display name' })

    await userEvent.clear(field)
    await userEvent.type(field, 'Solaire')
    await userEvent.click(within(section()).getByRole('button', { name: 'Save' }))

    expect(usersApi.updateMyDisplayName).toHaveBeenCalledWith('Solaire')
    expect(await screen.findByRole('heading', { name: 'Solaire' })).toBeInTheDocument()
  })

  it('resets to the Twitch name by sending null', async () => {
    vi.mocked(usersApi.getMe).mockResolvedValue({
      ...user,
      displayName: 'Solaire',
      displayNameOverride: 'Solaire',
    })
    vi.mocked(usersApi.updateMyDisplayName).mockResolvedValue(user)
    renderPage()

    await userEvent.click(
      await within(await screen.findByRole('region', { name: 'Display name' })).findByRole(
        'button',
        {
          name: 'Use Twitch name (SolaireOfAstora)',
        },
      ),
    )

    expect(usersApi.updateMyDisplayName).toHaveBeenCalledWith(null)
    expect(await screen.findByRole('heading', { name: 'SolaireOfAstora' })).toBeInTheDocument()
  })

  it('shows the server validation message on the field', async () => {
    vi.mocked(usersApi.updateMyDisplayName).mockRejectedValue({
      response: {
        data: { errors: { displayName: ['Display name must not contain control characters.'] } },
      },
    })
    renderPage()
    const field = await within(
      await screen.findByRole('region', { name: 'Display name' }),
    ).findByRole('textbox', { name: 'Display name' })

    await userEvent.clear(field)
    await userEvent.type(field, 'Solaire')
    await userEvent.click(within(section()).getByRole('button', { name: 'Save' }))

    expect(
      await within(section()).findByText('Display name must not contain control characters.'),
    ).toBeInTheDocument()
  })
})
