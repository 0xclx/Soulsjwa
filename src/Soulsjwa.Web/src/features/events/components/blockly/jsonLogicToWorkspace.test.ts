import { describe, it, expect } from 'vitest'
import { ruleToBlockState } from './jsonLogicToWorkspace'
import { COMPETITOR_COMPLETIONS_VAR_ID } from './constants'
import type { PredefinedObjective } from '../../../../types'

const PREDEFINED: PredefinedObjective[] = [
  {
    id: 'margit',
    gameId: 9,
    name: 'Margit, the Fell Omen - Slain',
    score: 10,
    rule: '{">":[{"var":"g9_f10000850"},0]}',
  },
]

describe('ruleToBlockState', () => {
  it('converts a number literal', () => {
    expect(ruleToBlockState(5, [])).toEqual({ type: 'jsonlogic_number', fields: { NUM: 5 } })
  })

  it('converts a plain game-variable reference', () => {
    expect(ruleToBlockState({ var: 'g9_f123' }, [])).toEqual({
      type: 'jsonlogic_var',
      fields: { VAR_ID: 'g9_f123' },
    })
  })

  it('converts the competitorCompletions variable to its dedicated block', () => {
    expect(ruleToBlockState({ var: COMPETITOR_COMPLETIONS_VAR_ID }, [])).toEqual({
      type: 'jsonlogic_competitor_completions',
    })
  })

  it('converts a comparison with nested operands', () => {
    const rule = { '>': [{ var: 'g9_f123' }, 0] }
    expect(ruleToBlockState(rule, [])).toEqual({
      type: 'jsonlogic_compare',
      fields: { OP: '>' },
      inputs: {
        LEFT: { block: { type: 'jsonlogic_var', fields: { VAR_ID: 'g9_f123' } } },
        RIGHT: { block: { type: 'jsonlogic_number', fields: { NUM: 0 } } },
      },
    })
  })

  it('converts a binary AND', () => {
    const rule = {
      and: [{ '>': [{ var: 'a' }, 0] }, { '<': [{ var: 'b' }, 10] }],
    }
    const state = ruleToBlockState(rule, [])
    expect(state?.type).toBe('jsonlogic_and')
    expect(state?.inputs?.A?.block.type).toBe('jsonlogic_compare')
    expect(state?.inputs?.B?.block.type).toBe('jsonlogic_compare')
  })

  it('right-folds an n-ary AND into nested binary blocks', () => {
    const rule = {
      and: [{ '>': [{ var: 'a' }, 0] }, { '>': [{ var: 'b' }, 0] }, { '>': [{ var: 'c' }, 0] }],
    }
    const state = ruleToBlockState(rule, [])
    // Top-level AND: A = first compare, B = nested AND of the remaining two.
    expect(state).toEqual({
      type: 'jsonlogic_and',
      inputs: {
        A: {
          block: {
            type: 'jsonlogic_compare',
            fields: { OP: '>' },
            inputs: {
              LEFT: { block: { type: 'jsonlogic_var', fields: { VAR_ID: 'a' } } },
              RIGHT: { block: { type: 'jsonlogic_number', fields: { NUM: 0 } } },
            },
          },
        },
        B: {
          block: {
            type: 'jsonlogic_and',
            inputs: {
              A: {
                block: {
                  type: 'jsonlogic_compare',
                  fields: { OP: '>' },
                  inputs: {
                    LEFT: { block: { type: 'jsonlogic_var', fields: { VAR_ID: 'b' } } },
                    RIGHT: { block: { type: 'jsonlogic_number', fields: { NUM: 0 } } },
                  },
                },
              },
              B: {
                block: {
                  type: 'jsonlogic_compare',
                  fields: { OP: '>' },
                  inputs: {
                    LEFT: { block: { type: 'jsonlogic_var', fields: { VAR_ID: 'c' } } },
                    RIGHT: { block: { type: 'jsonlogic_number', fields: { NUM: 0 } } },
                  },
                },
              },
            },
          },
        },
      },
    })
  })

  it('matches a rule identical to a predefined objective and emits its dedicated block', () => {
    const rule = JSON.parse(PREDEFINED[0]!.rule!)
    expect(ruleToBlockState(rule, PREDEFINED)).toEqual({
      type: 'jsonlogic_predefined_margit',
    })
  })

  it('returns null for a rule shape the builder cannot produce', () => {
    expect(ruleToBlockState({ unknownOp: [1, 2] }, [])).toBeNull()
    expect(ruleToBlockState({ var: 'a', extra: 1 }, [])).toBeNull()
  })

  it('returns null for malformed comparison operands', () => {
    expect(ruleToBlockState({ '>': [{ var: 'a' }] }, [])).toBeNull()
  })
})
