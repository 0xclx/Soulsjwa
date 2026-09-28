import { useId } from 'react'
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
import { DisplayNameField } from '../features/users/components/DisplayNameField'
import { useUpdateMyDisplayName } from '../features/users/hooks/useUpdateMyDisplayName'
import { getErrorDetail } from '../lib/getErrorDetail'
import type { User } from '../types'

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

      <DisplayNameSection user={user} />

      <Surface>
        <Typography variant="h5" component="h2" sx={{ mb: 2 }}>
          API access
        </Typography>
        <ApiKeyManager />
      </Surface>
    </Stack>
  )
}

const DISPLAY_NAME_SECTION_TEXT = {
  heading: 'Display name',
  description:
    'The name shown for you on scoreboards, overlays and event pages. It is taken from Twitch ' +
    'until you set your own; your own name stays when you sign in again.',
  failed: 'Failed to save the display name.',
} as const

const DisplayNameSection = ({ user }: { user: User }) => {
  const update = useUpdateMyDisplayName()
  const headingId = useId()

  return (
    <Box component="section" aria-labelledby={headingId}>
      <Surface>
        <Typography id={headingId} variant="h5" component="h2" sx={{ mb: 1 }}>
          {DISPLAY_NAME_SECTION_TEXT.heading}
        </Typography>
        <Typography color="text.secondary" sx={{ mb: 2 }}>
          {DISPLAY_NAME_SECTION_TEXT.description}
        </Typography>
        <DisplayNameField
          // Restart from the saved name after every change.
          key={user.displayName}
          currentName={user.displayName}
          twitchDisplayName={user.twitchDisplayName}
          hasOverride={user.displayNameOverride !== null}
          pending={update.isPending}
          error={
            update.isError ? getErrorDetail(update.error, DISPLAY_NAME_SECTION_TEXT.failed) : null
          }
          onSave={(displayName) => update.mutate(displayName)}
          onReset={() => update.mutate(null)}
        />
      </Surface>
    </Box>
  )
}
