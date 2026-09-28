import IconButton from '@mui/material/IconButton'
import { TwitchIcon } from './TwitchIcon'

const TWITCH_CHANNEL_BASE_URL = 'https://twitch.tv/'

interface TwitchLinkProps {
  twitchLogin: string
  displayName: string
}

/**
 * Icon link to a competitor's Twitch channel. Stops the click from reaching
 * a clickable row or card around it.
 */
export const TwitchLink = ({ twitchLogin, displayName }: TwitchLinkProps) => (
  <IconButton
    component="a"
    href={`${TWITCH_CHANNEL_BASE_URL}${twitchLogin}`}
    target="_blank"
    rel="noopener noreferrer"
    size="small"
    aria-label={`Watch ${displayName} on Twitch`}
    title={`Watch ${displayName} on Twitch`}
    onClick={(e: React.MouseEvent) => e.stopPropagation()}
    onKeyDown={(e: React.KeyboardEvent) => e.stopPropagation()}
    sx={{ color: 'primary.main' }}
  >
    <TwitchIcon />
  </IconButton>
)
