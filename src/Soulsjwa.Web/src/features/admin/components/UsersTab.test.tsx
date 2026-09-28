import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { UsersTab } from './UsersTab'
import { adminApi } from '../api/adminApi'
import type { AdminUserSummary, PaginatedResponse } from '../../../types'

vi.mock('../api/adminApi', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../api/adminApi')>()
  return {
    ...actual,
    adminApi: { ...actual.adminApi, listUsers: vi.fn(), setUserDisplayName: vi.fn() },
  }
})

const adminUser = (overrides: Partial<AdminUserSummary>): AdminUserSummary => ({
  id: 'u',
  twitchLogin: 'login',
  displayName: 'Name',
  role: 'User',
  isAllowlisted: true,
  createdAt: '2026-01-01T00:00:00Z',
  twitchDisplayName: 'Name',
  displayNameOverride: null,
  ...overrides,
})

const page = (items: AdminUserSummary[]): PaginatedResponse<AdminUserSummary> =>
  ({
    items,
    totalCount: items.length,
    page: 1,
    pageSize: 20,
    hasPreviousPage: false,
    hasNextPage: false,
  }) as PaginatedResponse<AdminUserSummary>

const SOLAIRE = adminUser({
  id: 'sol',
  twitchLogin: 'solaire',
  displayName: 'Solaire',
  twitchDisplayName: 'SolaireOfAstora',
  displayNameOverride: 'Solaire',
})
const PATCHES = adminUser({
  id: 'pat',
  twitchLogin: 'patches',
  displayName: 'Patches',
  twitchDisplayName: 'Patches',
})

const renderTab = () =>
  render(
    <QueryClientProvider
      client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}
    >
      <UsersTab currentUserId="admin" />
    </QueryClientProvider>,
  )

const rowFor = async (login: string) =>
  (await screen.findByText(login)).closest('tr') as HTMLElement

describe('UsersTab display names', () => {
  beforeEach(() => {
    vi.mocked(adminApi.listUsers).mockResolvedValue(page([SOLAIRE, PATCHES]))
    vi.mocked(adminApi.setUserDisplayName).mockReset()
  })

  it('marks users with a custom display name', async () => {
    renderTab()

    expect(within(await rowFor('solaire')).getByText('Custom')).toBeInTheDocument()
    expect(within(await rowFor('patches')).queryByText('Custom')).toBeNull()
  })

  it('renames a user from the dialog and closes it', async () => {
    vi.mocked(adminApi.setUserDisplayName).mockResolvedValue({
      ...PATCHES,
      displayName: 'Trusty Patches',
    })
    renderTab()

    await userEvent.click(
      within(await rowFor('patches')).getByRole('button', {
        name: 'Edit display name for Patches',
      }),
    )
    const dialog = screen.getByRole('dialog', { name: 'Display name for patches' })
    const field = within(dialog).getByRole('textbox', { name: 'Display name' })
    await userEvent.clear(field)
    await userEvent.type(field, 'Trusty Patches')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Save' }))

    expect(adminApi.setUserDisplayName).toHaveBeenCalledWith('pat', 'Trusty Patches')
    await vi.waitFor(() => expect(screen.queryByRole('dialog')).toBeNull())
  })

  it('resets a custom name to the Twitch name', async () => {
    vi.mocked(adminApi.setUserDisplayName).mockResolvedValue({
      ...SOLAIRE,
      displayName: 'SolaireOfAstora',
      displayNameOverride: null,
    })
    renderTab()

    await userEvent.click(
      within(await rowFor('solaire')).getByRole('button', {
        name: 'Edit display name for Solaire',
      }),
    )
    await userEvent.click(
      within(screen.getByRole('dialog')).getByRole('button', {
        name: 'Use Twitch name (SolaireOfAstora)',
      }),
    )

    expect(adminApi.setUserDisplayName).toHaveBeenCalledWith('sol', null)
  })

  it('keeps the dialog open with the server message on failure', async () => {
    vi.mocked(adminApi.setUserDisplayName).mockRejectedValue({
      response: {
        data: { errors: { displayName: ['Display name must be 50 characters or fewer.'] } },
      },
    })
    renderTab()

    await userEvent.click(
      within(await rowFor('patches')).getByRole('button', {
        name: 'Edit display name for Patches',
      }),
    )
    const dialog = screen.getByRole('dialog')
    await userEvent.type(within(dialog).getByRole('textbox', { name: 'Display name' }), '!')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Save' }))

    expect(
      await within(dialog).findByText('Display name must be 50 characters or fewer.'),
    ).toBeInTheDocument()
    expect(screen.getByRole('dialog')).toBeInTheDocument()
  })
})
