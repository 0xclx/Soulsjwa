import Box from '@mui/material/Box'
import Stack from '@mui/material/Stack'
import Typography from '@mui/material/Typography'

const Dot = ({ color }: { color: 'success.main' | 'error.main' }) => (
  <Box sx={{ width: 8, height: 8, borderRadius: '50%', bgcolor: color }} />
)

/** Legend explaining the live/offline status dots used across the scoreboard. */
export const LiveStatusLegend = () => (
  <Box sx={{ textAlign: 'center', py: 1 }}>
    <Stack direction="row" spacing={2} sx={{ justifyContent: 'center', alignItems: 'center' }}>
      <Stack direction="row" spacing={0.5} sx={{ alignItems: 'center' }}>
        <Dot color="success.main" />
        <Typography variant="caption" color="text.secondary">
          Live
        </Typography>
      </Stack>
      <Stack direction="row" spacing={0.5} sx={{ alignItems: 'center' }}>
        <Dot color="error.main" />
        <Typography variant="caption" color="text.secondary">
          Offline
        </Typography>
      </Stack>
    </Stack>
  </Box>
)
