import { useEffect, useMemo, useRef, useState } from 'react'
import { Link as RouterLink, useNavigate, useSearchParams } from 'react-router-dom'
import Stack from '@mui/material/Stack'
import Typography from '@mui/material/Typography'
import Alert from '@mui/material/Alert'
import Button from '@mui/material/Button'
import { useIsAuthenticated, tokenManager } from '../lib/axios'
import { queryClient } from '../lib/react-query'
import { Spinner } from '../components/ui'
import { USERS_QUERY_KEYS } from '../features/users/api/usersApi'

export const AuthCallbackPage = () => {
  const navigate = useNavigate()
  const [params] = useSearchParams()
  const error = params.get('error')
  const login = params.get('login')
  const [failed, setFailed] = useState(false)
  const isAuthenticated = useIsAuthenticated()
  const hasRun = useRef(false)

  // If the API redirected here with ?error=not_allowlisted, the OAuth flow
  // succeeded against Twitch but the user is not on the admin allowlist.
  // We must NOT try to refresh in that case; just show the friendly message.
  const blocked = useMemo(() => error === 'not_allowlisted', [error])
  // ?error=access_denied: the user cancelled on Twitch's consent screen. No
  // session was ever issued, so there is nothing to refresh either.
  const cancelled = useMemo(() => error === 'access_denied', [error])

  useEffect(() => {
    if (blocked || cancelled || hasRun.current) return
    hasRun.current = true

    // By the time this route renders, AppProviders' tokenManager.bootstrap()
    // has already refreshed and stored the token — refreshing again here
    // would burn a second rotation of a token that was just issued.
    if (isAuthenticated) {
      tokenManager.start()
      void queryClient.invalidateQueries({ queryKey: USERS_QUERY_KEYS.me })
      navigate('/')
      return
    }

    // Defer the state update so we don't synchronously setState from inside
    // an effect (React 19's `react-hooks/set-state-in-effect` rule).
    const handle = window.setTimeout(() => setFailed(true), 0)
    return () => window.clearTimeout(handle)
  }, [navigate, blocked, cancelled, isAuthenticated])

  if (cancelled) {
    return (
      <Stack spacing={2} sx={{ alignItems: 'center', py: 6 }}>
        <Alert severity="info" sx={{ width: '100%', maxWidth: 480 }}>
          Sign-in was cancelled on Twitch. Nothing was changed; sign in again whenever you like.
        </Alert>
        <Button component={RouterLink} to="/" variant="outlined">
          Back to home
        </Button>
      </Stack>
    )
  }

  if (blocked) {
    return (
      <Stack spacing={2} sx={{ alignItems: 'center', py: 6 }}>
        <Alert severity="warning" sx={{ width: '100%', maxWidth: 480 }}>
          The Twitch account <strong>{login}</strong> isn't on the allowlist for this server, so
          sign-in was rejected. Ask an administrator to add your Twitch login and then try again.
        </Alert>
        <Button component={RouterLink} to="/" variant="outlined">
          Back to home
        </Button>
      </Stack>
    )
  }

  if (failed) {
    return (
      <Stack spacing={2} sx={{ alignItems: 'center', py: 6 }}>
        <Alert severity="error" sx={{ width: '100%', maxWidth: 480 }}>
          Login failed. Please try again.
        </Alert>
        <Button component={RouterLink} to="/" variant="outlined">
          Back to home
        </Button>
      </Stack>
    )
  }

  return (
    <Stack spacing={2} sx={{ alignItems: 'center', py: 8 }}>
      <Spinner />
      <Typography>Completing login…</Typography>
    </Stack>
  )
}
