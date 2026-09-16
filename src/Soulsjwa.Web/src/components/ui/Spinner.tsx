import CircularProgress from '@mui/material/CircularProgress'

interface SpinnerProps {
  size?: number
}

export const Spinner = ({ size = 32 }: SpinnerProps) => (
  <CircularProgress role="status" aria-label="Loading" size={size} />
)
