import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { CalendarEntryEditorDialog } from './CalendarEntryEditorDialog'
import { CALENDAR_ENTRY_COLORS } from '../../../types/calendar'

vi.mock('../../media/hooks/useUploadMedia', () => ({
  useUploadMedia: () => ({ mutate: vi.fn(), isPending: false, reset: vi.fn() }),
}))

describe('CalendarEntryEditorDialog', () => {
  it('requires a title before submitting', async () => {
    const onSubmit = vi.fn()
    render(
      <CalendarEntryEditorDialog
        open
        entry={null}
        pending={false}
        error={null}
        onClose={() => {}}
        onSubmit={onSubmit}
      />,
    )

    await userEvent.type(screen.getByLabelText('Starts', { exact: false }), '2026-10-01T18:00')
    await userEvent.type(screen.getByLabelText('Ends', { exact: false }), '2026-10-01T20:00')
    await userEvent.click(screen.getByRole('button', { name: /save/i }))

    expect(screen.getByText('Title is required.')).toBeInTheDocument()
    expect(onSubmit).not.toHaveBeenCalled()
  })

  it('rejects an end time that is not after the start time', async () => {
    const onSubmit = vi.fn()
    render(
      <CalendarEntryEditorDialog
        open
        entry={null}
        pending={false}
        error={null}
        onClose={() => {}}
        onSubmit={onSubmit}
      />,
    )

    await userEvent.type(screen.getByLabelText('Title', { exact: false }), 'Grand Finals')
    await userEvent.type(screen.getByLabelText('Starts', { exact: false }), '2026-10-01T20:00')
    await userEvent.type(screen.getByLabelText('Ends', { exact: false }), '2026-10-01T18:00')
    await userEvent.click(screen.getByRole('button', { name: /save/i }))

    expect(screen.getByText('End must be after start.')).toBeInTheDocument()
    expect(onSubmit).not.toHaveBeenCalled()
  })

  it('offers colour as a fixed slot selector, never free-form text', async () => {
    render(
      <CalendarEntryEditorDialog
        open
        entry={null}
        pending={false}
        error={null}
        onClose={() => {}}
        onSubmit={() => {}}
      />,
    )

    await userEvent.click(screen.getByLabelText('Colour'))
    const listbox = await screen.findByRole('listbox')
    const options = screen.getAllByRole('option')
    expect(options).toHaveLength(CALENDAR_ENTRY_COLORS.length)
    CALENDAR_ENTRY_COLORS.forEach((slot) => {
      expect(within(listbox).getByText(slot)).toBeInTheDocument()
    })
  })

  it('submits a valid entry with the selected colour slot', async () => {
    const onSubmit = vi.fn()
    render(
      <CalendarEntryEditorDialog
        open
        entry={null}
        pending={false}
        error={null}
        onClose={() => {}}
        onSubmit={onSubmit}
      />,
    )

    await userEvent.type(screen.getByLabelText('Title', { exact: false }), 'Grand Finals')
    await userEvent.type(screen.getByLabelText('Starts', { exact: false }), '2026-10-01T18:00')
    await userEvent.type(screen.getByLabelText('Ends', { exact: false }), '2026-10-01T20:00')
    await userEvent.click(screen.getByRole('button', { name: /save/i }))

    expect(onSubmit).toHaveBeenCalledTimes(1)
    expect(onSubmit).toHaveBeenCalledWith(
      expect.objectContaining({ title: 'Grand Finals', color: 'Default' }),
    )
  })
})
