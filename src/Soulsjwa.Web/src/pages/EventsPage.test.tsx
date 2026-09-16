import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { render, screen, fireEvent, act } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { EventsPage } from './EventsPage'
import { useEvents } from '../features/events/hooks/useEvents'
import { useCreateEvent } from '../features/events/hooks/useCreateEvent'
import { useDuplicateEvent } from '../features/events/hooks/useDuplicateEvent'
import { useUnarchiveEvent } from '../features/events/hooks/useUnarchiveEvent'
import { useCurrentUser } from '../features/users/hooks/useCurrentUser'
import { tokenStore } from '../lib/axios'

vi.mock('../features/events/hooks/useEvents')
vi.mock('../features/events/hooks/useCreateEvent')
vi.mock('../features/events/hooks/useDuplicateEvent')
vi.mock('../features/events/hooks/useUnarchiveEvent')
vi.mock('../features/users/hooks/useCurrentUser')

const EMPTY_LIST = {
  items: [],
  totalCount: 0,
  page: 1,
  pageSize: 20,
  hasNextPage: false,
  hasPreviousPage: false,
}

const renderPage = () =>
  render(
    <MemoryRouter>
      <EventsPage />
    </MemoryRouter>,
  )

describe('<EventsPage />', () => {
  beforeEach(() => {
    tokenStore.clearAccessToken()
    vi.mocked(useEvents).mockReturnValue({
      data: EMPTY_LIST,
      isLoading: false,
      isError: false,
    } as unknown as ReturnType<typeof useEvents>)
    vi.mocked(useCreateEvent).mockReturnValue({
      mutate: vi.fn(),
      isPending: false,
    } as unknown as ReturnType<typeof useCreateEvent>)
    vi.mocked(useDuplicateEvent).mockReturnValue({
      mutate: vi.fn(),
      isPending: false,
    } as unknown as ReturnType<typeof useDuplicateEvent>)
    vi.mocked(useUnarchiveEvent).mockReturnValue({
      mutate: vi.fn(),
      isPending: false,
    } as unknown as ReturnType<typeof useUnarchiveEvent>)
    vi.mocked(useCurrentUser).mockReturnValue({
      data: undefined,
    } as unknown as ReturnType<typeof useCurrentUser>)
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('keeps focus in the search field while typing and debounces the query to a single update', async () => {
    vi.useFakeTimers()
    renderPage()

    const input = screen.getByLabelText('Search events') as HTMLInputElement
    input.focus()

    // Type "souls" one character at a time, as a real keystroke stream would
    // arrive — the search box must never unmount (and so never lose focus)
    // between keystrokes.
    for (const partial of ['s', 'so', 'sou', 'soul', 'souls']) {
      act(() => {
        fireEvent.change(input, { target: { value: partial } })
      })
      expect(document.activeElement).toBe(input)
    }

    // The 300ms debounce window hasn't elapsed — useEvents must still be
    // reading the pre-typing search value, not one query key per keystroke.
    expect(useEvents).toHaveBeenLastCalledWith(1, false, '', 'all')

    await act(async () => {
      await vi.advanceTimersByTimeAsync(300)
    })

    expect(useEvents).toHaveBeenLastCalledWith(1, false, 'souls', 'all')
  })

  it('keeps the header and filter bar mounted while results are loading', () => {
    vi.mocked(useEvents).mockReturnValue({
      data: undefined,
      isLoading: true,
      isError: false,
    } as unknown as ReturnType<typeof useEvents>)

    renderPage()

    expect(screen.getByRole('heading', { name: 'Events' })).toBeInTheDocument()
    expect(screen.getByLabelText('Search events')).toBeInTheDocument()
    expect(screen.getByRole('status', { name: 'Loading events…' })).toBeInTheDocument()
  })
})
