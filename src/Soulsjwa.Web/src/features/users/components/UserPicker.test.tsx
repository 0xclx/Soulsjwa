import { render, screen, fireEvent, act } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { UserPicker } from './UserPicker'
import { useUserSearch } from '../hooks/useUserSearch'
import type { UserSearchResult } from '../../../types'

const mocks = vi.hoisted(() => ({
  data: [] as UserSearchResult[],
}))

vi.mock('../hooks/useUserSearch', () => ({
  useUserSearch: vi.fn(() => ({ data: mocks.data, isFetching: false })),
}))

describe('UserPicker', () => {
  afterEach(() => {
    vi.useRealTimers()
  })

  it('debounces the search query instead of firing one request per keystroke', async () => {
    vi.useFakeTimers()
    mocks.data = []
    vi.mocked(useUserSearch).mockClear()
    render(<UserPicker value={null} onChange={vi.fn()} allowFreeText label="User" />)

    const input = screen.getByLabelText('User')
    for (const partial of ['e', 'ex', 'exi', 'exis', 'existin', 'existing']) {
      act(() => {
        fireEvent.change(input, { target: { value: partial } })
      })
    }

    expect(useUserSearch).toHaveBeenLastCalledWith('')

    await act(async () => {
      await vi.advanceTimersByTimeAsync(250)
    })

    expect(useUserSearch).toHaveBeenLastCalledWith('existing')
  })

  it('commits a valid free-text handle as the user types, without Enter', async () => {
    mocks.data = []
    const onChange = vi.fn()
    render(<UserPicker value={null} onChange={onChange} allowFreeText label="User" />)

    await userEvent.type(screen.getByLabelText('User'), 'newstreamer')

    expect(onChange).toHaveBeenLastCalledWith({
      twitchLogin: 'newstreamer',
      label: 'newstreamer',
    })
  })

  it('clears the value while the typed text is not a valid handle', async () => {
    mocks.data = []
    const onChange = vi.fn()
    render(<UserPicker value={null} onChange={onChange} allowFreeText label="User" />)

    await userEvent.type(screen.getByLabelText('User'), 'a')
    expect(onChange).toHaveBeenLastCalledWith(null)

    await userEvent.type(screen.getByLabelText('User'), ' b')
    expect(onChange).toHaveBeenLastCalledWith(null)
  })

  it('does not auto-commit free text when allowFreeText is false', async () => {
    mocks.data = []
    const onChange = vi.fn()
    render(<UserPicker value={null} onChange={onChange} label="User" />)

    await userEvent.type(screen.getByLabelText('User'), 'newstreamer')

    expect(onChange).not.toHaveBeenCalled()
  })

  it('selecting an existing option yields userId, not twitchLogin', async () => {
    mocks.data = [
      { id: 'u1', twitchLogin: 'existinguser', displayName: 'Existing User', isPending: false },
    ]
    const onChange = vi.fn()
    render(<UserPicker value={null} onChange={onChange} allowFreeText label="User" />)

    await userEvent.type(screen.getByLabelText('User'), 'existing')
    const option = await screen.findByText('Existing User')
    await userEvent.click(option)

    expect(onChange).toHaveBeenLastCalledWith({ userId: 'u1', label: 'Existing User' })
  })
})
