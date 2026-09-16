import Alert from '@mui/material/Alert'

interface ErrorMessageProps {
  message: string
}

export const ErrorMessage = ({ message }: ErrorMessageProps) => (
  <Alert severity="error" role="alert">
    {message}
  </Alert>
)
