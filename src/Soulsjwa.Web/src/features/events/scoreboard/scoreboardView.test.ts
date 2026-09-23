import { describe, expect, it } from 'vitest'
import { DEFAULT_SCOREBOARD_VIEW, parseScoreboardView } from './scoreboardView'

describe('parseScoreboardView', () => {
  it('reads the whole-event view', () => {
    expect(parseScoreboardView('event')).toBe('event')
  })

  it('reads the current-game view', () => {
    expect(parseScoreboardView('game')).toBe('game')
  })

  it('defaults to the current-game view when the param is absent', () => {
    expect(DEFAULT_SCOREBOARD_VIEW).toBe('game')
    expect(parseScoreboardView(null)).toBe('game')
  })

  it.each(['', 'EVENT', ' event', 'matrix'])('falls back to the default for %j', (raw) => {
    expect(parseScoreboardView(raw)).toBe('game')
  })
})
