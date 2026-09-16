import { describe, it, expect } from 'vitest'
import { buildToolbox } from './toolbox'

describe('buildToolbox', () => {
  it('omits the competitor-progress category by default', () => {
    const toolbox = buildToolbox([], [])
    expect(toolbox.contents.some((c) => c.name === 'Competitor Progress')).toBe(false)
  })

  it('includes the competitor-progress category when requested (fail-rule builder)', () => {
    const toolbox = buildToolbox([], [], { includeCompetitorCompletions: true })
    const category = toolbox.contents.find((c) => c.name === 'Competitor Progress')
    expect(category).toBeDefined()
    expect(category?.contents).toEqual([
      { kind: 'block', type: 'jsonlogic_competitor_completions' },
    ])
  })

  it('registers Predefined Objectives as a dynamic (searchable) category rather than a static block list', () => {
    const objectives = [
      { id: 'a', gameId: 7, name: 'Kill Margit', score: 10, rule: '{}' },
      { id: 'b', gameId: 7, name: 'Kill Godrick', score: 10, rule: '{}' },
    ]
    const toolbox = buildToolbox([], objectives)
    const category = toolbox.contents.find((c) => c.name === 'Predefined Objectives')
    expect(category?.custom).toBe('PREDEFINED_OBJECTIVES_SEARCH')
    expect(category?.contents).toBeUndefined()
  })

  it('omits the Predefined Objectives category when there are none', () => {
    const toolbox = buildToolbox([], [])
    expect(toolbox.contents.some((c) => c.name === 'Predefined Objectives')).toBe(false)
  })
})
