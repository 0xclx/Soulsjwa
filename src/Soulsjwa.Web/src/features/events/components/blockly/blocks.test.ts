import { describe, it, expect, beforeEach } from 'vitest'
import * as Blockly from 'blockly'
import { registerJsonLogicBlocks } from './blocks'

describe('registerJsonLogicBlocks', () => {
  beforeEach(() => {
    delete Blockly.Blocks['jsonlogic_competitor_completions']
  })

  it('does not register the competitor-completions block by default', () => {
    registerJsonLogicBlocks([], [])
    expect(Blockly.Blocks['jsonlogic_competitor_completions']).toBeUndefined()
  })

  it('registers the competitor-completions block when requested', () => {
    registerJsonLogicBlocks([], [], { includeCompetitorCompletions: true })
    expect(Blockly.Blocks['jsonlogic_competitor_completions']).toBeDefined()
  })
})
