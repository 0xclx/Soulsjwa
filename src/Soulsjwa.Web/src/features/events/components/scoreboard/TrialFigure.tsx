import Box from '@mui/material/Box'
import Typography from '@mui/material/Typography'
import { visuallyHidden } from '@mui/utils'
import { TRIAL_TOOLTIP } from '../../scoreboard/trialPresentation'

interface TrialFigureProps {
  value: number
  /** Visible prefix, e.g. "Trial" on the score column. */
  label?: string
  /**
   * What this figure is, for assistive tech. Required because amber and
   * position are the only *visual* cues separating a trial figure from the
   * official one above it — without this the number reads as a bare digit,
   * and colour alone is not an accessible signal.
   */
  srLabel: string
}

/**
 * A trial figure shown beside its official counterpart: amber, and on its own
 * line so it reads as a separate number rather than an adjustment to the
 * official one. Deliberately no "+" sign — the two are never summed.
 */
export const TrialFigure = ({ value, label, srLabel }: TrialFigureProps) => (
  <Typography
    component="div"
    variant="caption"
    title={TRIAL_TOOLTIP}
    sx={{ color: 'warning.main', fontWeight: 600, lineHeight: 1.4 }}
  >
    <Box component="span" sx={visuallyHidden}>
      {srLabel}:{' '}
    </Box>
    {label ? `${label} ${value}` : value}
  </Typography>
)
