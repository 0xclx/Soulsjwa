import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { AddCompetitorDialog } from './AddCompetitorDialog'
import type { UserSearchResult } from '../../../types'

const mocks = vi.hoisted(() => ({
  data: [] as UserSearchResult[],
  mutate: vi.fn(),
}))

vi.mock('../../users/hooks/useUserSearch', () => ({
  useUserSearch: () => ({ data: mocks.data, isFetching: false }),
}))

vi.mock('../hooks/useAddCompetitor', () => ({
  useAddCompetitor: () => ({ mutate: mocks.mutate, isPending: false }),
}))

const renderDialog = () => {
  const client = new QueryClient()
  return render(
    <QueryClientProvider client={client}>
      <AddCompetitorDialog open eventId="event-1" onClose={vi.fn()} />
    </QueryClientProvider>,
  )
}

const submitButton = () => screen.getByRole('button', { name: /^add$/i })

describe('AddCompetitorDialog', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mocks.data = []
  })

  it('enables Add once a valid handle is typed, without pressing Enter', async () => {
    renderDialog()
    expect(submitButton()).toBeDisabled()

    await userEvent.type(screen.getByLabelText(/twitch handle or known user/i), 'newstreamer')

    expect(submitButton()).toBeEnabled()
  })

  it('leaves Add disabled for a too-short handle or one containing a space', async () => {
    renderDialog()
    const input = screen.getByLabelText(/twitch handle or known user/i)

    await userEvent.type(input, 'a')
    expect(submitButton()).toBeDisabled()

    await userEvent.clear(input)
    await userEvent.type(input, 'has space')
    expect(submitButton()).toBeDisabled()
  })

  it('selecting an existing user submits userId, not twitchLogin', async () => {
    mocks.data = [
      { id: 'u1', twitchLogin: 'existinguser', displayName: 'Existing User', isPending: false },
    ]
    renderDialog()

    await userEvent.type(screen.getByLabelText(/twitch handle or known user/i), 'existing')
    await userEvent.click(await screen.findByText('Existing User'))
    expect(submitButton()).toBeEnabled()

    await userEvent.click(submitButton())

    expect(mocks.mutate).toHaveBeenCalledWith(
      { userId: 'u1', twitchLogin: undefined, isStreamer: false },
      expect.anything(),
    )
  })
})
