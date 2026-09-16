import Box from '@mui/material/Box'
import Stack from '@mui/material/Stack'
import Typography from '@mui/material/Typography'
import type { OverlayTokenSettings } from '../../../types/overlay'
import type { SampleGame } from '../overlay/overlaySampleData'
import { OverlayPreview } from './OverlayPreview'
import {
  OverlaySettingsForm,
  type OverlaySettingsCompetitor,
  type OverlaySettingsGame,
} from './OverlaySettingsForm'

/** A game as both halves of the designer need it: the form's picker and the preview's sample data. */
export type OverlayDesignerGame = OverlaySettingsGame & SampleGame

interface OverlayDesignerProps {
  eventId: string
  eventName: string
  value: OverlayTokenSettings
  onChange: (next: OverlayTokenSettings) => void
  competitors?: OverlaySettingsCompetitor[]
  games: OverlayDesignerGame[]
}

export const SETTINGS_HEADING = 'Look'
export const PREVIEW_HEADING = 'Preview'

/**
 * The settings form beside a live preview of what those settings produce,
 * used by both the create-token and edit-token dialogs. Controlled so the
 * dialog can save what is on screen.
 */
export function OverlayDesigner({
  eventId,
  eventName,
  value,
  onChange,
  competitors,
  games,
}: OverlayDesignerProps) {
  return (
    <Stack direction={{ xs: 'column', md: 'row' }} spacing={3}>
      <Box sx={{ flex: { md: '0 0 40%' }, minWidth: 0 }}>
        <Typography variant="subtitle2" sx={{ mb: 1 }}>
          {SETTINGS_HEADING}
        </Typography>
        <OverlaySettingsForm
          value={value}
          onChange={onChange}
          competitors={competitors}
          games={games}
        />
      </Box>
      <Box sx={{ flex: 1, minWidth: 0 }}>
        <Typography variant="subtitle2" sx={{ mb: 1 }}>
          {PREVIEW_HEADING}
        </Typography>
        <OverlayPreview eventId={eventId} eventName={eventName} games={games} settings={value} />
      </Box>
    </Stack>
  )
}
