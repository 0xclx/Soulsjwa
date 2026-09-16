import Stack from '@mui/material/Stack'
import { useTheme } from '@mui/material/styles'
import useMediaQuery from '@mui/material/useMediaQuery'
import { useScoreboard } from '../../hooks/useScoreboard'
import { ScoreboardCard } from './ScoreboardCard'
import { ScoreboardTable } from './ScoreboardTable'
import {
  CardListSkeleton,
  EmptyState,
  ErrorMessage,
  TableSkeleton,
} from '../../../../components/ui'
import type { EventResponse, User } from '../../../../types'

interface EventScoreboardViewProps {
  eventId: string
  event: EventResponse | undefined
  /** Undefined for an anonymous visitor. Passed straight through to the
   * per-competitor cards/rows for their own owner/admin/self gating — this
   * component itself never branches on it, so the scoreboard renders
   * identically for anonymous and authenticated visitors. */
  currentUser: User | undefined
}

/**
 * Renders one event's scoreboard: loading/empty/error states, then either
 * cards (narrow) or a table (wide) of entries. Shared by EventScoreboardPage,
 * the home page's featured scoreboard, and the public scoreboard deep link
 * (PublicScoreboardPage) so the three surfaces can never drift apart.
 */
export const EventScoreboardView = ({ eventId, event, currentUser }: EventScoreboardViewProps) => {
  const { data: scoreboard, isLoading, isError } = useScoreboard(eventId, event?.isStarted ?? false)
  const theme = useTheme()
  const isWide = useMediaQuery(theme.breakpoints.up('md'))

  if (isLoading) {
    return isWide ? (
      <TableSkeleton rows={6} columns={6} label="Loading scoreboard…" />
    ) : (
      <CardListSkeleton count={4} label="Loading scoreboard…" />
    )
  }

  if (isError || !scoreboard) {
    return <ErrorMessage message="Failed to load scoreboard." />
  }

  if (scoreboard.entries.length === 0) {
    return (
      <EmptyState
        title="No scores yet"
        description="Check back once competitors start completing objectives."
      />
    )
  }

  if (isWide) {
    return (
      <ScoreboardTable
        entries={scoreboard.entries}
        eventId={eventId}
        event={event}
        currentUser={currentUser}
      />
    )
  }

  return (
    <Stack spacing={2}>
      {scoreboard.entries.map((entry) => (
        <ScoreboardCard
          key={entry.userId}
          entry={entry}
          eventId={eventId}
          event={event}
          currentUser={currentUser}
        />
      ))}
    </Stack>
  )
}
