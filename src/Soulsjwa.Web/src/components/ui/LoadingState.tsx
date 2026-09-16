import Stack from '@mui/material/Stack'
import Typography from '@mui/material/Typography'
import { Spinner } from './Spinner'
import { Surface } from './Surface'

interface LoadingStateProps {
  label?: string
}

export const LoadingState = ({ label = 'Loading…' }: LoadingStateProps) => (
  <Surface>
    <Stack spacing={2} sx={{ alignItems: 'center', justifyContent: 'center', py: 6 }}>
      <Spinner />
      <Typography color="text.secondary">{label}</Typography>
    </Stack>
  </Surface>
)
