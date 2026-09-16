import { MemoryRouter } from 'react-router-dom'
import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { EventSectionTabs } from './EventSectionTabs'

const renderTabs = (canViewRules: boolean) =>
  render(
    <MemoryRouter>
      <EventSectionTabs
        eventId="event-1"
        activeSection="overview"
        canViewActivity
        canViewTokens
        canViewRules={canViewRules}
      />
    </MemoryRouter>,
  )

describe('EventSectionTabs', () => {
  it('hides the Rules tab when the event has no rules and the viewer cannot manage it', () => {
    renderTabs(false)
    expect(screen.queryByRole('tab', { name: /rules/i })).not.toBeInTheDocument()
  })

  it('shows the Rules tab when rules exist or the viewer can manage the event', () => {
    renderTabs(true)
    expect(screen.getByRole('tab', { name: /rules/i })).toBeInTheDocument()
  })
})
