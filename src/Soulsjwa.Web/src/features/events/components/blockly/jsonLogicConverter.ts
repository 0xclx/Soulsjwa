import type { Block, Workspace } from 'blockly'
import { COMPETITOR_COMPLETIONS_VAR_ID } from './constants'

type JsonLogicRule = Record<string, unknown> | null

/** The rule from the top-level output block, or null if the workspace is empty. */
export function workspaceToJsonLogic(workspace: Workspace): JsonLogicRule {
  const topBlocks = workspace.getTopBlocks(false)
  if (topBlocks.length !== 1) return null

  const rootBlock = topBlocks[0]
  if (!rootBlock || !rootBlock.outputConnection?.getCheck()?.includes('Boolean')) return null

  return blockToJsonLogic(rootBlock)
}

function blockToJsonLogic(block: Block): JsonLogicRule {
  switch (block.type) {
    case 'jsonlogic_var': {
      const varId = block.getFieldValue('VAR_ID') as string
      return { var: varId }
    }

    case 'jsonlogic_number': {
      const num = block.getFieldValue('NUM') as number
      return num as unknown as JsonLogicRule
    }

    case 'jsonlogic_competitor_completions': {
      return { var: COMPETITOR_COMPLETIONS_VAR_ID }
    }

    case 'jsonlogic_compare': {
      const op = block.getFieldValue('OP') as string
      const left = getInputValue(block, 'LEFT')
      const right = getInputValue(block, 'RIGHT')
      if (left === null || right === null) return null
      return { [op]: [left, right] }
    }

    case 'jsonlogic_and': {
      const a = getInputValue(block, 'A')
      const b = getInputValue(block, 'B')
      if (a === null || b === null) return null
      return { and: [a, b] }
    }

    case 'jsonlogic_or': {
      const a = getInputValue(block, 'A')
      const b = getInputValue(block, 'B')
      if (a === null || b === null) return null
      return { or: [a, b] }
    }

    default:
      if (block.type.startsWith('jsonlogic_predefined_')) {
        const ruleJson = block.data as string | undefined
        if (!ruleJson) return null
        try {
          return JSON.parse(ruleJson) as JsonLogicRule
        } catch {
          return null
        }
      }
      return null
  }
}

function getInputValue(block: Block, inputName: string): unknown {
  const input = block.getInput(inputName)
  if (!input?.connection?.targetBlock()) return null
  const targetBlock = input.connection.targetBlock()!
  const result = blockToJsonLogic(targetBlock)
  // For number blocks, the result is the number itself (not wrapped in an object)
  return result
}
