import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { ConfirmDialog } from './ConfirmDialog'

describe('ConfirmDialog', () => {
  it('names the consequence and initially focuses the safe action', async () => {
    const onCancel = vi.fn()
    const onConfirm = vi.fn()

    render(
      <ConfirmDialog
        open
        title="Delete objective?"
        description="Completion records will be permanently removed."
        confirmLabel="Delete objective"
        onCancel={onCancel}
        onConfirm={onConfirm}
      />,
    )

    expect(screen.getByRole('dialog', { name: 'Delete objective?' })).toHaveAccessibleDescription(
      'Completion records will be permanently removed.',
    )
    const cancelButton = screen.getByRole('button', { name: 'Cancel' })
    await waitFor(() => expect(cancelButton).toHaveFocus())

    fireEvent.click(screen.getByRole('button', { name: 'Delete objective' }))
    expect(onConfirm).toHaveBeenCalledOnce()
    expect(onCancel).not.toHaveBeenCalled()
  })
})
