import { useState } from 'react'
import Alert from '@mui/material/Alert'
import Button from '@mui/material/Button'
import Chip from '@mui/material/Chip'
import Divider from '@mui/material/Divider'
import FormControlLabel from '@mui/material/FormControlLabel'
import Link from '@mui/material/Link'
import List from '@mui/material/List'
import ListItem from '@mui/material/ListItem'
import ListItemText from '@mui/material/ListItemText'
import MenuItem from '@mui/material/MenuItem'
import Stack from '@mui/material/Stack'
import Switch from '@mui/material/Switch'
import TextField from '@mui/material/TextField'
import Typography from '@mui/material/Typography'
import DownloadIcon from '@mui/icons-material/Download'
import { ErrorMessage, LoadingState, Surface } from '../../../components/ui'
import {
  TWITCH_EXTENSION_SCOPE_LABELS,
  type TwitchExtensionAdminInfo,
  type TwitchExtensionPolicy,
  type TwitchExtensionScope,
} from '../../../types/twitchExtension'
import { useDownloadTwitchExtensionBundle } from '../hooks/useDownloadTwitchExtensionBundle'
import { useTwitchExtensionAdmin } from '../hooks/useTwitchExtensionAdmin'
import { useUpdateTwitchExtensionSettings } from '../hooks/useUpdateTwitchExtensionSettings'

export const STATUS_HEADING = 'Status'
export const RULES_HEADING = 'Rules for every channel'
export const BUNDLE_HEADING = 'Extension bundle'
export const SETUP_HEADING = 'Setting it up on Twitch'
export const DOWNLOAD_LABEL = 'Download extension zip'
export const SAVE_LABEL = 'Save rules'
export const SAVED_LABEL = 'Saved. Every channel picks the change up within a few seconds.'
export const CONFIGURED_LABEL = 'Configured'
export const NOT_CONFIGURED_LABEL = 'Not configured'
export const PUSH_ON_LABEL = 'Push updates on'
export const PUSH_OFF_LABEL = 'Push updates off'
export const BUNDLE_READY_LABEL = 'Bundle ready'
export const BUNDLE_MISSING_LABEL = 'Bundle missing'
const ALLOW_EVENT_CHOICE_LABEL = 'Streamers may show an event other than the featured one'
const ALLOW_SCOPE_SWITCH_LABEL =
  'Viewers may switch between all games, the active game and any game'
const DEFAULT_SCOPE_LABEL = 'Default view for channels without their own settings'
const DEFAULT_HIGHLIGHT_LABEL = "Highlight the streamer's own row by default"
const DEFAULT_TRIAL_LABEL = 'Show trial-run progress by default'
const SAVE_ERROR = 'Could not save the rules.'
const DOWNLOAD_ERROR = 'Could not download the bundle.'
const NOT_CONFIGURED_HINT =
  'Set TwitchExtension__ClientId and TwitchExtension__Secret on the server (and TwitchExtension__OwnerUserId for push updates), then restart it.'
const BUNDLE_MISSING_HINT =
  'The Docker image builds the bundle; outside Docker run `npm run build:twitch` in src/Soulsjwa.Web and point TwitchExtension__BundlePath at dist-twitch.'
const TWITCH_CONSOLE_URL = 'https://dev.twitch.tv/console/extensions'
const TWITCH_DASHBOARD_EXTENSIONS_URL = 'https://dashboard.twitch.tv/extensions'
const IMAGE_ALLOWLIST_HOST = 'static-cdn.jtvnw.net'
const PANEL_HEIGHT_PX = 496

/** A channel-wide default cannot pin a game, so the picker offers only these. */
const GLOBAL_DEFAULT_SCOPES = [
  'AllGames',
  'ActiveGame',
] as const satisfies readonly TwitchExtensionScope[]

const hostOf = (url: string): string => {
  try {
    return new URL(url).host
  } catch {
    return url
  }
}

/**
 * The admin page's Twitch extension tab: where the credentials stand (they
 * live in server configuration, so this is read-only), the rules that apply
 * to every channel, the zip to upload to Twitch with this deployment's API
 * origin written in, and the console steps with the values filled in.
 */
export const TwitchExtensionAdminTab = () => {
  const info = useTwitchExtensionAdmin()
  const update = useUpdateTwitchExtensionSettings()
  const download = useDownloadTwitchExtensionBundle()
  const [draft, setDraft] = useState<TwitchExtensionPolicy | null>(null)
  const [saved, setSaved] = useState(false)

  if (info.isLoading) return <LoadingState label="Loading Twitch extension…" />
  if (info.isError || !info.data)
    return <ErrorMessage message="Failed to load the Twitch extension status." />

  const data = info.data
  const rules = draft ?? data.settings

  const change = (patch: Partial<TwitchExtensionPolicy>) => {
    setSaved(false)
    setDraft({ ...rules, ...patch })
  }

  const save = async () => {
    await update.mutateAsync(rules)
    setDraft(null)
    setSaved(true)
  }

  return (
    <Stack spacing={3}>
      <Surface>
        <Stack spacing={2}>
          <Typography variant="h6">{STATUS_HEADING}</Typography>
          <Stack direction="row" spacing={1} sx={{ flexWrap: 'wrap', gap: 1 }}>
            <Chip
              label={data.configured ? CONFIGURED_LABEL : NOT_CONFIGURED_LABEL}
              color={data.configured ? 'success' : 'default'}
            />
            <Chip
              label={data.canPush ? PUSH_ON_LABEL : PUSH_OFF_LABEL}
              color={data.canPush ? 'success' : 'default'}
            />
            <Chip
              label={data.bundle.available ? BUNDLE_READY_LABEL : BUNDLE_MISSING_LABEL}
              color={data.bundle.available ? 'success' : 'warning'}
            />
          </Stack>
          {!data.configured && <Alert severity="info">{NOT_CONFIGURED_HINT}</Alert>}
          <StatusRows data={data} />
        </Stack>
      </Surface>

      <Surface>
        <Stack spacing={2}>
          <Typography variant="h6">{BUNDLE_HEADING}</Typography>
          <Typography color="text.secondary">
            The zip Twitch's developer console takes under <em>Files → Upload Version in Assets</em>
            . It is built into this deployment and, on download, told to call{' '}
            <code>{data.bundle.apiUrl}</code>, so download it again after moving the site to another
            host.
          </Typography>
          {!data.bundle.available && <Alert severity="warning">{BUNDLE_MISSING_HINT}</Alert>}
          <Stack direction="row" spacing={2} sx={{ alignItems: 'center' }}>
            <Button
              variant="contained"
              startIcon={<DownloadIcon />}
              disabled={!data.bundle.available || download.isPending}
              onClick={() => download.mutate()}
            >
              {DOWNLOAD_LABEL}
            </Button>
            {data.bundle.available && (
              <Typography variant="body2" color="text.secondary">
                {data.bundle.fileCount} files
              </Typography>
            )}
          </Stack>
          {download.isError && <Alert severity="error">{DOWNLOAD_ERROR}</Alert>}
        </Stack>
      </Surface>

      <Surface>
        <Stack spacing={2}>
          <Typography variant="h6">{RULES_HEADING}</Typography>
          <Typography color="text.secondary">
            These apply to every channel that installs the extension. Streamers still choose their
            own default view and toggles within them, from the extension's settings on Twitch or the
            event's Broadcast tab.
          </Typography>
          <Stack>
            <FormControlLabel
              control={
                <Switch
                  checked={rules.allowChannelEventChoice}
                  onChange={(e) => change({ allowChannelEventChoice: e.target.checked })}
                />
              }
              label={ALLOW_EVENT_CHOICE_LABEL}
            />
            <FormControlLabel
              control={
                <Switch
                  checked={rules.allowViewerScopeSwitch}
                  onChange={(e) => change({ allowViewerScopeSwitch: e.target.checked })}
                />
              }
              label={ALLOW_SCOPE_SWITCH_LABEL}
            />
            <FormControlLabel
              control={
                <Switch
                  checked={rules.defaultHighlightChannelCompetitor}
                  onChange={(e) => change({ defaultHighlightChannelCompetitor: e.target.checked })}
                />
              }
              label={DEFAULT_HIGHLIGHT_LABEL}
            />
            <FormControlLabel
              control={
                <Switch
                  checked={rules.defaultShowTrialProgress}
                  onChange={(e) => change({ defaultShowTrialProgress: e.target.checked })}
                />
              }
              label={DEFAULT_TRIAL_LABEL}
            />
          </Stack>
          <TextField
            select
            size="small"
            label={DEFAULT_SCOPE_LABEL}
            value={rules.defaultScope}
            onChange={(e) => change({ defaultScope: e.target.value as TwitchExtensionScope })}
            sx={{ maxWidth: 360 }}
          >
            {GLOBAL_DEFAULT_SCOPES.map((scope) => (
              <MenuItem key={scope} value={scope}>
                {TWITCH_EXTENSION_SCOPE_LABELS[scope]}
              </MenuItem>
            ))}
          </TextField>
          <Stack direction="row" spacing={2} sx={{ alignItems: 'center', flexWrap: 'wrap' }}>
            <Button
              variant="contained"
              disabled={!draft || update.isPending}
              onClick={() => void save()}
            >
              {SAVE_LABEL}
            </Button>
            {saved && <Typography variant="body2">{SAVED_LABEL}</Typography>}
          </Stack>
          {update.isError && <Alert severity="error">{SAVE_ERROR}</Alert>}
        </Stack>
      </Surface>

      <Surface>
        <Stack spacing={1}>
          <Typography variant="h6">{SETUP_HEADING}</Typography>
          <SetupSteps data={data} />
        </Stack>
      </Surface>
    </Stack>
  )
}

const StatusRows = ({ data }: { data: TwitchExtensionAdminInfo }) => (
  <List dense disablePadding>
    <ListItem disableGutters>
      <ListItemText primary="Client ID" secondary={data.clientId ?? '—'} />
    </ListItem>
    <ListItem disableGutters>
      <ListItemText
        primary="Extension origin (allowed by CORS)"
        secondary={data.extensionOrigin ?? '—'}
      />
    </ListItem>
    <ListItem disableGutters>
      <ListItemText
        primary="API origin written into the bundle"
        secondary={data.bundle.apiUrl || '—'}
      />
    </ListItem>
    {data.localTestOrigin && (
      <ListItem disableGutters>
        <ListItemText
          primary="Local-test origin (development only)"
          secondary={data.localTestOrigin}
        />
      </ListItem>
    )}
  </List>
)

const SetupSteps = ({ data }: { data: TwitchExtensionAdminInfo }) => {
  const apiHost = hostOf(data.bundle.apiUrl)
  const steps: Array<{ title: string; detail: React.ReactNode }> = [
    {
      title: 'Create the extension',
      detail: (
        <>
          Turn on two-factor authentication on the Twitch account, open the{' '}
          <Link href={TWITCH_CONSOLE_URL} target="_blank" rel="noopener noreferrer">
            developer console
          </Link>{' '}
          and create an Extension.
        </>
      ),
    },
    {
      title: 'Extension views',
      detail: (
        <>
          Enable Panel (<code>panel.html</code>, height {PANEL_HEIGHT_PX}), Video Component (
          <code>video_component.html</code>), Mobile (<code>mobile.html</code>), Config (
          <code>config.html</code>) and Live Config (<code>live_config.html</code>).
        </>
      ),
    },
    {
      title: 'Capabilities',
      detail: (
        <>
          Allowlist for URL Fetching Domains: <code>{apiHost || 'your API host'}</code>. Allowlist
          for Image Domains: <code>{IMAGE_ALLOWLIST_HOST}</code>.
        </>
      ),
    },
    {
      title: 'Server configuration',
      detail: data.configured ? (
        <>
          Done: this server backs client ID <code>{data.clientId}</code>
          {data.canPush
            ? ' with push updates.'
            : '. Set TwitchExtension__OwnerUserId to enable push updates.'}
        </>
      ) : (
        <>
          Copy the Client ID and Extension Secret into <code>TwitchExtension__ClientId</code> and{' '}
          <code>TwitchExtension__Secret</code> on the server, optionally the owner account's numeric
          Twitch id into <code>TwitchExtension__OwnerUserId</code>, then restart the server and
          reload this page.
        </>
      ),
    },
    {
      title: 'Upload the bundle',
      detail: (
        <>
          Download the zip above, upload it under <em>Files → Upload Version in Assets</em>, and
          move the version to <em>Hosted Test</em>.
        </>
      ),
    },
    {
      title: 'Install and test',
      detail: (
        <>
          Add your channel to the testing allowlist, install the extension from{' '}
          <Link href={TWITCH_DASHBOARD_EXTENSIONS_URL} target="_blank" rel="noopener noreferrer">
            your Twitch dashboard
          </Link>
          , activate it in a Panel slot, and open your channel page. Streamers who want to change
          what their channel shows sign in here once with the same Twitch account.
        </>
      ),
    },
    {
      title: 'Release (optional)',
      detail:
        'Submit the version for review only if channels outside the allowlist should install it.',
    },
  ]
  return (
    <List>
      {steps.map((step, index) => (
        <ListItem
          key={step.title}
          alignItems="flex-start"
          disableGutters
          divider={index < steps.length - 1}
        >
          <ListItemText
            primary={`${index + 1}. ${step.title}`}
            secondary={step.detail}
            slotProps={{ secondary: { component: 'div' } }}
          />
        </ListItem>
      ))}
      <Divider />
    </List>
  )
}
