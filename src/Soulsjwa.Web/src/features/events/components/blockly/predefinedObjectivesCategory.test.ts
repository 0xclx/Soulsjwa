import { describe, it, expect, vi } from 'vitest'
import { registerPredefinedObjectivesCategory } from './predefinedObjectivesCategory'
import { PREDEFINED_OBJECTIVES_CATEGORY_KEY } from './constants'
import type { PredefinedObjective } from '../../../../types'

const OBJECTIVES: PredefinedObjective[] = [
  { id: 'margit', gameId: 7, name: 'Kill Margit, the Fell Omen', score: 10 },
  { id: 'godrick', gameId: 7, name: 'Kill Godrick the Grafted', score: 10 },
  { id: 'rennala', gameId: 7, name: 'Kill Rennala, Queen of the Full Moon', score: 10 },
]

function fakeWorkspace() {
  const callbacks = new Map<string, () => unknown>()
  return {
    registerToolboxCategoryCallback: vi.fn((key: string, fn: () => unknown) => {
      callbacks.set(key, fn)
    }),
    invoke: (key: string) => callbacks.get(key)?.(),
  }
}

describe('registerPredefinedObjectivesCategory', () => {
  it('returns every objective as a block when the filter is empty', () => {
    const workspace = fakeWorkspace()
    const filterText = ''
    registerPredefinedObjectivesCategory(workspace as never, OBJECTIVES, () => filterText)

    const contents = workspace.invoke(PREDEFINED_OBJECTIVES_CATEGORY_KEY) as Array<{
      kind: string
      type?: string
    }>
    expect(contents).toEqual(
      OBJECTIVES.map((obj) => ({ kind: 'block', type: `jsonlogic_predefined_${obj.id}` })),
    )
  })

  it('filters to matching objectives, case-insensitively, when the filter text changes', () => {
    const workspace = fakeWorkspace()
    let filterText = ''
    registerPredefinedObjectivesCategory(workspace as never, OBJECTIVES, () => filterText)

    filterText = 'GODRICK'
    const contents = workspace.invoke(PREDEFINED_OBJECTIVES_CATEGORY_KEY) as Array<{
      kind: string
      type?: string
    }>
    expect(contents).toEqual([{ kind: 'block', type: 'jsonlogic_predefined_godrick' }])
  })

  it('shows a label instead of blocks when nothing matches', () => {
    const workspace = fakeWorkspace()
    let filterText = ''
    registerPredefinedObjectivesCategory(workspace as never, OBJECTIVES, () => filterText)

    filterText = 'nonexistent boss'
    const contents = workspace.invoke(PREDEFINED_OBJECTIVES_CATEGORY_KEY) as Array<{
      kind: string
      text?: string
    }>
    expect(contents).toEqual([{ kind: 'label', text: 'No matching objectives' }])
  })

  it('groups objectives under a category label when more than one category is present', () => {
    const workspace = fakeWorkspace()
    const categorized: PredefinedObjective[] = [
      { id: 'margit', gameId: 7, name: 'Kill Margit', score: 10, category: 'Stormveil Castle' },
      { id: 'godrick', gameId: 7, name: 'Kill Godrick', score: 10, category: 'Stormveil Castle' },
      { id: 'rennala', gameId: 7, name: 'Kill Rennala', score: 10, category: 'Raya Lucaria' },
    ]
    registerPredefinedObjectivesCategory(workspace as never, categorized, () => '')

    const contents = workspace.invoke(PREDEFINED_OBJECTIVES_CATEGORY_KEY) as Array<{
      kind: string
      text?: string
      type?: string
    }>
    expect(contents).toEqual([
      { kind: 'label', text: 'Stormveil Castle' },
      { kind: 'block', type: 'jsonlogic_predefined_margit' },
      { kind: 'block', type: 'jsonlogic_predefined_godrick' },
      { kind: 'label', text: 'Raya Lucaria' },
      { kind: 'block', type: 'jsonlogic_predefined_rennala' },
    ])
  })

  it('does not add a category label when every objective shares one category', () => {
    const workspace = fakeWorkspace()
    registerPredefinedObjectivesCategory(workspace as never, OBJECTIVES, () => '')

    const contents = workspace.invoke(PREDEFINED_OBJECTIVES_CATEGORY_KEY) as Array<{
      kind: string
    }>
    expect(contents.every((item) => item.kind === 'block')).toBe(true)
  })
})
