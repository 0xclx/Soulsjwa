import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { EventRulesPage } from './EventRulesPage'
import type { EventRules } from '../types'

const mocks = vi.hoisted(() => ({
  canManage: false,
  rules: { content: null, updatedAt: null } as EventRules,
  mutate: vi.fn(),
}))

vi.mock('../features/events/hooks/useEventRoute', () => ({
  useEventRoute: () => ({ eventId: 'event-1', canManage: mocks.canManage }),
}))

vi.mock('../features/events/hooks/useEventRules', () => ({
  useEventRules: () => ({ data: mocks.rules, isLoading: false, isError: false }),
}))

vi.mock('../features/events/hooks/useUpdateEventRules', () => ({
  useUpdateEventRules: () => ({ mutate: mocks.mutate, isPending: false }),
}))

describe('EventRulesPage', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mocks.canManage = false
    mocks.rules = { content: null, updatedAt: null }
  })

  it('shows an empty state for a viewer who cannot manage the event when rules are unset', () => {
    render(<EventRulesPage />)
    expect(screen.getByText('No rules yet')).toBeInTheDocument()
    expect(screen.getByText('This event has no published rules.')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /add rules/i })).not.toBeInTheDocument()
  })

  it('offers to add rules for a manager when rules are unset', () => {
    mocks.canManage = true
    render(<EventRulesPage />)
    expect(screen.getByRole('button', { name: /add rules/i })).toBeInTheDocument()
  })

  it('renders existing rules through MarkdownView', () => {
    mocks.rules = {
      content: '# Ground rules\n\nNo item duping.',
      updatedAt: '2026-01-01T00:00:00Z',
    }
    render(<EventRulesPage />)
    expect(screen.getByRole('heading', { name: 'Ground rules' })).toBeInTheDocument()
    expect(screen.getByText('No item duping.')).toBeInTheDocument()
  })

  it('never renders a script tag or javascript: link from rules content (XSS re-check)', () => {
    mocks.rules = {
      content: '<script>alert(1)</script>[x](javascript:alert(1))',
      updatedAt: null,
    }
    const { container } = render(<EventRulesPage />)
    expect(container.querySelector('script')).toBeNull()
    expect(container.innerHTML).not.toMatch(/href=["']javascript:/i)
  })

  it('lets a manager edit and save rules', async () => {
    mocks.canManage = true
    mocks.rules = { content: 'Old rules', updatedAt: '2026-01-01T00:00:00Z' }
    render(<EventRulesPage />)

    await userEvent.click(screen.getByRole('button', { name: /edit/i }))
    const textbox = screen.getByRole('textbox')
    await userEvent.clear(textbox)
    await userEvent.type(textbox, 'New rules')
    await userEvent.click(screen.getByRole('button', { name: /^save$/i }))

    await waitFor(() => expect(mocks.mutate).toHaveBeenCalledWith('New rules', expect.anything()))
  })
})
