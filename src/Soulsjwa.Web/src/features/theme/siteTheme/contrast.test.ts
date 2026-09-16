import { describe, it, expect } from 'vitest'
import { contrastRatio, isValidHex } from './contrast'

describe('contrastRatio', () => {
  it('black on white is ~21:1', () => {
    expect(contrastRatio('#000000', '#ffffff')).toBeCloseTo(21, 0)
  })

  it('a colour against itself is 1:1', () => {
    expect(contrastRatio('#6d28d9', '#6d28d9')).toBeCloseTo(1, 5)
  })

  it('is symmetric', () => {
    const a = contrastRatio('#6d28d9', '#f5f5f5')
    const b = contrastRatio('#f5f5f5', '#6d28d9')
    expect(a).toBeCloseTo(b, 10)
  })

  it('matches the backend default light palette (all slots pass 4.5:1 against Default)', () => {
    const background = '#f5f5f5'
    for (const fg of ['#6d28d9', '#b91c1c', '#0369a1', '#15803d', '#b45309']) {
      expect(contrastRatio(fg, background)).toBeGreaterThanOrEqual(4.5)
    }
  })
})

describe('isValidHex', () => {
  it('accepts a well-formed #rrggbb colour', () => {
    expect(isValidHex('#6d28d9')).toBe(true)
    expect(isValidHex('#FFFFFF')).toBe(true)
  })

  it('rejects malformed values', () => {
    expect(isValidHex('6d28d9')).toBe(false)
    expect(isValidHex('#fff')).toBe(false)
    expect(isValidHex('#gggggg')).toBe(false)
    expect(isValidHex('')).toBe(false)
  })
})
