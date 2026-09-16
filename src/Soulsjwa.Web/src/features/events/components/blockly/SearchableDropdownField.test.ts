import { describe, it, expect, vi } from 'vitest'
import { buildFilterableList } from './SearchableDropdownField'

const OPTIONS: Array<[string, string]> = [
  ['Death count', 'death_count'],
  ['Boss: Jagged Peak Drake', 'boss_jagged_peak_drake'],
  ['Boss: Elden Beast', 'boss_elden_beast'],
  ['Hours played', 'hours_played'],
]

const labels = (list: HTMLDivElement) =>
  Array.from(list.querySelectorAll('.blocklySearchableDropdownItem')).map((el) => el.textContent)

describe('buildFilterableList', () => {
  it('renders every option unfiltered', () => {
    const { container } = buildFilterableList(OPTIONS, () => null, vi.fn())
    const list = container.querySelector<HTMLDivElement>('.blocklySearchableDropdownList')!
    expect(labels(list)).toEqual(OPTIONS.map(([label]) => label))
  })

  it('narrows the list as the filter input is typed into', () => {
    const { container, input } = buildFilterableList(OPTIONS, () => null, vi.fn())
    const list = container.querySelector<HTMLDivElement>('.blocklySearchableDropdownList')!

    input.value = 'death'
    input.dispatchEvent(new Event('input'))

    expect(labels(list)).toEqual(['Death count'])
  })

  it('matches case-insensitively and by substring anywhere in the label', () => {
    const { container, input } = buildFilterableList(OPTIONS, () => null, vi.fn())
    const list = container.querySelector<HTMLDivElement>('.blocklySearchableDropdownList')!

    input.value = 'BOSS'
    input.dispatchEvent(new Event('input'))

    expect(labels(list)).toEqual(['Boss: Jagged Peak Drake', 'Boss: Elden Beast'])
  })

  it('shows an empty state when nothing matches', () => {
    const { container, input } = buildFilterableList(OPTIONS, () => null, vi.fn())
    const list = container.querySelector<HTMLDivElement>('.blocklySearchableDropdownList')!

    input.value = 'nonexistent'
    input.dispatchEvent(new Event('input'))

    expect(labels(list)).toEqual([])
    expect(list.querySelector('.blocklySearchableDropdownEmpty')?.textContent).toBe('No matches')
  })

  it('invokes onSelect with the option value when an item is clicked', () => {
    const onSelect = vi.fn()
    const { container } = buildFilterableList(OPTIONS, () => null, onSelect)
    const list = container.querySelector<HTMLDivElement>('.blocklySearchableDropdownList')!

    const item = Array.from(
      list.querySelectorAll<HTMLDivElement>('.blocklySearchableDropdownItem'),
    ).find((el) => el.textContent === 'Death count')!
    item.click()

    expect(onSelect).toHaveBeenCalledWith('death_count')
  })

  it('marks the currently selected value', () => {
    const { container } = buildFilterableList(OPTIONS, () => 'hours_played', vi.fn())
    const list = container.querySelector<HTMLDivElement>('.blocklySearchableDropdownList')!

    const selected = list.querySelector('.isSelected')
    expect(selected?.textContent).toBe('Hours played')
  })

  it('Enter selects the highlighted (first matching) item', () => {
    const onSelect = vi.fn()
    const { input } = buildFilterableList(OPTIONS, () => null, onSelect)

    input.value = 'boss'
    input.dispatchEvent(new Event('input'))
    input.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter' }))

    expect(onSelect).toHaveBeenCalledWith('boss_jagged_peak_drake')
  })

  it('ArrowDown moves the highlight before Enter selects it', () => {
    const onSelect = vi.fn()
    const { input } = buildFilterableList(OPTIONS, () => null, onSelect)

    input.value = 'boss'
    input.dispatchEvent(new Event('input'))
    input.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowDown' }))
    input.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter' }))

    expect(onSelect).toHaveBeenCalledWith('boss_elden_beast')
  })
})
