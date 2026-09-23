import Stack from '@mui/material/Stack'
import Typography from '@mui/material/Typography'

interface ActiveGameHeaderProps {
  gameName: string
  objectiveCount: number
  /** Competitors with every objective of the game completed or failed. */
  doneCount: number
  competitorCount: number
}

/** "Now playing" line above the current game standings. Nothing time-based. */
export const ActiveGameHeader = ({
  gameName,
  objectiveCount,
  doneCount,
  competitorCount,
}: ActiveGameHeaderProps) => (
  <Stack
    direction={{ xs: 'column', sm: 'row' }}
    spacing={{ xs: 0.5, sm: 2 }}
    sx={{ alignItems: { sm: 'baseline' } }}
  >
    <Typography component="h3" variant="h6">
      Now playing: {gameName}
    </Typography>
    <Typography variant="body2" color="text.secondary">
      {objectiveCount} {objectiveCount === 1 ? 'objective' : 'objectives'}
    </Typography>
    <Typography variant="body2" color="text.secondary">
      {doneCount} of {competitorCount} done
    </Typography>
  </Stack>
)
