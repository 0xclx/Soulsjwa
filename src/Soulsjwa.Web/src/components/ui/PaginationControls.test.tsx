import { describe, expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { PaginationControls } from './PaginationControls'

describe('PaginationControls', () => {
  it('renders the current page indicator and a labelled pager', () => {
    render(
      <PaginationControls
        page={2}
        totalPages={5}
        hasPreviousPage
        hasNextPage
        onPrevious={() => {}}
        onNext={() => {}}
        label="events"
      />,
    )
    expect(screen.getByText('Page 2 of 5')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Previous page of events' })).toBeEnabled()
    expect(screen.getByRole('button', { name: 'Next page of events' })).toBeEnabled()
  })

  it('disables navigation at the boundaries', () => {
    render(
      <PaginationControls
        page={1}
        totalPages={1}
        hasPreviousPage={false}
        hasNextPage={false}
        onPrevious={() => {}}
        onNext={() => {}}
      />,
    )
    expect(screen.getByRole('button', { name: 'Previous page of results' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Next page of results' })).toBeDisabled()
  })

  it('invokes callbacks when the pager buttons are clicked', async () => {
    const onPrevious = vi.fn()
    const onNext = vi.fn()
    render(
      <PaginationControls
        page={2}
        totalPages={3}
        hasPreviousPage
        hasNextPage
        onPrevious={onPrevious}
        onNext={onNext}
      />,
    )
    await userEvent.click(screen.getByRole('button', { name: 'Previous page of results' }))
    await userEvent.click(screen.getByRole('button', { name: 'Next page of results' }))
    expect(onPrevious).toHaveBeenCalledOnce()
    expect(onNext).toHaveBeenCalledOnce()
  })
})
