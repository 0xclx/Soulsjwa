import { render, screen, waitForElementToBeRemoved, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { SampleDataTab } from './SampleDataTab'
import { adminApi } from '../api/adminApi'
import type { CreatedSampleEvent } from '../../../types'

vi.mock('../api/adminApi', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../api/adminApi')>()
  return { ...actual, adminApi: { ...actual.adminApi, createSampleEvents: vi.fn() } }
})

const CREATED: CreatedSampleEvent[] = [
  { id: 'e1', name: 'Sample: Not started' },
  { id: 'e2', name: 'Sample: Game in progress' },
  { id: 'e3', name: 'Sample: Between games' },
  { id: 'e4', name: 'Sample: Many games' },
  { id: 'e5', name: 'Sample: Empty setup' },
]

const renderTab = () =>
  render(
    <QueryClientProvider
      client={new QueryClient({ defaultOptions: { mutations: { retry: false } } })}
    >
      <MemoryRouter>
        <SampleDataTab />
      </MemoryRouter>
    </QueryClientProvider>,
  )

const createButton = () => screen.getByRole('button', { name: 'Create sample events' })
const dialog = () => screen.getByRole('dialog', { name: 'Create 5 sample events?' })

describe('SampleDataTab', () => {
  beforeEach(() => {
    vi.mocked(adminApi.createSampleEvents).mockReset()
  })

  it('describes the five events it creates', () => {
    renderTab()

    for (const { name } of CREATED) expect(screen.getByText(name)).toBeInTheDocument()
  })

  it('asks for confirmation, and cancelling sends nothing', async () => {
    renderTab()

    await userEvent.click(createButton())
    expect(dialog()).toHaveTextContent(
      "They are named 'Sample: …', owned by you and visible to everyone until you archive them.",
    )
    await userEvent.click(within(dialog()).getByRole('button', { name: 'Cancel' }))

    expect(adminApi.createSampleEvents).not.toHaveBeenCalled()
  })

  it('creates once on confirm and disables the button while it runs', async () => {
    let resolve: (value: CreatedSampleEvent[]) => void = () => {}
    vi.mocked(adminApi.createSampleEvents).mockReturnValue(
      new Promise((r) => {
        resolve = r
      }),
    )
    renderTab()

    await userEvent.click(createButton())
    await userEvent.click(within(dialog()).getByRole('button', { name: 'Create' }))

    expect(adminApi.createSampleEvents).toHaveBeenCalledTimes(1)
    // The page is aria-hidden until the dialog has finished closing.
    await waitForElementToBeRemoved(() => screen.queryByRole('dialog'))
    expect(createButton()).toBeDisabled()
    resolve(CREATED)
    await screen.findByRole('link', { name: 'Sample: Not started' })
    expect(createButton()).toBeEnabled()
  })

  it('links to every created event', async () => {
    vi.mocked(adminApi.createSampleEvents).mockResolvedValue(CREATED)
    renderTab()

    await userEvent.click(createButton())
    await userEvent.click(within(dialog()).getByRole('button', { name: 'Create' }))

    const created = await screen.findByRole('list', { name: 'Created sample events' })
    expect(
      within(created)
        .getAllByRole('link')
        .map((link) => link.getAttribute('href')),
    ).toEqual(CREATED.map(({ id }) => `/events/${id}`))
    expect(within(created).getByRole('link', { name: 'Sample: Many games' })).toBeInTheDocument()
  })

  it('says so when creating fails', async () => {
    vi.mocked(adminApi.createSampleEvents).mockRejectedValue(new Error('boom'))
    renderTab()

    await userEvent.click(createButton())
    await userEvent.click(within(dialog()).getByRole('button', { name: 'Create' }))

    expect(await screen.findByText('Failed to create the sample events.')).toBeInTheDocument()
    expect(screen.queryByRole('list', { name: 'Created sample events' })).toBeNull()
  })
})
