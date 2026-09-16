import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { DisableTrialRunDialog } from './DisableTrialRunDialog'

describe('DisableTrialRunDialog', () => {
  it('keeps the confirm button disabled until the game name is typed exactly', async () => {
    const onConfirm = vi.fn()
    render(
      <DisableTrialRunDialog
        open
        gameName="Elden Ring"
        pending={false}
        error={null}
        onCancel={() => {}}
        onConfirm={onConfirm}
      />,
    )

    const confirmButton = screen.getByRole('button', { name: /disable trial mode/i })
    expect(confirmButton).toBeDisabled()

    await userEvent.type(screen.getByLabelText('Game name'), 'Elden')
    expect(confirmButton).toBeDisabled()

    await userEvent.type(screen.getByLabelText('Game name'), ' Ring')
    expect(confirmButton).toBeEnabled()

    await userEvent.click(confirmButton)
    expect(onConfirm).toHaveBeenCalledTimes(1)
  })

  it('rejects a partial or case-mismatched name', async () => {
    render(
      <DisableTrialRunDialog
        open
        gameName="Elden Ring"
        pending={false}
        error={null}
        onCancel={() => {}}
        onConfirm={() => {}}
      />,
    )

    await userEvent.type(screen.getByLabelText('Game name'), 'elden ring')
    expect(screen.getByRole('button', { name: /disable trial mode/i })).toBeDisabled()
  })

  it('surfaces a server error', () => {
    render(
      <DisableTrialRunDialog
        open
        gameName="Elden Ring"
        pending={false}
        error="Trial run not enabled."
        onCancel={() => {}}
        onConfirm={() => {}}
      />,
    )

    expect(screen.getByText('Trial run not enabled.')).toBeInTheDocument()
  })
})
