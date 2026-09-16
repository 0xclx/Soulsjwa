import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import {
  FAIL_REMAINING_LABEL,
  FailRemainingObjectivesButton,
  NOTHING_REMAINING_HINT,
} from './FailRemainingObjectivesButton'

describe('FailRemainingObjectivesButton', () => {
  it('asks before failing, naming the game, the count and the competitor', async () => {
    const onConfirm = vi.fn(() => Promise.resolve())
    render(
      <FailRemainingObjectivesButton
        gameName="Elden Ring"
        remainingCount={3}
        targetName="Ashen_Kai"
        onConfirm={onConfirm}
      />,
    )

    fireEvent.click(screen.getByRole('button', { name: FAIL_REMAINING_LABEL }))
    expect(onConfirm).not.toHaveBeenCalled()

    const dialog = await screen.findByRole('dialog', {
      name: 'Fail all remaining objectives in Elden Ring?',
    })
    expect(dialog).toHaveAccessibleDescription(/3 objectives still pending for Ashen_Kai/)

    fireEvent.click(screen.getByRole('button', { name: 'Fail 3 objectives' }))
    expect(onConfirm).toHaveBeenCalledOnce()
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
  })

  it('cancelling leaves the objectives alone', async () => {
    const onConfirm = vi.fn(() => Promise.resolve())
    render(
      <FailRemainingObjectivesButton
        gameName="Elden Ring"
        remainingCount={1}
        targetName="you"
        onConfirm={onConfirm}
      />,
    )

    fireEvent.click(screen.getByRole('button', { name: FAIL_REMAINING_LABEL }))
    fireEvent.click(await screen.findByRole('button', { name: 'Cancel' }))

    expect(onConfirm).not.toHaveBeenCalled()
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
  })

  it('says the failures go to the trial run when they do', async () => {
    render(
      <FailRemainingObjectivesButton
        gameName="Sekiro"
        remainingCount={2}
        targetName="you"
        isTrial
        onConfirm={() => Promise.resolve()}
      />,
    )

    fireEvent.click(screen.getByRole('button', { name: FAIL_REMAINING_LABEL }))

    expect(await screen.findByRole('dialog')).toHaveAccessibleDescription(/on the trial run/)
  })

  it('is disabled, with the reason, when nothing is pending or the surface says so', () => {
    const { rerender } = render(
      <FailRemainingObjectivesButton
        gameName="Elden Ring"
        remainingCount={0}
        targetName="you"
        onConfirm={() => Promise.resolve()}
      />,
    )
    expect(screen.getByRole('button', { name: FAIL_REMAINING_LABEL })).toBeDisabled()
    expect(screen.getByLabelText(NOTHING_REMAINING_HINT)).toBeInTheDocument()

    rerender(
      <FailRemainingObjectivesButton
        gameName="Elden Ring"
        remainingCount={4}
        targetName="you"
        disabled
        disabledHint="A trial is recording."
        onConfirm={() => Promise.resolve()}
      />,
    )
    expect(screen.getByRole('button', { name: FAIL_REMAINING_LABEL })).toBeDisabled()
    expect(screen.getByLabelText('A trial is recording.')).toBeInTheDocument()
  })
})
