import { describe, expect, it } from 'vitest'
import { reorderList } from './dragReorder'

describe('reorderList', () => {
  it('moves an item down', () => {
    expect(reorderList(['a', 'b', 'c'], 0, 2)).toEqual(['b', 'c', 'a'])
  })

  it('moves an item up', () => {
    expect(reorderList(['a', 'b', 'c'], 2, 0)).toEqual(['c', 'a', 'b'])
  })

  it('moving to the same index is a no-op', () => {
    expect(reorderList(['a', 'b', 'c'], 1, 1)).toEqual(['a', 'b', 'c'])
  })

  it('handles boundary indices', () => {
    expect(reorderList(['a', 'b', 'c'], 0, 0)).toEqual(['a', 'b', 'c'])
    expect(reorderList(['a', 'b', 'c'], 2, 2)).toEqual(['a', 'b', 'c'])
  })
})
