import { useCallback, useState } from 'react'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Dialog from '@mui/material/Dialog'
import DialogActions from '@mui/material/DialogActions'
import DialogContent from '@mui/material/DialogContent'
import DialogTitle from '@mui/material/DialogTitle'
import Stack from '@mui/material/Stack'
import TextField from '@mui/material/TextField'
import Typography from '@mui/material/Typography'
import { ObjectiveRuleSection } from './ObjectiveRuleSection'
import { useGameDataDefinitions } from '../hooks/useGameDataDefinitions'
import { usePredefinedObjectives } from '../hooks/usePredefinedObjectives'
import { useCreateObjective } from '../hooks/useCreateObjective'
import type { EventGame } from '../../../types'

interface CreateObjectiveDialogProps {
  open: boolean
  eventId: string
  game: EventGame
  connectorSupported: boolean
  onClose: () => void
}

/**
 * Focused dialog for hand-authoring an objective on a game. For connector-
 * supported games it embeds the Blockly rule builder — one instance for the
 * auto-completion rule, one for the (optional) fail condition — which needs
 * real room to lay out its toolbox and canvas, hence a wide dialog rather
 * than the cramped inline form it replaced.
 */
export const CreateObjectiveDialog = ({
  open,
  eventId,
  game,
  connectorSupported,
  onClose,
}: CreateObjectiveDialogProps) => {
  const [name, setName] = useState('')
  const [score, setScore] = useState(0)
  const [category, setCategory] = useState('')
  const [rule, setRule] = useState<string | undefined>(undefined)
  const [failRule, setFailRule] = useState<string | undefined>(undefined)
  const [showRuleBuilder, setShowRuleBuilder] = useState(false)
  const [showFailRuleBuilder, setShowFailRuleBuilder] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const needsDataPoints = connectorSupported && (showRuleBuilder || showFailRuleBuilder)
  const { data: dataPoints, isLoading: loadingDataPoints } = useGameDataDefinitions(
    needsDataPoints && game.knownGameId ? game.knownGameId : undefined,
  )
  const { data: predefinedObjectives, isLoading: loadingPredefinedObjectives } =
    usePredefinedObjectives(
      game.knownGameId ?? 0,
      connectorSupported && (showRuleBuilder || showFailRuleBuilder) && game.knownGameId != null,
    )
  const createObjective = useCreateObjective()

  const handleRuleChange = useCallback((newRule: string | undefined) => {
    setRule(newRule)
  }, [])

  const handleFailRuleChange = useCallback((newRule: string | undefined) => {
    setFailRule(newRule)
  }, [])

  const resetForm = useCallback(() => {
    setName('')
    setScore(0)
    setCategory('')
    setRule(undefined)
    setFailRule(undefined)
    setShowRuleBuilder(false)
    setShowFailRuleBuilder(false)
    setError(null)
  }, [])

  const handleClose = useCallback(() => {
    resetForm()
    onClose()
  }, [onClose, resetForm])

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    setError(null)

    if (!name.trim()) {
      setError('Name is required.')
      return
    }

    createObjective.mutate(
      {
        eventId,
        eventGameId: game.eventGameId,
        payload: {
          name: name.trim(),
          score,
          category: category.trim() || undefined,
          rule: showRuleBuilder ? rule : undefined,
          failRule: showFailRuleBuilder ? failRule : undefined,
        },
      },
      {
        onSuccess: () => {
          resetForm()
          onClose()
        },
        onError: () => {
          setError('Failed to create objective.')
        },
      },
    )
  }

  const wide = connectorSupported && (showRuleBuilder || showFailRuleBuilder)

  return (
    <Dialog open={open} onClose={handleClose} fullWidth maxWidth={wide ? 'md' : 'sm'}>
      <Box component="form" onSubmit={handleSubmit}>
        <DialogTitle>Add objective — {game.gameName}</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ mt: 1 }}>
            <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1.5}>
              <TextField
                label="Objective name"
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
                onChange={(e) => setScore(Number(e.target.value))}
                slotProps={{ htmlInput: { min: 0, step: 1 } }}
                sx={{ width: { xs: '100%', sm: 140 } }}
              />
            </Stack>

            <TextField
              label="Category"
              value={category}
              onChange={(e) => setCategory(e.target.value)}
              helperText="Optional grouping label (e.g. an in-game area) shown wherever objectives are grouped."
              slotProps={{ htmlInput: { maxLength: 200 } }}
            />

            {connectorSupported && (
              <>
                <ObjectiveRuleSection
                  label="Define auto-completion rule"
                  checked={showRuleBuilder}
                  onCheckedChange={setShowRuleBuilder}
                  loading={loadingDataPoints || loadingPredefinedObjectives}
                  dataPoints={dataPoints ?? []}
                  predefinedObjectives={predefinedObjectives}
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
                  rule={failRule}
                  onRuleChange={handleFailRuleChange}
                />
              </>
            )}

            {error && (
              <Typography color="error" variant="body2">
                {error}
              </Typography>
            )}
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={handleClose} color="inherit" disabled={createObjective.isPending}>
            Cancel
          </Button>
          <Button type="submit" variant="contained" disabled={createObjective.isPending}>
            {createObjective.isPending ? 'Creating…' : 'Create objective'}
          </Button>
        </DialogActions>
      </Box>
    </Dialog>
  )
}
