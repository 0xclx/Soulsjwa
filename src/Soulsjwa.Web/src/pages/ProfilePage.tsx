import { Link as RouterLink } from 'react-router-dom'
import Stack from '@mui/material/Stack'
import Typography from '@mui/material/Typography'
import Avatar from '@mui/material/Avatar'
import Button from '@mui/material/Button'
import Box from '@mui/material/Box'
import { useCurrentUser } from '../features/users/hooks/useCurrentUser'
import { useLogout } from '../features/auth/hooks/useLogout'
import { ErrorMessage, LoadingState, PageHeader, Surface } from '../components/ui'
import { ApiKeyManager } from '../features/users/components/ApiKeyManager'

export const ProfilePage = () => {
  const { data: user, isLoading, isError } = useCurrentUser()
  const logout = useLogout()

  if (isLoading) {
    return <LoadingState label="Loading profile…" />
  }

  if (isError || !user) {
    return (
      <Stack spacing={2}>
        <ErrorMessage message="Failed to load profile." />
        <Button component={RouterLink} to="/" variant="text">
          Go home
        </Button>
      </Stack>
    )
  }

  return (
    <Stack spacing={3}>
      <Surface>
        <Stack
          direction={{ xs: 'column', sm: 'row' }}
          spacing={2}
          sx={{ alignItems: { xs: 'flex-start', sm: 'center' } }}
        >
          <Avatar
            src={user.profileImageUrl}
            alt={`${user.displayName} avatar`}
            sx={{ width: 72, height: 72 }}
          />
          <Box sx={{ flexGrow: 1, minWidth: 0 }}>
            <PageHeader
              eyebrow={user.role}
              title={user.displayName}
              description={
                <>
                  @{user.twitchLogin}
                  {user.email ? ` · ${user.email}` : ''}
                </>
              }
            />
          </Box>
          <Button onClick={() => logout.mutate()} disabled={logout.isPending} variant="outlined">
            {logout.isPending ? 'Logging out…' : 'Logout'}
          </Button>
        </Stack>
      </Surface>

      <Surface>
        <Typography variant="h5" component="h2" sx={{ mb: 2 }}>
          API access
        </Typography>
        <ApiKeyManager />
      </Surface>
    </Stack>
  )
}
