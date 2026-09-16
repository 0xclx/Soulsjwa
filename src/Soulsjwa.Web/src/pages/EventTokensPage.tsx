import Stack from '@mui/material/Stack'
import { ErrorMessage, LoadingState } from '../components/ui'
import { OverlayTokensSection } from '../features/events/components/OverlayTokensSection'
import { TwitchExtensionSection } from '../features/events/components/TwitchExtensionSection'
import { useEventRoute } from '../features/events/hooks/useEventRoute'
import { useEventScores } from '../features/events/hooks/useEventScores'
import { useScoreboard } from '../features/events/hooks/useScoreboard'
import { useCurrentUser } from '../features/users/hooks/useCurrentUser'

export const EventTokensPage = () => {
  const { eventId, eventUrlIdentifier, event, canManage } = useEventRoute()
  const { data: currentUser, isLoading: isLoadingUser } = useCurrentUser()
  const { data: scores } = useEventScores(eventId, event.isStarted)
  // Per-game trial state for the overlay link builder's "restrict to game"
  // picker — `scores` (above) is the lightweight per-competitor summary this
  // page otherwise needs and has no per-game breakdown, so the trial flag is
  // read from the full scoreboard instead.
  const { data: scoreboard } = useScoreboard(eventId, event.isStarted)

  if (isLoadingUser) {
    return <LoadingState label="Loading user…" />
  }

  const selfIsCompetitor = event.competitors.some(
    (competitor) => competitor.userId === currentUser?.id,
  )

  if (!canManage && !selfIsCompetitor) {
    return <ErrorMessage message="Overlay tokens are available to event competitors and owners." />
  }

  return (
    <Stack spacing={5}>
      <OverlayTokensSection
        eventId={eventId}
        eventUrlIdentifier={eventUrlIdentifier}
        eventName={event.name}
        canManageAll={canManage}
        currentUserId={currentUser?.id}
        competitors={scores?.map((score) => ({
          userId: score.userId,
          displayName: score.displayName,
        }))}
        games={event.games.map((game) => ({
          eventGameId: game.eventGameId,
          gameName: game.gameName,
          isEnabled: game.isEnabled,
          hasActiveTrial: !!scoreboard?.entries.some((entry) =>
            entry.games.some((g) => g.eventGameId === game.eventGameId && g.isTrialActive),
          ),
          objectives: game.objectives.map((objective) => ({
            id: objective.id,
            name: objective.name,
            score: objective.score,
            category: objective.category ?? null,
          })),
        }))}
      />
      <TwitchExtensionSection
        eventId={eventId}
        eventName={event.name}
        games={event.games.map((game) => ({
          eventGameId: game.eventGameId,
          gameName: game.gameName,
          isEnabled: game.isEnabled,
        }))}
      />
    </Stack>
  )
}
