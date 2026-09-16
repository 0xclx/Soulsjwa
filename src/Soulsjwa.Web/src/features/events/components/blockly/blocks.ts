import * as Blockly from 'blockly'
import type { GameDataPoint, PredefinedObjective } from '../../../../types'
import { COMPETITOR_COMPLETIONS_VAR_ID } from './constants'
import { SearchableDropdownField } from './SearchableDropdownField'

export interface RegisterBlocksOptions {
  /** Registers the `competitorCompletions` count block (fail-rule builder only). */
  includeCompetitorCompletions?: boolean
}

/** Call once before creating a workspace. */
export function registerJsonLogicBlocks(
  dataPoints: GameDataPoint[],
  predefinedObjectives?: PredefinedObjective[],
  options?: RegisterBlocksOptions,
) {
  if (dataPoints.length > 0) {
    Blockly.Blocks['jsonlogic_var'] = {
      init(this: Blockly.Block) {
        const options: Array<[string, string]> = dataPoints.map((dp) => [dp.displayName, dp.id])
        this.appendDummyInput().appendField(new SearchableDropdownField(options), 'VAR_ID')
        this.setOutput(true, 'Number')
        this.setColour(230)
        this.setTooltip('A game variable (e.g., boss kill count)')
      },
    }
  }

  Blockly.Blocks['jsonlogic_number'] = {
    init(this: Blockly.Block) {
      this.appendDummyInput().appendField(new Blockly.FieldNumber(0), 'NUM')
      this.setOutput(true, 'Number')
      this.setColour(160)
      this.setTooltip('A number value')
    },
  }

  Blockly.Blocks['jsonlogic_compare'] = {
    init(this: Blockly.Block) {
      this.appendValueInput('LEFT').setCheck('Number')
      this.appendDummyInput().appendField(
        new Blockly.FieldDropdown([
          ['>', '>'],
          ['<', '<'],
          ['≥', '>='],
          ['≤', '<='],
          ['=', '=='],
        ]),
        'OP',
      )
      this.appendValueInput('RIGHT').setCheck('Number')
      this.setInputsInline(true)
      this.setOutput(true, 'Boolean')
      this.setColour(120)
      this.setTooltip('Compare two values')
    },
  }

  Blockly.Blocks['jsonlogic_and'] = {
    init(this: Blockly.Block) {
      this.appendValueInput('A').setCheck('Boolean').appendField('AND')
      this.appendValueInput('B').setCheck('Boolean')
      this.setInputsInline(false)
      this.setOutput(true, 'Boolean')
      this.setColour(60)
      this.setTooltip('Both conditions must be true')
    },
  }

  Blockly.Blocks['jsonlogic_or'] = {
    init(this: Blockly.Block) {
      this.appendValueInput('A').setCheck('Boolean').appendField('OR')
      this.appendValueInput('B').setCheck('Boolean')
      this.setInputsInline(false)
      this.setOutput(true, 'Boolean')
      this.setColour(60)
      this.setTooltip('At least one condition must be true')
    },
  }

  // Predefined objective blocks — each embeds a complete JsonLogic rule
  if (predefinedObjectives && predefinedObjectives.length > 0) {
    for (const obj of predefinedObjectives) {
      const blockType = `jsonlogic_predefined_${obj.id}`
      Blockly.Blocks[blockType] = {
        init(this: Blockly.Block) {
          this.appendDummyInput().appendField(`🎯 ${obj.name}`)
          this.setOutput(true, 'Boolean')
          this.setColour(290)
          this.setTooltip(`Predefined: ${obj.name} (${obj.score} pts)`)
          this.data = obj.rule ?? ''
        },
      }
    }
  }

  // "Other competitors completed" count block — fail rules only. Compiles
  // to a `{"var": "competitorCompletions"}` JsonLogic reference.
  if (options?.includeCompetitorCompletions) {
    Blockly.Blocks['jsonlogic_competitor_completions'] = {
      init(this: Blockly.Block) {
        this.appendDummyInput().appendField('other competitors completed')
        this.setOutput(true, 'Number')
        this.setColour(210)
        this.setTooltip(
          'Number of OTHER event competitors who have already completed this objective',
        )
        this.data = COMPETITOR_COMPLETIONS_VAR_ID
      },
    }
  }
}
