import { useState } from 'react'
import Alert from '@mui/material/Alert'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Chip from '@mui/material/Chip'
import FormControlLabel from '@mui/material/FormControlLabel'
import Link from '@mui/material/Link'
import MenuItem from '@mui/material/MenuItem'
import Paper from '@mui/material/Paper'
import Stack from '@mui/material/Stack'
import Switch from '@mui/material/Switch'
import TextField from '@mui/material/TextField'
import Typography from '@mui/material/Typography'
import OpenInNewIcon from '@mui/icons-material/OpenInNew'
import {
  TWITCH_EXTENSION_SCOPES,
  TWITCH_EXTENSION_SCOPE_LABELS,
  type TwitchExtensionScope,
  type UpdateTwitchExtensionConfigurationRequest,
} from '../../../types/twitchExtension'
import { twitchExtensionInstallUrl } from '../api/twitchExtensionApi'
import { useMyTwitchExtensionConfiguration } from '../hooks/useMyTwitchExtensionConfiguration'
import { useTwitchExtensionStatus } from '../hooks/useTwitchExtensionStatus'
import { useUpdateMyTwitchExtensionConfiguration } from '../hooks/useUpdateMyTwitchExtensionConfiguration'

export interface TwitchExtensionSectionGame {
  eventGameId: string
  gameName: string
  isEnabled: boolean
}

interface TwitchExtensionSectionProps {
  eventId: string
  eventName: string
  games: TwitchExtensionSectionGame[]
}

export const TWITCH_EXTENSION_HEADING = 'Twitch extension'
export const INSTALL_LABEL = 'Install on Twitch'
export const SHOW_THIS_EVENT_LABEL = 'Show this event on my channel'
export const FOLLOW_FEATURED_LABEL = 'Follow the featured event'
export const SAVE_LABEL = 'Save'
export const SAVED_LABEL = 'Saved. Viewers see the change within a few seconds.'
export const SHOWING_THIS_EVENT_LABEL = 'Your channel shows this event'
export const FOLLOWING_FEATURED_LABEL = 'Your channel follows the featured event'
export const SHOWING_OTHER_PREFIX = 'Your channel shows'
export const NOTHING_SHOWN_LABEL = 'Your channel has nothing to show right now'
export const EVENT_CHOICE_LOCKED_NOTICE =
  'An admin has set every channel to follow the featured event, so the event cannot be changed here.'
const DESCRIPTION =
  "Viewers on your Twitch channel see this scoreboard in a panel under your stream once you install the extension. Pick what your channel shows here or in the extension's own settings on Twitch."
const SCOPE_LABEL = 'Default view for viewers'
const PINNED_GAME_LABEL = 'Pinned game'
const HIGHLIGHT_LABEL = 'Highlight my own row when I compete'
const TRIAL_LABEL = 'Show trial-run progress'
const PINNED_SCOPE: TwitchExtensionScope = 'PinnedGame'
const SAVE_ERROR = 'Could not save the extension settings.'

/**
 * The Broadcast tab's card for the Twitch extension: an install link and the
 * signed-in user's channel settings, edited on the same server row the
 * extension's config view uses. Rendered only when the server backs an
 * extension at all, and only for competitors and owners, who are the ones
 * with a channel to show it on.
 */
export function TwitchExtensionSection({ eventId, eventName, games }: TwitchExtensionSectionProps) {
  const { data: status } = useTwitchExtensionStatus()
  const configured = status?.configured === true && !!status.clientId
  const { data: configuration, isLoading } = useMyTwitchExtensionConfiguration(configured)
  const update = useUpdateMyTwitchExtensionConfiguration()
  const [draft, setDraft] = useState<UpdateTwitchExtensionConfigurationRequest | null>(null)
  const [saved, setSaved] = useState(false)

  if (!configured || !status?.clientId) return null

  const settings = draft ?? configuration?.settings ?? null
  const shownEvent = configuration?.resolvedEvent ?? null
  const showsThisEvent = settings?.eventId === eventId
  const followsFeatured = settings?.eventId === null
  const canPickEvent = configuration?.policy.allowChannelEventChoice ?? true

  const change = (patch: Partial<UpdateTwitchExtensionConfigurationRequest>) => {
    if (!settings) return
    setSaved(false)
    setDraft({ ...settings, ...patch })
  }

  const save = async () => {
    if (!settings) return
    await update.mutateAsync({
      ...settings,
      pinnedEventGameId: settings.defaultScope === PINNED_SCOPE ? settings.pinnedEventGameId : null,
    })
    setDraft(null)
    setSaved(true)
  }

  const statusLine = !configuration
    ? null
    : showsThisEvent && !draft
      ? SHOWING_THIS_EVENT_LABEL
      : followsFeatured && !draft
        ? `${FOLLOWING_FEATURED_LABEL}${shownEvent ? ` (${shownEvent.name})` : ''}`
        : shownEvent && !draft
          ? `${SHOWING_OTHER_PREFIX} ${shownEvent.name}`
          : draft
            ? null
            : NOTHING_SHOWN_LABEL

  // A pinned game belongs to the event the channel will show; offer this
  // event's games when it is the pick, otherwise whatever the channel resolves to.
  const pinnableGames: TwitchExtensionSectionGame[] = showsThisEvent
    ? games
    : (configuration?.events
        .find((e) => (settings?.eventId ? e.id === settings.eventId : e.isFeatured))
        ?.games.map((g) => ({
          eventGameId: g.eventGameId,
          gameName: g.name,
          isEnabled: g.isEnabled,
        })) ?? [])

  return (
    <Box component="section" aria-labelledby="twitch-extension-heading">
      <Stack
        direction={{ xs: 'column', sm: 'row' }}
        spacing={2}
        sx={{ justifyContent: 'space-between', alignItems: { xs: 'stretch', sm: 'center' }, mb: 2 }}
      >
        <Box>
          <Typography variant="h5" component="h2" id="twitch-extension-heading">
            {TWITCH_EXTENSION_HEADING}
          </Typography>
          <Typography variant="body2" color="text.secondary">
            {DESCRIPTION}
          </Typography>
        </Box>
        <Button
          variant="outlined"
          endIcon={<OpenInNewIcon />}
          component={Link}
          href={twitchExtensionInstallUrl(status.clientId)}
          target="_blank"
          rel="noopener noreferrer"
          sx={{ alignSelf: { xs: 'flex-start', sm: 'center' }, flexShrink: 0 }}
        >
          {INSTALL_LABEL}
        </Button>
      </Stack>

      <Paper variant="outlined" sx={{ p: 2 }}>
        {isLoading || !settings ? (
          <Typography variant="body2" color="text.secondary">
            Loading channel settings…
          </Typography>
        ) : (
          <Stack spacing={2}>
            {statusLine && (
              <Stack direction="row" spacing={1} sx={{ alignItems: 'center', flexWrap: 'wrap' }}>
                <Typography variant="body1">{statusLine}</Typography>
                {showsThisEvent && <Chip label={eventName} size="small" color="primary" />}
              </Stack>
            )}
            {canPickEvent ? (
              <Stack direction="row" spacing={1} sx={{ flexWrap: 'wrap' }}>
                <Button
                  variant={showsThisEvent ? 'contained' : 'outlined'}
                  size="small"
                  disabled={showsThisEvent}
                  onClick={() => change({ eventId, pinnedEventGameId: null })}
                >
                  {SHOW_THIS_EVENT_LABEL}
                </Button>
                <Button
                  variant={followsFeatured ? 'contained' : 'outlined'}
                  size="small"
                  disabled={followsFeatured}
                  onClick={() => change({ eventId: null, pinnedEventGameId: null })}
                >
                  {FOLLOW_FEATURED_LABEL}
                </Button>
              </Stack>
            ) : (
              <Alert severity="info">{EVENT_CHOICE_LOCKED_NOTICE}</Alert>
            )}
            <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2}>
              <TextField
                select
                size="small"
                label={SCOPE_LABEL}
                value={settings.defaultScope}
                onChange={(e) => change({ defaultScope: e.target.value as TwitchExtensionScope })}
                sx={{ minWidth: 220 }}
              >
                {TWITCH_EXTENSION_SCOPES.map((scope) => (
                  <MenuItem key={scope} value={scope}>
                    {TWITCH_EXTENSION_SCOPE_LABELS[scope]}
                  </MenuItem>
                ))}
              </TextField>
              {settings.defaultScope === PINNED_SCOPE && (
                <TextField
                  select
                  size="small"
                  label={PINNED_GAME_LABEL}
                  value={settings.pinnedEventGameId ?? ''}
                  onChange={(e) => change({ pinnedEventGameId: e.target.value || null })}
                  sx={{ minWidth: 220 }}
                >
                  {pinnableGames.map((game) => (
                    <MenuItem key={game.eventGameId} value={game.eventGameId}>
                      {game.gameName}
                      {game.isEnabled ? ' (active)' : ''}
                    </MenuItem>
                  ))}
                </TextField>
              )}
            </Stack>
            <Stack>
              <FormControlLabel
                control={
                  <Switch
                    checked={settings.highlightChannelCompetitor}
                    onChange={(e) => change({ highlightChannelCompetitor: e.target.checked })}
                  />
                }
                label={HIGHLIGHT_LABEL}
              />
              <FormControlLabel
                control={
                  <Switch
                    checked={settings.showTrialProgress}
                    onChange={(e) => change({ showTrialProgress: e.target.checked })}
                  />
                }
                label={TRIAL_LABEL}
              />
            </Stack>
            <Stack direction="row" spacing={2} sx={{ alignItems: 'center', flexWrap: 'wrap' }}>
              <Button
                variant="contained"
                onClick={() => void save()}
                disabled={
                  !draft ||
                  update.isPending ||
                  (settings.defaultScope === PINNED_SCOPE && !settings.pinnedEventGameId)
                }
              >
                {SAVE_LABEL}
              </Button>
              {saved && <Typography variant="body2">{SAVED_LABEL}</Typography>}
            </Stack>
            {update.isError && <Alert severity="error">{SAVE_ERROR}</Alert>}
          </Stack>
        )}
      </Paper>
    </Box>
  )
}
