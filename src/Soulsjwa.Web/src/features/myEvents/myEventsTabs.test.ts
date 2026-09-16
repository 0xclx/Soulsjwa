import { describe, expect, it } from 'vitest'
import { MY_EVENTS_TAB_NAV, getMyEventsTab } from './myEventsTabs'

describe('getMyEventsTab', () => {
  it('resolves each tab from its own path', () => {
    expect(getMyEventsTab('/my-events')).toBe('competing')
    expect(getMyEventsTab('/my-events/delegated')).toBe('delegated')
    expect(getMyEventsTab('/my-events/owned')).toBe('owned')
    expect(getMyEventsTab('/my-events/trial')).toBe('trial')
  })

  it('falls back to competing for an unknown suffix', () => {
    expect(getMyEventsTab('/my-events/nonsense')).toBe('competing')
    expect(getMyEventsTab('/')).toBe('competing')
  })

  it('round-trips every nav entry through its own path', () => {
    for (const item of MY_EVENTS_TAB_NAV) {
      expect(getMyEventsTab(item.to)).toBe(item.tab)
    }
  })
})
