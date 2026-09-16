import { useState } from 'react'
import Button from '@mui/material/Button'
import Tooltip from '@mui/material/Tooltip'
import CancelIcon from '@mui/icons-material/Cancel'
import { ConfirmDialog } from '../../../components/ui'

export const FAIL_REMAINING_LABEL = 'Fail all remaining objectives'
export const NOTHING_REMAINING_HINT = 'Every objective in this game is already completed or failed.'
/** How the dialog names the viewer when the failures land on their own record. */
export const SELF_TARGET_NAME = 'you'

const objectives = (count: number) => `${count} objective${count === 1 ? '' : 's'}`

interface FailRemainingObjectivesButtonProps {
  gameName: string
  /** Objectives of the game still pending for the target — what a confirm fails. */
  remainingCount: number
  /** Whose record the failures land on: {@link SELF_TARGET_NAME} or a competitor's name. */
  targetName: string
  /** The failures go to a trial run rather than the official record. */
  isTrial?: boolean
  disabled?: boolean
  /** Why the button is disabled, shown as a tooltip. */
  disabledHint?: string
  pending?: boolean
  /** Resolves when the write settled; rejection is the surface's to report. */
  onConfirm: () => Promise<unknown>
}

/**
 * The one-click end of a run: a button that, after a confirmation naming the
 * game, the count and the competitor, fails every objective still pending
 * for them. Offered wherever objectives can be failed one at a time — the
 * event's Games tab, My Events, the Trial tab — so a death in a no-death run
 * reaches its terminal outcome on every scoreboard without a click per
 * objective. Completed and already-failed objectives are never touched, and
 * any objective failed this way can still be reset individually.
 */
export const FailRemainingObjectivesButton = ({
  gameName,
  remainingCount,
  targetName,
  isTrial = false,
  disabled = false,
  disabledHint,
  pending = false,
  onConfirm,
}: FailRemainingObjectivesButtonProps) => {
  const [open, setOpen] = useState(false)
  const nothingLeft = remainingCount === 0
  const isDisabled = disabled || nothingLeft || pending
  const hint = disabled ? disabledHint : nothingLeft ? NOTHING_REMAINING_HINT : undefined

  const confirm = () => {
    onConfirm().finally(() => setOpen(false))
  }

  return (
    <>
      <Tooltip title={isDisabled && hint ? hint : ''}>
        <span>
          <Button
            size="small"
            variant="outlined"
            color="error"
            startIcon={<CancelIcon />}
            disabled={isDisabled}
            onClick={() => setOpen(true)}
          >
            {FAIL_REMAINING_LABEL}
          </Button>
        </span>
      </Tooltip>
      <ConfirmDialog
        open={open}
        title={`Fail all remaining objectives in ${gameName}?`}
        description={`${objectives(remainingCount)} still pending for ${targetName} will be marked failed${
          isTrial ? ' on the trial run' : ''
        }. Completed and already-failed objectives stay as they are, and any objective failed this way can still be reset one at a time.`}
        confirmLabel={`Fail ${objectives(remainingCount)}`}
        pending={pending}
        onCancel={() => setOpen(false)}
        onConfirm={confirm}
      />
    </>
  )
}
