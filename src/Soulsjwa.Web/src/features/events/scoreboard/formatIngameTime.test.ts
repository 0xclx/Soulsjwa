import { describe, expect, it } from 'vitest'
import { formatIngameTime } from './formatIngameTime'

describe('formatIngameTime', () => {
  it('formats sub-hour durations as M:SS', () => {
    expect(formatIngameTime(0)).toBe('0:00')
    expect(formatIngameTime(9_000)).toBe('0:09')
    expect(formatIngameTime(65_000)).toBe('1:05')
    expect(formatIngameTime(59 * 60_000 + 59_000)).toBe('59:59')
  })

  it('formats hour-plus durations as H:MM:SS with zero-padding', () => {
    expect(formatIngameTime(3_600_000)).toBe('1:00:00')
    expect(formatIngameTime(3_661_000)).toBe('1:01:01')
    expect(formatIngameTime(10 * 3_600_000 + 5 * 60_000 + 3_000)).toBe('10:05:03')
  })

  it('truncates sub-second remainders', () => {
    expect(formatIngameTime(1_999)).toBe('0:01')
  })
})
