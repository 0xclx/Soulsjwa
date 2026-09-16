import { describe, expect, it } from 'vitest'
import { getEventPath, getEventUrlIdentifier } from './eventUrl'

describe('event URL helpers', () => {
  it('prefers an alias when one is available', () => {
    expect(getEventUrlIdentifier('event-id', 'summer-race')).toBe('summer-race')
    expect(getEventPath('event-id', 'summer-race')).toBe('/events/summer-race')
  })

  it('falls back to the event ID', () => {
    expect(getEventUrlIdentifier('event-id', null)).toBe('event-id')
    expect(getEventPath('event-id')).toBe('/events/event-id')
  })
})
