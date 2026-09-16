import * as Blockly from 'blockly'

export interface FilterableListHandle {
  container: HTMLDivElement
  input: HTMLInputElement
  focus: () => void
}

/**
 * Builds the filter input + option list DOM used by the dropdown editor.
 * Kept independent of Blockly's DropDownDiv/workspace so it's unit-testable
 * without spinning up a full rendered Blockly workspace.
 */
export function buildFilterableList(
  options: Array<[string, string]>,
  getValue: () => string | null,
  onSelect: (value: string) => void,
): FilterableListHandle {
  let highlightedIndex = -1

  const container = document.createElement('div')
  container.className = 'blocklySearchableDropdown'

  const input = document.createElement('input')
  input.type = 'text'
  input.placeholder = 'Filter…'
  input.className = 'blocklySearchableDropdownFilter'
  input.setAttribute('aria-label', 'Filter options')
  container.appendChild(input)

  const list = document.createElement('div')
  list.className = 'blocklySearchableDropdownList'
  container.appendChild(list)

  const renderList = (filterText: string) => {
    list.replaceChildren()
    const needle = filterText.trim().toLowerCase()
    const filtered = needle
      ? options.filter(([label]) => label.toLowerCase().includes(needle))
      : options

    highlightedIndex = filtered.length > 0 ? 0 : -1

    if (filtered.length === 0) {
      const empty = document.createElement('div')
      empty.className = 'blocklySearchableDropdownEmpty'
      empty.textContent = 'No matches'
      list.appendChild(empty)
      return
    }

    filtered.forEach(([label, value], index) => {
      const item = document.createElement('div')
      item.className = 'blocklySearchableDropdownItem'
      if (value === getValue()) item.classList.add('isSelected')
      if (index === highlightedIndex) item.classList.add('isHighlighted')
      item.textContent = label
      item.dataset.value = value
      item.addEventListener('mousedown', (e) => e.preventDefault())
      item.addEventListener('click', () => onSelect(value))
      list.appendChild(item)
    })
  }

  const highlight = (items: HTMLDivElement[]) => {
    items.forEach((el, i) => el.classList.toggle('isHighlighted', i === highlightedIndex))
    items[highlightedIndex]?.scrollIntoView?.({ block: 'nearest' })
  }

  renderList('')
  input.addEventListener('input', () => renderList(input.value))
  input.addEventListener('keydown', (e) => {
    const items = Array.from(
      list.querySelectorAll<HTMLDivElement>('.blocklySearchableDropdownItem'),
    )
    if (items.length === 0) return
    if (e.key === 'ArrowDown') {
      e.preventDefault()
      highlightedIndex = Math.min(highlightedIndex + 1, items.length - 1)
      highlight(items)
    } else if (e.key === 'ArrowUp') {
      e.preventDefault()
      highlightedIndex = Math.max(highlightedIndex - 1, 0)
      highlight(items)
    } else if (e.key === 'Enter') {
      e.preventDefault()
      items[highlightedIndex]?.click()
    }
  })

  return { container, input, focus: () => input.focus() }
}

/**
 * A dropdown field with a text filter above the option list. Several games
 * expose 50-150+ data points (one per boss/area), and Blockly's stock
 * dropdown renders them as a single unfiltered scrolling list, so finding
 * e.g. "death count" means scrolling through the whole thing by eye.
 */
export class SearchableDropdownField extends Blockly.FieldDropdown {
  protected override showEditor_(): void {
    const options = this.getOptions(false).filter(
      (opt): opt is [string, string] => opt !== 'separator' && typeof opt[0] === 'string',
    )

    const { container, input } = buildFilterableList(
      options,
      () => this.getValue(),
      (value) => {
        this.setValue(value)
        Blockly.DropDownDiv.hideWithoutAnimation()
      },
    )

    input.addEventListener('keydown', (e) => {
      if (e.key === 'Escape') {
        e.preventDefault()
        Blockly.DropDownDiv.hideWithoutAnimation()
      }
    })

    Blockly.DropDownDiv.getContentDiv().appendChild(container)
    Blockly.DropDownDiv.showPositionedByField(this, this.dropdownDispose_.bind(this))

    // Focus once the div is positioned so typing can start immediately.
    requestAnimationFrame(() => input.focus())
  }
}
