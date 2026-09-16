import Alert from '@mui/material/Alert'
import FormControlLabel from '@mui/material/FormControlLabel'
import Stack from '@mui/material/Stack'
import Switch from '@mui/material/Switch'
import Typography from '@mui/material/Typography'
import { ErrorMessage, LoadingState, Surface } from '../../../components/ui'
import { FEATURE_FLAG_KEYS } from '../api/adminApi'
import { useFeatureFlag } from '../hooks/useFeatureFlag'
import { useUpdateFeatureFlag } from '../hooks/useUpdateFeatureFlag'

export const FeatureFlagsTab = () => {
  const key = FEATURE_FLAG_KEYS.myEventsQuickComplete
  const flag = useFeatureFlag(key)
  const update = useUpdateFeatureFlag(key)

  if (flag.isLoading) return <LoadingState label="Loading feature flags…" />
  if (flag.isError || !flag.data) return <ErrorMessage message="Failed to load feature flags." />

  return (
    <Surface>
      <Stack spacing={1}>
        <Typography variant="h6">My Events quick complete</Typography>
        <Typography color="text.secondary">
          Allow competitors and delegated moderators to complete or uncomplete objectives directly
          from the My Events dashboard.
        </Typography>
        <FormControlLabel
          control={
            <Switch
              checked={flag.data.enabled}
              disabled={update.isPending}
              onChange={(_event, enabled) => update.mutate(enabled)}
            />
          }
          label={flag.data.enabled ? 'Enabled' : 'Disabled'}
        />
        {update.isError && <Alert severity="error">Failed to update the feature flag.</Alert>}
      </Stack>
    </Surface>
  )
}
