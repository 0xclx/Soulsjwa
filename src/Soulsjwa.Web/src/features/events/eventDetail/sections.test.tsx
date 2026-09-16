import { describe, expect, it } from 'vitest'
import { EVENT_SECTION_NAV, getEventSection } from './sections'

describe('event route sections', () => {
  it('includes the scoreboard in event-local navigation', () => {
    const scoreboard = EVENT_SECTION_NAV.find((item) => item.section === 'scoreboard')
    expect(scoreboard?.label).toBe('Scoreboard')
    expect(scoreboard?.to('event-id')).toBe('/events/event-id/scoreboard')
  })

  it('resolves scoreboard and event overview routes', () => {
    expect(getEventSection('/events/event-id/scoreboard')).toBe('scoreboard')
    expect(getEventSection('/events/event-id')).toBe('overview')
  })

  it('includes rules in event-local navigation', () => {
    const rules = EVENT_SECTION_NAV.find((item) => item.section === 'rules')
    expect(rules?.label).toBe('Rules')
    expect(rules?.to('event-id')).toBe('/events/event-id/rules')
  })

  it('resolves the rules route', () => {
    expect(getEventSection('/events/event-id/rules')).toBe('rules')
  })
})
