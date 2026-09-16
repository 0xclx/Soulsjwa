import { useCallback, useState } from 'react'
import Alert from '@mui/material/Alert'
import Snackbar from '@mui/material/Snackbar'
import Stack from '@mui/material/Stack'
import {
  EditCompletionTimeDialog,
  type EditCompletionTarget,
} from '../features/events/components/EditCompletionTimeDialog'
import {
  EditObjectiveDialog,
  type EditObjectiveTarget,
} from '../features/events/components/EditObjectiveDialog'
import { EventGamesSection } from '../features/events/components/EventGamesSection'
import { useEditCompletionTime } from '../features/events/hooks/useEditCompletionTime'
import { useEditObjective } from '../features/events/hooks/useEditObjective'
import { useEventRoute } from '../features/events/hooks/useEventRoute'
import { useGames } from '../features/events/hooks/useGames'
import { useScoreboard } from '../features/events/hooks/useScoreboard'
import { useObjectiveCompletion } from '../features/events/hooks/useObjectiveCompletion'
import type { Objective } from '../types'

export const EventGamesPage = () => {
  const { eventId, event, currentUser, canManage, isAdmin } = useEventRoute()
  const { data: scoreboard } = useScoreboard(eventId, event.isStarted)
  const { data: games } = useGames()
  const editObjective = useEditObjective(eventId)
  const editCompletionTime = useEditCompletionTime(eventId)
  const completion = useObjectiveCompletion(eventId, event, currentUser, scoreboard)
  const [editingObjective, setEditingObjective] = useState<EditObjectiveTarget | null>(null)
  const [editingCompletion, setEditingCompletion] = useState<EditCompletionTarget | null>(null)
  const [successMessage, setSuccessMessage] = useState<string | null>(null)

  const handleOpenEditObjective = useCallback((gameId: string, objective: Objective) => {
    setEditingObjective({
      eventGameId: gameId,
      objectiveId: objective.id,
      name: objective.name,
      score: String(objective.score),
      category: objective.category ?? '',
      rule: objective.rule ?? '',
      failRule: objective.failRule ?? '',
    })
  }, [])

  const handleOpenEditCompletionTime = useCallback(
    (gameId: string, objectiveId: string) => {
      if (!completion.effectiveTarget) return
      const competitor = event.competitors.find(
        (candidate) => candidate.userId === completion.effectiveTarget,
      )
      const game = event.games.find((candidate) => candidate.eventGameId === gameId)
      const objective = game?.objectives.find((candidate) => candidate.id === objectiveId)
      const completedAt = completion.completedObjectiveTimes.get(objectiveId)
      if (!competitor || !objective || !completedAt) return

      setEditingCompletion({
        eventGameId: gameId,
        objectiveId,
        objectiveName: objective.name,
        competitorName: competitor.displayName,
        currentCompletedAt: completedAt,
      })
    },
    [event, completion.effectiveTarget, completion.completedObjectiveTimes],
  )

  return (
    <Stack spacing={3}>
      <EventGamesSection
        event={event}
        eventId={eventId}
        canManage={canManage}
        isAdmin={isAdmin}
        currentUserId={currentUser?.id}
        canEditCompletionTimes={canManage}
        knownGames={games ?? []}
        completion={completion}
        onEditObjective={handleOpenEditObjective}
        onEditCompletionTime={handleOpenEditCompletionTime}
      />

      {editingObjective && (
        <EditObjectiveDialog
          key={editingObjective.objectiveId}
          open
          target={editingObjective}
          game={event.games.find((g) => g.eventGameId === editingObjective.eventGameId)}
          mutation={editObjective}
          onClose={() => setEditingObjective(null)}
        />
      )}
      {editingCompletion && (
        <EditCompletionTimeDialog
          key={editingCompletion.objectiveId}
          open
          target={editingCompletion}
          userId={completion.effectiveTarget}
          mutation={editCompletionTime}
          onClose={() => setEditingCompletion(null)}
          onSuccess={() => {
            setSuccessMessage('Completion time updated.')
            setEditingCompletion(null)
          }}
        />
      )}
      <Snackbar
        open={!!successMessage}
        autoHideDuration={3000}
        onClose={() => setSuccessMessage(null)}
      >
        <Alert severity="success" onClose={() => setSuccessMessage(null)}>
          {successMessage}
        </Alert>
      </Snackbar>
    </Stack>
  )
}
