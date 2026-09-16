import { useState } from 'react'
import { Link as RouterLink } from 'react-router-dom'
import Alert from '@mui/material/Alert'
import Box from '@mui/material/Box'
import Chip from '@mui/material/Chip'
import CircularProgress from '@mui/material/CircularProgress'
import List from '@mui/material/List'
import ListItemButton from '@mui/material/ListItemButton'
import Paper from '@mui/material/Paper'
import Stack from '@mui/material/Stack'
import Typography from '@mui/material/Typography'
import { EmptyState, ErrorMessage, LoadingState } from '../../../components/ui'
import { MyEventObjectiveList } from './MyEventObjectiveList'
import { TrialRunControl } from '../../events/components/TrialRunControl'
import { useMyTrialRuns } from '../hooks/useMyTrialRuns'
import { useMyTrialRunObjectives } from '../hooks/useMyTrialRunObjectives'
import { useToggleTrialObjective } from '../hooks/useToggleTrialObjective'
import { useToggleTrialObjectiveFailure } from '../hooks/useToggleTrialObjectiveFailure'
import { useFailRemainingTrialObjectives } from '../hooks/useFailRemainingTrialObjectives'
import { getEventPath } from '../../events/eventUrl'
import { TRIAL_TOOLTIP } from '../../events/scoreboard/trialPresentation'
import {
  TRIAL_RUN_RECORDING_STATE,
  TRIAL_RUN_STATE_COLORS,
  TRIAL_RUN_STATE_LABELS,
  type MyTrialRun,
} from '../../../types'

const runKey = (run: MyTrialRun) => run.trialRunId

/**
 * The Trial runs tab: every trial the viewer may see, and the selected run's
 * own objectives. Trial progress appears here and nowhere else on this page —
 * the other tabs stay official-only so the two can't be mistaken for each
 * other in the same view.
 */
export const MyTrialRunsPanel = () => {
  const { data: runs, isLoading, isError } = useMyTrialRuns()
  const [selectedId, setSelectedId] = useState<string | null>(null)

  if (isLoading) return <LoadingState label="Loading trial runs…" />
  if (isError || !runs) return <ErrorMessage message="Failed to load trial runs." />

  if (runs.length === 0) {
    return (
      <EmptyState
        title="No trial runs yet."
        description="Enable trial mode on a competitor's game to practise without touching the official score."
      />
    )
  }

  const selected = runs.find((run) => runKey(run) === selectedId) ?? runs[0]!

  return (
    <Stack spacing={1.5}>
      <Alert severity="info" variant="outlined">
        {TRIAL_TOOLTIP}
      </Alert>
      <Stack direction={{ xs: 'column', md: 'row' }} spacing={2} sx={{ alignItems: 'flex-start' }}>
        <Paper variant="outlined" sx={{ width: { xs: '100%', md: 320 }, flexShrink: 0 }}>
          <List disablePadding>
            {runs.map((run) => (
              <ListItemButton
                key={runKey(run)}
                selected={runKey(run) === runKey(selected)}
                onClick={() => setSelectedId(runKey(run))}
                sx={{ display: 'block' }}
              >
                <Typography sx={{ fontWeight: 600 }}>{run.gameName}</Typography>
                <Typography variant="body2" color="text.secondary">
                  {run.eventName}
                  {!run.isOwnTrial && ` · ${run.competitorName}`}
                </Typography>
                <Stack direction="row" spacing={1} sx={{ mt: 0.5, alignItems: 'center' }}>
                  <Chip
                    label={TRIAL_RUN_STATE_LABELS[run.state]}
                    size="small"
                    color={TRIAL_RUN_STATE_COLORS[run.state]}
                    variant="outlined"
                  />
                  <Typography variant="caption" sx={{ color: 'warning.main', fontWeight: 600 }}>
                    {run.score} pts · {run.completedCount}/{run.totalObjectives}
                  </Typography>
                </Stack>
              </ListItemButton>
            ))}
          </List>
        </Paper>
        <Box sx={{ flex: 1, minWidth: 0, width: '100%' }}>
          <SelectedTrialRun run={selected} />
        </Box>
      </Stack>
    </Stack>
  )
}

const SelectedTrialRun = ({ run }: { run: MyTrialRun }) => {
  const toggle = useToggleTrialObjective(run)
  const toggleFailure = useToggleTrialObjectiveFailure(run)
  const failRemaining = useFailRemainingTrialObjectives(run)
  const isPending = toggle.isPending || toggleFailure.isPending || failRemaining.isPending
  const objectives = useMyTrialRunObjectives(run.trialRunId, true, isPending)
  // Completions are attributed by the server to whichever run is *recording*,
  // so ticking is only correct while this one is.
  const isRecording = run.state === TRIAL_RUN_RECORDING_STATE

  return (
    <Paper variant="outlined" sx={{ p: 2 }}>
      <Stack spacing={1.5}>
        <Stack
          direction="row"
          spacing={1}
          sx={{ alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap' }}
        >
          <Box sx={{ minWidth: 0 }}>
            <Typography
              component={RouterLink}
              to={getEventPath(run.eventId, run.urlAlias)}
              sx={{ color: 'primary.main', fontWeight: 700 }}
            >
              {run.eventName}
            </Typography>
            <Typography variant="body2" color="text.secondary">
              {run.gameName}
              {!run.isGameEnabled && ' · not the event’s active game'}
            </Typography>
          </Box>
          <Typography variant="h6" sx={{ color: 'warning.main' }}>
            {run.score} pts
          </Typography>
        </Stack>

        {/* Enable/start/stop/reset/disable live here so the tab that shows a
            run's progress is also where it can be cleared. */}
        <TrialRunControl
          eventId={run.eventId}
          eventGameId={run.eventGameId}
          gameName={run.gameName}
          userId={run.competitorId}
        />

        {!isRecording && (
          <Alert severity="warning" variant="outlined">
            This run is {TRIAL_RUN_STATE_LABELS[run.state].toLowerCase()}, so nothing can be ticked
            for this game — not here, and not against the official score either. Start the run to
            practise, or disable trial mode to play for real again.
          </Alert>
        )}

        {objectives.isLoading && (
          <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
            <CircularProgress size={20} />
            <Typography>Loading objectives…</Typography>
          </Stack>
        )}
        {objectives.isError && <Alert severity="error">Failed to load trial objectives.</Alert>}
        {toggle.isError && <Alert severity="error">Could not update the objective.</Alert>}
        {toggleFailure.isError && (
          <Alert severity="error">Could not update the objective&apos;s failure status.</Alert>
        )}
        {failRemaining.isError && (
          <Alert severity="error">Could not fail the remaining objectives.</Alert>
        )}
        {objectives.data && (
          <MyEventObjectiveList
            games={objectives.data.games}
            isTrial
            canToggle={() => isRecording}
            isPending={isPending}
            onToggleCompleted={(_game, objectiveId, completed) =>
              toggle.mutate({ objectiveId, completed })
            }
            onToggleFailed={(_game, objectiveId, failed) =>
              toggleFailure.mutate({ objectiveId, failed })
            }
            onFailRemaining={() => failRemaining.mutateAsync().catch(() => undefined)}
            failRemainingPending={failRemaining.isPending}
            targetName={run.isOwnTrial ? undefined : run.competitorName}
          />
        )}
      </Stack>
    </Paper>
  )
}
