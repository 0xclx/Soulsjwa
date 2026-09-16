import { describe, it, expect, vi } from 'vitest'
import { workspaceToJsonLogic } from './jsonLogicConverter'
import { COMPETITOR_COMPLETIONS_VAR_ID } from './constants'
import type { Block, Workspace } from 'blockly'

// We don't need a real Blockly Workspace — only the methods/properties accessed by
// jsonLogicConverter. Build minimal stand-ins.
type FakeBlock = {
  type: string
  outputConnection: object | null
  outputType?: 'Boolean' | 'Number'
  fields?: Record<string, string | number>
  inputs?: Record<string, FakeBlock | null>
  data?: string
}

function makeBlock(b: FakeBlock): Block {
  return {
    type: b.type,
    outputConnection:
      b.outputConnection === null
        ? null
        : { getCheck: () => (b.outputType ? [b.outputType] : null) },
    data: b.data,
    getFieldValue: vi.fn((name: string) => b.fields?.[name] ?? null),
    getInput: vi.fn((name: string) => {
      const target = b.inputs?.[name]
      if (!target) return null
      return {
        connection: {
          targetBlock: () => makeBlock(target),
        },
      }
    }),
  } as unknown as Block
}

function makeWorkspace(top: FakeBlock | FakeBlock[] | null): Workspace {
  return {
    getTopBlocks: () => (top ? (Array.isArray(top) ? top.map(makeBlock) : [makeBlock(top)]) : []),
  } as unknown as Workspace
}

describe('workspaceToJsonLogic', () => {
  it('returns null for an empty workspace', () => {
    expect(workspaceToJsonLogic(makeWorkspace(null))).toBeNull()
  })

  it('returns null when top block is not a boolean rule', () => {
    const ws = makeWorkspace({
      type: 'jsonlogic_var',
      outputConnection: {},
      outputType: 'Number',
      fields: { VAR_ID: 'x' },
    })
    expect(workspaceToJsonLogic(ws)).toBeNull()
  })

  it('converts a compare block with both children connected', () => {
    const ws = makeWorkspace({
      type: 'jsonlogic_compare',
      outputConnection: {},
      outputType: 'Boolean',
      fields: { OP: '>' },
      inputs: {
        LEFT: { type: 'jsonlogic_var', outputConnection: {}, fields: { VAR_ID: 'hp' } },
        RIGHT: { type: 'jsonlogic_number', outputConnection: {}, fields: { NUM: 0 } },
      },
    })
    expect(workspaceToJsonLogic(ws)).toEqual({ '>': [{ var: 'hp' }, 0] })
  })

  it('returns null for a compare block missing one operand', () => {
    const ws = makeWorkspace({
      type: 'jsonlogic_compare',
      outputConnection: {},
      outputType: 'Boolean',
      fields: { OP: '==' },
      inputs: {
        LEFT: { type: 'jsonlogic_var', outputConnection: {}, fields: { VAR_ID: 'hp' } },
        RIGHT: null,
      },
    })
    expect(workspaceToJsonLogic(ws)).toBeNull()
  })

  it('converts a logical and/or with two operands', () => {
    const ws = makeWorkspace({
      type: 'jsonlogic_and',
      outputConnection: {},
      outputType: 'Boolean',
      inputs: {
        A: {
          type: 'jsonlogic_compare',
          outputConnection: {},
          fields: { OP: '>' },
          inputs: {
            LEFT: { type: 'jsonlogic_var', outputConnection: {}, fields: { VAR_ID: 'a' } },
            RIGHT: { type: 'jsonlogic_number', outputConnection: {}, fields: { NUM: 1 } },
          },
        },
        B: {
          type: 'jsonlogic_compare',
          outputConnection: {},
          fields: { OP: '<' },
          inputs: {
            LEFT: { type: 'jsonlogic_var', outputConnection: {}, fields: { VAR_ID: 'b' } },
            RIGHT: { type: 'jsonlogic_number', outputConnection: {}, fields: { NUM: 5 } },
          },
        },
      },
    })
    expect(workspaceToJsonLogic(ws)).toEqual({
      and: [{ '>': [{ var: 'a' }, 1] }, { '<': [{ var: 'b' }, 5] }],
    })
  })

  it('returns null for an unknown block type', () => {
    const ws = makeWorkspace({
      type: 'unknown_type',
      outputConnection: {},
      outputType: 'Boolean',
    })
    expect(workspaceToJsonLogic(ws)).toBeNull()
  })

  it('converts a predefined objective block to its embedded JsonLogic rule', () => {
    const rule = JSON.stringify({ '>': [{ var: '117' }, 0] })
    const ws = makeWorkspace({
      type: 'jsonlogic_predefined_abc-123',
      outputConnection: {},
      outputType: 'Boolean',
      data: rule,
    })
    expect(workspaceToJsonLogic(ws)).toEqual({ '>': [{ var: '117' }, 0] })
  })

  it('returns null for a predefined block with empty rule data', () => {
    const ws = makeWorkspace({
      type: 'jsonlogic_predefined_abc-123',
      outputConnection: {},
      outputType: 'Boolean',
      data: '',
    })
    expect(workspaceToJsonLogic(ws)).toBeNull()
  })

  it('converts a compound rule using predefined blocks with AND', () => {
    const rule1 = JSON.stringify({ '>': [{ var: '117' }, 0] })
    const rule2 = JSON.stringify({ '<': [{ var: 'time' }, 30] })
    const ws = makeWorkspace({
      type: 'jsonlogic_and',
      outputConnection: {},
      outputType: 'Boolean',
      inputs: {
        A: {
          type: 'jsonlogic_predefined_boss1',
          outputConnection: {},
          data: rule1,
        },
        B: {
          type: 'jsonlogic_predefined_time1',
          outputConnection: {},
          data: rule2,
        },
      },
    })
    expect(workspaceToJsonLogic(ws)).toEqual({
      and: [{ '>': [{ var: '117' }, 0] }, { '<': [{ var: 'time' }, 30] }],
    })
  })

  it('returns null when multiple disconnected boolean rules exist', () => {
    const ws = makeWorkspace([
      {
        type: 'jsonlogic_predefined_boss1',
        outputConnection: {},
        outputType: 'Boolean',
        data: JSON.stringify({ '>': [{ var: '117' }, 0] }),
      },
      {
        type: 'jsonlogic_predefined_boss2',
        outputConnection: {},
        outputType: 'Boolean',
        data: JSON.stringify({ '>': [{ var: '118' }, 0] }),
      },
    ])

    expect(workspaceToJsonLogic(ws)).toBeNull()
  })

  it('converts a competitor-completions block used alone as a Number (no boolean root)', () => {
    const ws = makeWorkspace({
      type: 'jsonlogic_competitor_completions',
      outputConnection: {},
      outputType: 'Number',
    })
    expect(workspaceToJsonLogic(ws)).toBeNull()
  })

  it('converts a fail rule combining competitor-completions with a comparison', () => {
    const ws = makeWorkspace({
      type: 'jsonlogic_compare',
      outputConnection: {},
      outputType: 'Boolean',
      fields: { OP: '>=' },
      inputs: {
        LEFT: { type: 'jsonlogic_competitor_completions', outputConnection: {} },
        RIGHT: { type: 'jsonlogic_number', outputConnection: {}, fields: { NUM: 2 } },
      },
    })
    expect(workspaceToJsonLogic(ws)).toEqual({
      '>=': [{ var: COMPETITOR_COMPLETIONS_VAR_ID }, 2],
    })
  })

  it('converts a fail rule combining a game variable AND competitor-completions', () => {
    const ws = makeWorkspace({
      type: 'jsonlogic_and',
      outputConnection: {},
      outputType: 'Boolean',
      inputs: {
        A: {
          type: 'jsonlogic_compare',
          outputConnection: {},
          fields: { OP: '>' },
          inputs: {
            LEFT: { type: 'jsonlogic_var', outputConnection: {}, fields: { VAR_ID: 'deaths' } },
            RIGHT: { type: 'jsonlogic_number', outputConnection: {}, fields: { NUM: 3 } },
          },
        },
        B: {
          type: 'jsonlogic_compare',
          outputConnection: {},
          fields: { OP: '>=' },
          inputs: {
            LEFT: { type: 'jsonlogic_competitor_completions', outputConnection: {} },
            RIGHT: { type: 'jsonlogic_number', outputConnection: {}, fields: { NUM: 1 } },
          },
        },
      },
    })
    expect(workspaceToJsonLogic(ws)).toEqual({
      and: [{ '>': [{ var: 'deaths' }, 3] }, { '>=': [{ var: COMPETITOR_COMPLETIONS_VAR_ID }, 1] }],
    })
  })
})
