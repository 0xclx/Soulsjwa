import Box from '@mui/material/Box'
import Checkbox from '@mui/material/Checkbox'
import FormControlLabel from '@mui/material/FormControlLabel'
import { RuleBuilderWrapper } from './blockly/RuleBuilderWrapper'
import { Spinner } from '../../../components/ui'
import type { GameDataPoint, PredefinedObjective } from '../../../types'

interface ObjectiveRuleSectionProps {
  label: string
  checked: boolean
  onCheckedChange: (checked: boolean) => void
  loading: boolean
  dataPoints: GameDataPoint[]
  predefinedObjectives?: PredefinedObjective[]
  /** Permits the `competitorCompletions` count block — fail-rule builders only. */
  includeCompetitorCompletions?: boolean
  /** Seeds the canvas from an existing rule (edit flows) — read once, on mount, only. */
  initialRule?: string
  rule: string | undefined
  onRuleChange: (rule: string | undefined) => void
}

/**
 * A single "enable this rule" checkbox plus its Blockly rule builder and a
 * read-only JSON preview. Shared between the completion-rule and fail-rule
 * sections of {@link CreateObjectiveDialog} so both stay visually and
 * behaviourally identical apart from which building blocks they permit.
 */
export const ObjectiveRuleSection = ({
  label,
  checked,
  onCheckedChange,
  loading,
  dataPoints,
  predefinedObjectives,
  includeCompetitorCompletions,
  initialRule,
  rule,
  onRuleChange,
}: ObjectiveRuleSectionProps) => (
  <Box>
    <FormControlLabel
      control={<Checkbox checked={checked} onChange={(e) => onCheckedChange(e.target.checked)} />}
      label={label}
    />

    {checked && (
      <Box sx={{ mt: 1 }}>
        {loading ? (
          <Box sx={{ display: 'flex', justifyContent: 'center', py: 3 }}>
            <Spinner size={24} />
          </Box>
        ) : (
          <RuleBuilderWrapper
            dataPoints={dataPoints}
            predefinedObjectives={predefinedObjectives}
            includeCompetitorCompletions={includeCompetitorCompletions}
            initialRule={initialRule}
            onChange={onRuleChange}
          />
        )}
        {rule && (
          <Box
            component="pre"
            sx={{
              mt: 1,
              p: 1,
              bgcolor: '#1f2937',
              color: '#d1d5db',
              borderRadius: 1,
              typography: 'caption',
              overflow: 'auto',
              maxHeight: 120,
            }}
          >
            {JSON.stringify(JSON.parse(rule), null, 2)}
          </Box>
        )}
      </Box>
    )}
  </Box>
)
