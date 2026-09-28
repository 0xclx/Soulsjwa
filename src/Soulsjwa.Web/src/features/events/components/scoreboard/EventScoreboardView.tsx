import { useMemo, useState, type ReactNode } from 'react'
import Button from '@mui/material/Button'
import Stack from '@mui/material/Stack'
import Typography from '@mui/material/Typography'
import { useTheme } from '@mui/material/styles'
import useMediaQuery from '@mui/material/useMediaQuery'
import { useLiveEvent } from '../../hooks/useLiveEvent'
import { useScoreboard } from '../../hooks/useScoreboard'
import { useScoreboardView } from '../../hooks/useScoreboardView'
import {
  activeGameRows,
  gameIsDone,
  matchesCompetitorSearch,
  showsTrial,
} from '../../scoreboard/scoreboardMetrics'
import { canEditCompetitorInfo } from '../../eventDetail/permissions'
import { CompetitorGameDialog } from './CompetitorGameDialog'
import { CompetitorSearchField } from './CompetitorSearchField'
import { EventGameMatrix } from './EventGameMatrix'
import type { ScoreboardView } from '../../scoreboard/scoreboardView'
import { ActiveGameHeader } from './ActiveGameHeader'
import { ActiveGameStandings } from './ActiveGameStandings'
import { PreStartOverview } from './PreStartOverview'
import { ScoreboardViewSwitch } from './ScoreboardViewSwitch'
import {
  CardListSkeleton,
  EmptyState,
  ErrorMessage,
  TableSkeleton,
} from '../../../../components/ui'
import type { EventResponse, User } from '../../../../types'

interface EventScoreboardViewProps {
  eventId: string
  /** The event as the surface loaded it; re-read live from here on. */
  event: EventResponse
  /** Undefined for an anonymous visitor. Passed straight through to the
   * per-competitor cards/rows for their own owner/admin/self gating — this
   * component itself never branches on it, so the scoreboard renders
   * identically for anonymous and authenticated visitors. */
  currentUser: User | undefined
}

/**
 * Renders one event's scoreboard: the pre-start overview until the event
 * starts, then loading/empty/error states, then either cards (narrow) or a
 * table (wide) of entries. Shared by EventScoreboardPage,
 * the home page's featured scoreboard, and the public scoreboard deep link
 * (PublicScoreboardPage) so the three surfaces can never drift apart.
 */
export const EventScoreboardView = ({
  eventId,
  event: loadedEvent,
  currentUser,
}: EventScoreboardViewProps) => {
  const event = useLiveEvent(loadedEvent)
  const {
    data: scoreboard,
    isLoading,
    isError,
  } = useScoreboard(eventId, {
    live: event.isStarted,
    enabled: event.isStarted,
  })
  const theme = useTheme()
  const isWide = useMediaQuery(theme.breakpoints.up('md'))
  const [requestedView, setView] = useScoreboardView()
  const activeEventGame = event.games.find((game) => game.isEnabled)
  // Without an enabled game there is no current game to show, whatever the URL says.
  const view: ScoreboardView = activeEventGame ? requestedView : 'event'
  const activeRows = useMemo(
    () =>
      activeEventGame && scoreboard
        ? activeGameRows(scoreboard.entries, activeEventGame.eventGameId)
        : [],
    [activeEventGame, scoreboard],
  )

  // Local, so it survives switching views; never in the URL.
  const [query, setQuery] = useState('')
  const entries = scoreboard?.entries ?? []
  const matchCount = entries.filter((entry) => matchesCompetitorSearch(entry, query)).length

  // Only ids are kept, so each poll re-derives what an open dialog shows.
  const [selected, setSelected] = useState<{ userId: string; eventGameId: string } | null>(null)
  const selectedEntry = selected
    ? scoreboard?.entries.find((entry) => entry.userId === selected.userId)
    : undefined
  const selectedGame = selectedEntry?.games.find(
    (game) => game.eventGameId === selected?.eventGameId,
  )
  const select = (userId: string, eventGameId: string) => setSelected({ userId, eventGameId })

  if (!event.isStarted) {
    return <PreStartOverview event={event} />
  }

  const content = (): ReactNode => {
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
      return <EmptyState title="No competitors yet" />
    }

    if (matchCount === 0) {
      return <NoMatch query={query} onClear={() => setQuery('')} />
    }

    if (view === 'game' && activeEventGame) {
      return (
        <Stack spacing={2}>
          <ActiveGameHeader
            gameName={activeEventGame.gameName}
            objectiveCount={activeEventGame.objectives.length}
            doneCount={activeRows.filter((row) => gameIsDone(row.game)).length}
            competitorCount={activeRows.length}
          />
          <ActiveGameStandings
            entries={scoreboard.entries}
            eventGameId={activeEventGame.eventGameId}
            query={query}
            onSelect={select}
          />
        </Stack>
      )
    }

    return (
      <EventGameMatrix
        entries={scoreboard.entries}
        games={event.games}
        query={query}
        onSelect={select}
      />
    )
  }
  return (
    <Stack spacing={2}>
      {(activeEventGame || entries.length > 0) && (
        <Stack
          direction={{ xs: 'column', md: 'row' }}
          spacing={2}
          useFlexGap
          sx={{ alignItems: { md: 'center' } }}
        >
          {activeEventGame && <ScoreboardViewSwitch view={view} onChange={setView} />}
          {entries.length > 0 && (
            <CompetitorSearchField
              query={query}
              onChange={setQuery}
              matchCount={matchCount}
              totalCount={entries.length}
            />
          )}
        </Stack>
      )}
      {content()}
      {selectedEntry && selectedGame && (
        <CompetitorGameDialog
          open
          onClose={() => setSelected(null)}
          entry={selectedEntry}
          game={selectedGame}
          eventId={eventId}
          canEdit={canEditCompetitorInfo(event, currentUser, selectedEntry.userId)}
          showTrial={showsTrial(selectedGame, activeEventGame ? [activeEventGame.eventGameId] : [])}
        />
      )}
    </Stack>
  )
}

const NoMatch = ({ query, onClear }: { query: string; onClear: () => void }) => (
  <Stack spacing={1} sx={{ alignItems: 'flex-start' }}>
    <Typography>No competitor matches &apos;{query.trim()}&apos;</Typography>
    <Button size="small" onClick={onClear}>
      Clear search
    </Button>
  </Stack>
)
