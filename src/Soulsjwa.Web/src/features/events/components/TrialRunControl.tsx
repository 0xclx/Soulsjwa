import { useState } from 'react'
import type { UseMutationResult } from '@tanstack/react-query'
import Alert from '@mui/material/Alert'
import Button from '@mui/material/Button'
import Chip from '@mui/material/Chip'
import Stack from '@mui/material/Stack'
import Typography from '@mui/material/Typography'
import { getErrorDetail } from '../../../lib/getErrorDetail'
import { useTrialRun } from '../hooks/useTrialRun'
import { useEnableTrialRun } from '../hooks/useEnableTrialRun'
import { useDisableTrialRun } from '../hooks/useDisableTrialRun'
import { useStartTrialRun } from '../hooks/useStartTrialRun'
import { useStopTrialRun } from '../hooks/useStopTrialRun'
import { useResetTrialRun } from '../hooks/useResetTrialRun'
import { DisableTrialRunDialog } from './DisableTrialRunDialog'
import {
  TRIAL_RUN_RECORDING_STATE,
  TRIAL_RUN_STATE_COLORS,
  TRIAL_RUN_STATE_LABELS,
  type TrialRun,
} from '../../../types'

interface TrialRunControlProps {
  eventId: string
  eventGameId: string
  gameName: string
  userId: string
}

/**
 * One game's trial/training controls for a competitor: enable,
 * start/stop/reset, and a destructive disable behind a typed confirmation.
 *
 * Trial progress never reaches official scoring, ranking, or cross-competitor
 * fail-rule cascades. It *is* shown publicly — in amber, behind a Trial badge,
 * on the scoreboard and the OBS overlay — so a practice run is watchable
 * without being scoreable.
 */
export const TrialRunControl = ({
  eventId,
  eventGameId,
  gameName,
  userId,
}: TrialRunControlProps) => {
  const { data: trialRun, isLoading } = useTrialRun(eventId, eventGameId, userId)
  const enable = useEnableTrialRun(eventId, eventGameId, userId)
  const disable = useDisableTrialRun(eventId, eventGameId, userId)
  const start = useStartTrialRun(eventId, eventGameId, userId)
  const stop = useStopTrialRun(eventId, eventGameId, userId)
  const reset = useResetTrialRun(eventId, eventGameId, userId)
  const [confirmOpen, setConfirmOpen] = useState(false)
  const [error, setError] = useState<string | null>(null)

  if (isLoading) return null

  const isBusy =
    enable.isPending || disable.isPending || start.isPending || stop.isPending || reset.isPending

  const run = (
    mutation: UseMutationResult<TrialRun | void, unknown, void, unknown>,
    fallback: string,
    onSuccess?: () => void,
  ) => {
    setError(null)
    mutation.mutate(undefined, {
      onError: (err) => setError(getErrorDetail(err, fallback)),
      onSuccess,
    })
  }

  return (
    <Stack spacing={0.5}>
      <Stack direction="row" spacing={1} sx={{ alignItems: 'center', flexWrap: 'wrap' }}>
        <Typography variant="body2" sx={{ minWidth: 0 }}>
          {gameName}
        </Typography>
        {!trialRun ? (
          <Button
            size="small"
            variant="outlined"
            disabled={isBusy}
            onClick={() => run(enable, 'Failed to enable trial mode.')}
          >
            Enable trial
          </Button>
        ) : (
          <>
            <Chip
              label={`Trial: ${TRIAL_RUN_STATE_LABELS[trialRun.state]}`}
              size="small"
              color={TRIAL_RUN_STATE_COLORS[trialRun.state]}
            />
            {trialRun.state === TRIAL_RUN_RECORDING_STATE ? (
              <Button
                size="small"
                variant="outlined"
                disabled={isBusy}
                onClick={() => run(stop, 'Failed to pause trial run.')}
              >
                Stop
              </Button>
            ) : (
              <Button
                size="small"
                variant="outlined"
                disabled={isBusy || trialRun.state === 'Completed'}
                onClick={() => run(start, 'Failed to start trial run.')}
              >
                Start
              </Button>
            )}
            <Button
              size="small"
              variant="outlined"
              disabled={isBusy}
              onClick={() => run(reset, 'Failed to reset trial run.')}
            >
              Reset
            </Button>
            <Button
              size="small"
              variant="outlined"
              color="error"
              disabled={isBusy}
              onClick={() => setConfirmOpen(true)}
            >
              Disable
            </Button>
          </>
        )}
      </Stack>
      {!confirmOpen && error && <Alert severity="error">{error}</Alert>}
      <DisableTrialRunDialog
        open={confirmOpen}
        gameName={gameName}
        pending={disable.isPending}
        error={confirmOpen ? error : null}
        onCancel={() => setConfirmOpen(false)}
        onConfirm={() => run(disable, 'Failed to disable trial mode.', () => setConfirmOpen(false))}
      />
    </Stack>
  )
}
