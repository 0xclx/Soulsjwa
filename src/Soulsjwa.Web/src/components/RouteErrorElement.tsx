import { isRouteErrorResponse, useRouteError, Link as RouterLink } from 'react-router-dom'
import Box from '@mui/material/Box'
import Typography from '@mui/material/Typography'
import Button from '@mui/material/Button'
import Alert from '@mui/material/Alert'
import AlertTitle from '@mui/material/AlertTitle'

/**
 * Route-level error boundary used as the `errorElement` of the root route.
 *
 * React Router catches render / loader / action errors thrown inside the
 * route subtree and renders this element in place of the failing route,
 * which means the global `<ErrorBoundary>` wrapping the whole app stays as
 * a last-resort fallback for errors thrown outside the router (e.g. inside
 * the providers themselves).
 */
export const RouteErrorElement = () => {
  const error = useRouteError()

  const status = isRouteErrorResponse(error) ? error.status : undefined
  const message = isRouteErrorResponse(error)
    ? error.statusText || `${error.status}`
    : error instanceof Error
      ? error.message
      : 'An unexpected error occurred.'

  return (
    <Box
      role="alert"
      sx={{
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'center',
        p: { xs: 4, md: 8 },
        gap: 2,
      }}
    >
      <Typography variant="h4" component="h1">
        {status === 404 ? 'Page not found' : 'Something went wrong'}
      </Typography>
      <Typography color="text.secondary" sx={{ textAlign: 'center' }}>
        {status === 404
          ? "We couldn't find the page you were looking for."
          : 'An unexpected error occurred while rendering this page.'}
      </Typography>
      {message && status !== 404 && (
        <Alert severity="error" sx={{ maxWidth: 600, width: '100%' }}>
          <AlertTitle>Error</AlertTitle>
          {message}
        </Alert>
      )}
      <Box sx={{ display: 'flex', gap: 1 }}>
        <Button component={RouterLink} to="/" variant="contained">
          Go home
        </Button>
        <Button onClick={() => window.location.reload()} variant="outlined">
          Refresh
        </Button>
      </Box>
    </Box>
  )
}
