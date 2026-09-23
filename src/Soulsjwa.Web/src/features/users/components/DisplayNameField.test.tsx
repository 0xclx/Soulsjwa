import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { DisplayNameField } from './DisplayNameField'

const renderField = (props: Partial<Parameters<typeof DisplayNameField>[0]> = {}) => {
  const onSave = vi.fn()
  const onReset = vi.fn()
  render(
    <DisplayNameField
      currentName="SolaireOfAstora"
      twitchDisplayName="SolaireOfAstora"
      hasOverride={false}
      pending={false}
      error={null}
      onSave={onSave}
      onReset={onReset}
      {...props}
    />,
  )
  return { onSave, onReset }
}

const field = () => screen.getByRole('textbox', { name: 'Display name' })
const save = () => screen.getByRole('button', { name: 'Save' })

describe('DisplayNameField', () => {
  it('is prefilled with the current name and counts characters up to 50', async () => {
    renderField()

    expect(field()).toHaveValue('SolaireOfAstora')
    expect(field()).toHaveAttribute('maxlength', '50')
    expect(screen.getByText('15/50')).toBeInTheDocument()

    await userEvent.clear(field())
    await userEvent.type(field(), 'Sol')
    expect(screen.getByText('3/50')).toBeInTheDocument()
  })

  it('saves the trimmed name', async () => {
    const { onSave } = renderField()

    await userEvent.clear(field())
    await userEvent.type(field(), '  Solaire  ')
    await userEvent.click(save())

    expect(onSave).toHaveBeenCalledWith('Solaire')
  })

  it('saves on Enter', async () => {
    const { onSave } = renderField()

    await userEvent.clear(field())
    await userEvent.type(field(), 'Solaire{Enter}')

    expect(onSave).toHaveBeenCalledWith('Solaire')
  })

  it('cannot save an unchanged or blank name, or while saving', async () => {
    renderField()
    expect(save()).toBeDisabled()

    await userEvent.clear(field())
    await userEvent.type(field(), '   ')
    expect(save()).toBeDisabled()
  })

  it('is disabled while a save is pending', () => {
    renderField({ pending: true })

    expect(field()).toBeDisabled()
    expect(save()).toBeDisabled()
  })

  it('offers the Twitch name back only while a custom name is set', async () => {
    renderField()
    expect(screen.queryByRole('button', { name: /Use Twitch name/ })).toBeNull()
  })

  it('resets to the Twitch name', async () => {
    const { onReset } = renderField({
      currentName: 'Solaire',
      hasOverride: true,
    })

    await userEvent.click(screen.getByRole('button', { name: 'Use Twitch name (SolaireOfAstora)' }))

    expect(onReset).toHaveBeenCalled()
  })

  it('shows a server error on the field', () => {
    renderField({ error: 'Display name must be 50 characters or fewer.' })

    expect(field()).toHaveAttribute('aria-invalid', 'true')
    expect(field()).toHaveAccessibleDescription('Display name must be 50 characters or fewer.')
  })
})
