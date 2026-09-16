import * as Blockly from 'blockly'
import { COMPETITOR_COMPLETIONS_VAR_ID } from './constants'
import type { PredefinedObjective } from '../../../../types'

/**
 * Minimal shape of Blockly's block serialization state — just enough of
 * `Blockly.serialization.blocks.State` to build blocks programmatically.
 * Defined locally (rather than imported from Blockly's internal
 * `serialization/blocks` module) since that subpath isn't part of the
 * package's public `exports` map.
 */
interface BlockState {
  type: string
  fields?: Record<string, unknown>
  inputs?: Record<string, { block: BlockState }>
}

const COMPARISON_OPERATORS = new Set(['>', '<', '>=', '<=', '=='])

function deepEqual(a: unknown, b: unknown): boolean {
  if (a === b) return true
  if (typeof a !== typeof b) return false
  if (Array.isArray(a) || Array.isArray(b)) {
    if (!Array.isArray(a) || !Array.isArray(b) || a.length !== b.length) return false
    return a.every((item, i) => deepEqual(item, b[i]))
  }
  if (a && b && typeof a === 'object' && typeof b === 'object') {
    const aKeys = Object.keys(a)
    const bKeys = Object.keys(b)
    if (aKeys.length !== bKeys.length) return false
    return aKeys.every((key) =>
      deepEqual((a as Record<string, unknown>)[key], (b as Record<string, unknown>)[key]),
    )
  }
  return false
}

function findMatchingPredefinedObjective(
  rule: unknown,
  predefinedObjectives: PredefinedObjective[],
): PredefinedObjective | undefined {
  return predefinedObjectives.find((obj) => {
    if (!obj.rule) return false
    try {
      return deepEqual(JSON.parse(obj.rule), rule)
    } catch {
      return false
    }
  })
}

/**
 * Converts a JsonLogic rule back into Blockly block state — the reverse of
 * `workspaceToJsonLogic`. Used to seed the visual builder from a rule that
 * already exists (e.g. when editing an objective), instead of forcing the
 * user to rebuild it from scratch or fall back to hand-editing JSON.
 *
 * Returns null for rule shapes it doesn't recognize (fields the builder
 * itself can't produce, e.g. hand-written JsonLogic) rather than guessing —
 * callers should fall back to a raw JSON view in that case.
 */
export function ruleToBlockState(
  rule: unknown,
  predefinedObjectives: PredefinedObjective[],
): BlockState | null {
  if (typeof rule === 'number') {
    return { type: 'jsonlogic_number', fields: { NUM: rule } }
  }

  if (typeof rule !== 'object' || rule === null) return null

  const predefined = findMatchingPredefinedObjective(rule, predefinedObjectives)
  if (predefined) {
    return { type: `jsonlogic_predefined_${predefined.id}` }
  }

  const keys = Object.keys(rule as Record<string, unknown>)
  if (keys.length !== 1) return null
  const key = keys[0]!
  const value = (rule as Record<string, unknown>)[key]

  if (key === 'var') {
    if (value === COMPETITOR_COMPLETIONS_VAR_ID) {
      return { type: 'jsonlogic_competitor_completions' }
    }
    if (typeof value !== 'string') return null
    return { type: 'jsonlogic_var', fields: { VAR_ID: value } }
  }

  if (COMPARISON_OPERATORS.has(key)) {
    if (!Array.isArray(value) || value.length !== 2) return null
    const left = ruleToBlockState(value[0], predefinedObjectives)
    const right = ruleToBlockState(value[1], predefinedObjectives)
    if (!left || !right) return null
    return {
      type: 'jsonlogic_compare',
      fields: { OP: key },
      inputs: { LEFT: { block: left }, RIGHT: { block: right } },
    }
  }

  if (key === 'and' || key === 'or') {
    if (!Array.isArray(value) || value.length < 2) return null
    const blockType = key === 'and' ? 'jsonlogic_and' : 'jsonlogic_or'
    // The AND/OR blocks are strictly binary (A, B inputs) — right-fold an
    // n-ary JsonLogic array into nested binary blocks.
    const build = (index: number): BlockState | null => {
      const a = ruleToBlockState(value[index], predefinedObjectives)
      if (!a) return null
      if (index === value.length - 2) {
        const b = ruleToBlockState(value[index + 1], predefinedObjectives)
        if (!b) return null
        return { type: blockType, inputs: { A: { block: a }, B: { block: b } } }
      }
      const b = build(index + 1)
      if (!b) return null
      return { type: blockType, inputs: { A: { block: a }, B: { block: b } } }
    }
    return build(0)
  }

  return null
}

/**
 * Parses `ruleJson` and loads it into `workspace` as blocks, if recognized.
 * No-ops (leaving the canvas empty) when the rule is missing, malformed, or
 * shaped in a way the builder can't represent — the caller's raw-JSON
 * fallback view covers that case instead of silently losing the rule.
 */
export function loadRuleIntoWorkspace(
  workspace: Blockly.WorkspaceSvg,
  ruleJson: string | undefined,
  predefinedObjectives: PredefinedObjective[],
): boolean {
  if (!ruleJson) return false

  let rule: unknown
  try {
    rule = JSON.parse(ruleJson)
  } catch {
    return false
  }

  const state = ruleToBlockState(rule, predefinedObjectives)
  if (!state) return false

  try {
    Blockly.serialization.blocks.append(state, workspace)
    return true
  } catch {
    return false
  }
}
