import ToggleButton from '@mui/material/ToggleButton'
import ToggleButtonGroup from '@mui/material/ToggleButtonGroup'
import {
  SCOREBOARD_VIEWS,
  SCOREBOARD_VIEW_LABELS,
  type ScoreboardView,
} from '../../scoreboard/scoreboardView'

interface ScoreboardViewSwitchProps {
  view: ScoreboardView
  onChange: (view: ScoreboardView) => void
}

/** "Current game" / "Whole event" toggle. Only rendered while a game is enabled. */
export const ScoreboardViewSwitch = ({ view, onChange }: ScoreboardViewSwitchProps) => (
  <ToggleButtonGroup
    exclusive
    size="small"
    color="primary"
    value={view}
    // Clicking the selected option again reports null; keep the current view.
    onChange={(_, next: ScoreboardView | null) => next && onChange(next)}
    aria-label="Scoreboard view"
    sx={{ width: { xs: '100%', md: 'auto' } }}
  >
    {SCOREBOARD_VIEWS.map((option) => (
      <ToggleButton key={option} value={option} sx={{ flex: { xs: 1, md: 'initial' } }}>
        {SCOREBOARD_VIEW_LABELS[option]}
      </ToggleButton>
    ))}
  </ToggleButtonGroup>
)
