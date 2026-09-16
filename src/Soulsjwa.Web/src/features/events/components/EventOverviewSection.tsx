import Stack from '@mui/material/Stack'
import { EventScoreboardPreview } from './EventScoreboardPreview'
import type { ScoreEntry } from '../../../types'

interface EventOverviewSectionProps {
  eventId: string
  scores: ScoreEntry[] | undefined
}

/**
 * Overview tab: a compact scoreboard preview. OBS overlay tokens live in their
 * own dedicated tab.
 */
export const EventOverviewSection = ({ eventId, scores }: EventOverviewSectionProps) => (
  <Stack spacing={3}>
    {scores && scores.length > 0 && <EventScoreboardPreview eventId={eventId} scores={scores} />}
  </Stack>
)
