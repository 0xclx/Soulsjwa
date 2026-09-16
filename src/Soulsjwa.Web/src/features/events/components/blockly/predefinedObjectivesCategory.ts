import type * as Blockly from 'blockly'
import type { PredefinedObjective } from '../../../../types'
import { PREDEFINED_OBJECTIVES_CATEGORY_KEY } from './constants'

/** Fallback group label for objectives that have no category set. */
const UNCATEGORIZED_LABEL = 'Other'

/**
 * Registers the "Predefined Objectives" toolbox category as a dynamic
 * category so its flyout contents can be filtered by name/category without
 * reinjecting the workspace, and grouped by category (with a label heading
 * per group, same grouping as the scoreboard and OBS overlay) instead of one
 * long flat stack. Some games have hundreds of predefined objectives, which
 * otherwise renders as an unfiltered, ungrouped list in the flyout.
 *
 * `getFilterText` is read fresh each time the category's flyout opens or
 * `workspace.getToolbox()?.refreshSelection()` is called, so callers can
 * keep it backed by a ref updated from a search input without re-running
 * this registration on every keystroke.
 */
export function registerPredefinedObjectivesCategory(
  workspace: Blockly.WorkspaceSvg,
  predefinedObjectives: PredefinedObjective[],
  getFilterText: () => string,
): void {
  workspace.registerToolboxCategoryCallback(PREDEFINED_OBJECTIVES_CATEGORY_KEY, () => {
    const needle = getFilterText().trim().toLowerCase()
    const filtered = needle
      ? predefinedObjectives.filter(
          (obj) =>
            obj.name.toLowerCase().includes(needle) ||
            (obj.category ?? '').toLowerCase().includes(needle),
        )
      : predefinedObjectives

    if (filtered.length === 0) {
      return [{ kind: 'label', text: 'No matching objectives' }]
    }

    const byCategory = new Map<string, PredefinedObjective[]>()
    for (const obj of filtered) {
      const category = obj.category?.trim() || UNCATEGORIZED_LABEL
      const bucket = byCategory.get(category)
      if (bucket) bucket.push(obj)
      else byCategory.set(category, [obj])
    }

    const showCategoryLabels = byCategory.size > 1
    const contents: Array<{ kind: string; text?: string; type?: string }> = []
    for (const [category, objectives] of byCategory) {
      if (showCategoryLabels) contents.push({ kind: 'label', text: category })
      for (const obj of objectives) {
        contents.push({ kind: 'block', type: `jsonlogic_predefined_${obj.id}` })
      }
    }
    return contents
  })
}
