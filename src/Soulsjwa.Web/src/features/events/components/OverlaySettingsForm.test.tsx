import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import type { OverlayTokenSettings } from '../../../types/overlay'
import { OVERLAY_DEFAULT_SETTINGS, OVERLAY_LIMITS } from '../overlay/overlayConfig'
import { OverlaySettingsForm, TOP_RANKED_LABEL } from './OverlaySettingsForm'

const competitors = [
  { userId: 'u1', displayName: 'Chosen Undead' },
  { userId: 'u2', displayName: 'Tarnished' },
]
const games = [
  { eventGameId: 'g1', gameName: 'Dark Souls', isEnabled: true, hasActiveTrial: false },
]

const renderForm = (value: OverlayTokenSettings = OVERLAY_DEFAULT_SETTINGS) => {
  const onChange = vi.fn()
  render(
    <OverlaySettingsForm
      value={value}
      onChange={onChange}
      competitors={competitors}
      games={games}
    />,
  )
  return onChange
}

const lastChange = (onChange: ReturnType<typeof vi.fn>): OverlayTokenSettings =>
  onChange.mock.calls.at(-1)![0] as OverlayTokenSettings

describe('<OverlaySettingsForm />', () => {
  it('pins exactly one player and unpins back to everyone', async () => {
    const onChange = renderForm()

    await userEvent.click(screen.getByLabelText(/pin to player/i))
    await userEvent.click(screen.getByRole('option', { name: 'Tarnished' }))
    expect(lastChange(onChange).playerIds).toEqual(['u2'])

    onChange.mockClear()
    render(
      <OverlaySettingsForm
        value={{ ...OVERLAY_DEFAULT_SETTINGS, playerIds: ['u2'] }}
        onChange={onChange}
        competitors={competitors}
        games={games}
      />,
    )
    await userEvent.click(screen.getAllByLabelText(/pin to player/i).at(-1)!)
    await userEvent.click(screen.getByRole('option', { name: TOP_RANKED_LABEL }))
    expect(lastChange(onChange).playerIds).toBeNull()
  })

  it('restricts to one game by id', async () => {
    const onChange = renderForm()

    await userEvent.click(screen.getByLabelText(/restrict to game/i))
    await userEvent.click(screen.getByRole('option', { name: /dark souls/i }))

    expect(lastChange(onChange).gameIds).toEqual(['g1'])
  })

  it('flips a toggle and reports every other knob unchanged', async () => {
    const onChange = renderForm()

    await userEvent.click(screen.getByLabelText('Pagination'))

    expect(lastChange(onChange)).toEqual({ ...OVERLAY_DEFAULT_SETTINGS, showPagination: false })
  })

  it('clamps a typed number into its documented range', async () => {
    const onChange = renderForm()
    const pageSize = screen.getByLabelText('Page size')

    await userEvent.clear(pageSize)
    await userEvent.type(pageSize, '999')

    expect(lastChange(onChange).pageSize).toBe(OVERLAY_LIMITS.pageSize.max)
  })

  it('stores a blank title as no title', async () => {
    const onChange = renderForm({ ...OVERLAY_DEFAULT_SETTINGS, title: 'Finals' })

    await userEvent.clear(screen.getByLabelText(/title override/i))

    expect(lastChange(onChange).title).toBeNull()
  })
})
