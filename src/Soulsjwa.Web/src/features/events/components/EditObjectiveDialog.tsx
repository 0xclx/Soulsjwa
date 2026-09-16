import { useCallback, useState } from 'react'
import Alert from '@mui/material/Alert'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Dialog from '@mui/material/Dialog'
import DialogActions from '@mui/material/DialogActions'
import DialogContent from '@mui/material/DialogContent'
import DialogTitle from '@mui/material/DialogTitle'
import Stack from '@mui/material/Stack'
import TextField from '@mui/material/TextField'
import { ObjectiveRuleSection } from './ObjectiveRuleSection'
import { useGameDataDefinitions } from '../hooks/useGameDataDefinitions'
import { usePredefinedObjectives } from '../hooks/usePredefinedObjectives'
import { getErrorDetail } from '../../../lib/getErrorDetail'
import type { SubmitMutation } from '../eventDetail/mutation'
import type { EventGame } from '../../../types'

export interface EditObjectiveTarget {
  eventGameId: string
  objectiveId: string
  name: string
  score: string
  category: string
  rule: string
  failRule: string
}

type EditObjectiveVars = {
  eventGameId: string
  objectiveId: string
  payload: {
    name?: string
    score?: number
    category?: string | null
    rule?: string | null
    failRule?: string | null
  }
}

interface EditObjectiveDialogProps {
  open: boolean
  target: EditObjectiveTarget
  /** The objective's game — enables the Blockly rule builder for connector-supported games. */
  game?: EventGame
  mutation: SubmitMutation<EditObjectiveVars>
  onClose: () => void
}

/**
 * Owner dialog for editing an objective's name, score, category, and
 * auto-completion / fail rules. For connector-supported games the rules are
 * edited with the same Blockly builder as objective creation (seeded from
 * the existing rule) rather than a raw JsonLogic textbox.
 */
export const EditObjectiveDialog = ({
  open,
  target,
  game,
  mutation,
  onClose,
}: EditObjectiveDialogProps) => {
  const [name, setName] = useState(target.name)
  const [score, setScore] = useState(target.score)
  const [category, setCategory] = useState(target.category)
  const [rule, setRule] = useState<string | undefined>(target.rule || undefined)
  const [failRule, setFailRule] = useState<string | undefined>(target.failRule || undefined)
  const [showRuleBuilder, setShowRuleBuilder] = useState(!!target.rule)
  const [showFailRuleBuilder, setShowFailRuleBuilder] = useState(!!target.failRule)
  const [error, setError] = useState<string | null>(null)

  const connectorSupported = game?.connectorSupported ?? false
  const needsDataPoints = connectorSupported && (showRuleBuilder || showFailRuleBuilder)
  const { data: dataPoints, isLoading: loadingDataPoints } = useGameDataDefinitions(
    needsDataPoints && game?.knownGameId ? game.knownGameId : undefined,
  )
  const { data: predefinedObjectives, isLoading: loadingPredefinedObjectives } =
    usePredefinedObjectives(
      game?.knownGameId ?? 0,
      connectorSupported && (showRuleBuilder || showFailRuleBuilder) && game?.knownGameId != null,
    )

  const handleRuleChange = useCallback((newRule: string | undefined) => setRule(newRule), [])
  const handleFailRuleChange = useCallback(
    (newRule: string | undefined) => setFailRule(newRule),
    [],
  )

  const handleSave = () => {
    const parsedScore = Number(score)
    if (!name.trim()) {
      setError('Name is required.')
      return
    }
    if (!Number.isInteger(parsedScore) || parsedScore < 0) {
      setError('Score must be a non-negative whole number.')
      return
    }

    setError(null)
    // Connector-supported games gate the rule/fail-rule on the Blockly
    // builder's "enabled" checkbox; other games' plain textboxes are the
    // rule, full stop — there's no separate enable toggle for them.
    const finalRule = connectorSupported
      ? showRuleBuilder
        ? (rule ?? null)
        : null
      : rule?.trim() || null
    const finalFailRule = connectorSupported
      ? showFailRuleBuilder
        ? (failRule ?? null)
        : null
      : failRule?.trim() || null

    mutation.mutate(
      {
        eventGameId: target.eventGameId,
        objectiveId: target.objectiveId,
        payload: {
          name: name.trim(),
          score: parsedScore,
          category: category.trim() || null,
          rule: finalRule,
          failRule: finalFailRule,
        },
      },
      {
        onSuccess: onClose,
        onError: (err) => setError(getErrorDetail(err, 'Failed to edit objective.')),
      },
    )
  }

  const wide = connectorSupported && (showRuleBuilder || showFailRuleBuilder)

  return (
    <Dialog open={open} onClose={onClose} fullWidth maxWidth={wide ? 'md' : 'sm'}>
      <DialogTitle>Edit objective</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ mt: 1 }}>
          <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1.5}>
            <TextField
              label="Name"
              value={name}
              onChange={(e) => setName(e.target.value)}
              slotProps={{ htmlInput: { maxLength: 200 } }}
              required
              sx={{ flex: 1 }}
            />
            <TextField
              label="Score"
              type="number"
              value={score}
              onChange={(e) => setScore(e.target.value)}
              slotProps={{ htmlInput: { min: 0, step: 1 } }}
              required
              sx={{ width: { xs: '100%', sm: 140 } }}
            />
          </Stack>
          <TextField
            label="Category"
            value={category}
            onChange={(e) => setCategory(e.target.value)}
            helperText="Optional grouping label (e.g. an in-game area) shown wherever objectives are grouped."
            slotProps={{ htmlInput: { maxLength: 200 } }}
            fullWidth
          />

          {connectorSupported ? (
            <>
              <ObjectiveRuleSection
                label="Define auto-completion rule"
                checked={showRuleBuilder}
                onCheckedChange={setShowRuleBuilder}
                loading={loadingDataPoints || loadingPredefinedObjectives}
                dataPoints={dataPoints ?? []}
                predefinedObjectives={predefinedObjectives}
                initialRule={target.rule || undefined}
                rule={rule}
                onRuleChange={handleRuleChange}
              />

              <ObjectiveRuleSection
                label="Define fail condition"
                checked={showFailRuleBuilder}
                onCheckedChange={setShowFailRuleBuilder}
                loading={loadingDataPoints || loadingPredefinedObjectives}
                dataPoints={dataPoints ?? []}
                predefinedObjectives={predefinedObjectives}
                includeCompetitorCompletions
                initialRule={target.failRule || undefined}
                rule={failRule}
                onRuleChange={handleFailRuleChange}
              />
            </>
          ) : (
            <Box>
              <TextField
                label="JsonLogic rule"
                value={rule ?? ''}
                onChange={(e) => setRule(e.target.value || undefined)}
                helperText="Optional. Leave empty for manual-only completion."
                multiline
                minRows={3}
                fullWidth
              />
              <TextField
                sx={{ mt: 2 }}
                label="Fail-condition JsonLogic rule"
                value={failRule ?? ''}
                onChange={(e) => setFailRule(e.target.value || undefined)}
                helperText="Optional. When it evaluates true first, the objective is marked failed instead of completed. May reference the competitorCompletions count of other event competitors."
                multiline
                minRows={3}
                fullWidth
              />
            </Box>
          )}

          {error && <Alert severity="error">{error}</Alert>}
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={mutation.isPending}>
          Cancel
        </Button>
        <Button variant="contained" onClick={handleSave} disabled={mutation.isPending}>
          Save
        </Button>
      </DialogActions>
    </Dialog>
  )
}
