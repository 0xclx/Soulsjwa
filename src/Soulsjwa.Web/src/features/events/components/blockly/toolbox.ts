import type { GameDataPoint, PredefinedObjective } from '../../../../types'
import { PREDEFINED_OBJECTIVES_CATEGORY_KEY } from './constants'

interface ToolboxBlockInfo {
  kind: 'block'
  type: string
}

interface ToolboxCategoryInfo {
  kind: 'category'
  name: string
  colour: string
  contents?: ToolboxBlockInfo[]
  /** Registers this category as dynamic (see registerToolboxCategoryCallback) instead of a static block list. */
  custom?: string
}

interface ToolboxInfo {
  kind: 'categoryToolbox'
  contents: ToolboxCategoryInfo[]
}

export interface BuildToolboxOptions {
  /** Includes the "other competitors completed" count block (fail-rule builder only). */
  includeCompetitorCompletions?: boolean
}

/**
 * The game variable block is only included when data points are available;
 * predefined objectives come in as ready-to-use building blocks.
 */
export function buildToolbox(
  dataPoints: GameDataPoint[],
  predefinedObjectives?: PredefinedObjective[],
  options?: BuildToolboxOptions,
): ToolboxInfo {
  const contents: ToolboxCategoryInfo[] = []

  if (predefinedObjectives && predefinedObjectives.length > 0) {
    contents.push({
      kind: 'category',
      name: 'Predefined Objectives',
      colour: '290',
      custom: PREDEFINED_OBJECTIVES_CATEGORY_KEY,
    })
  }

  if (dataPoints.length > 0) {
    contents.push({
      kind: 'category',
      name: 'Game Variables',
      colour: '230',
      contents: [{ kind: 'block', type: 'jsonlogic_var' }],
    })
  }

  contents.push({
    kind: 'category',
    name: 'Values',
    colour: '160',
    contents: [{ kind: 'block', type: 'jsonlogic_number' }],
  })

  contents.push({
    kind: 'category',
    name: 'Comparisons',
    colour: '120',
    contents: [{ kind: 'block', type: 'jsonlogic_compare' }],
  })

  contents.push({
    kind: 'category',
    name: 'Logic',
    colour: '60',
    contents: [
      { kind: 'block', type: 'jsonlogic_and' },
      { kind: 'block', type: 'jsonlogic_or' },
    ],
  })

  if (options?.includeCompetitorCompletions) {
    contents.push({
      kind: 'category',
      name: 'Competitor Progress',
      colour: '210',
      contents: [{ kind: 'block', type: 'jsonlogic_competitor_completions' }],
    })
  }

  return { kind: 'categoryToolbox', contents }
}
