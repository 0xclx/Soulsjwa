import { useCallback, useMemo, useState } from 'react'
import { canToggleFor } from '../eventDetail/permissions'
import { getErrorDetail } from '../../../lib/getErrorDetail'
import { useCompleteObjective } from './useCompleteObjective'
import { useUncompleteObjective } from './useUncompleteObjective'
import { useFailObjective } from './useFailObjective'
import { useResetFailedObjective } from './useResetFailedObjective'
import { useFailRemainingObjectives } from './useFailRemainingObjectives'
import { trialBlockReason } from '../scoreboard/trialPresentation'
import type { EventCompetitor, EventResponse, ScoreboardResponse, User } from '../../../types'

/**
 * Encapsulates the "which competitor am I updating, and what have they already
 * completed/failed" state that drives objective checkboxes and manual
 * fail/reset controls on the games tab. Mirrors the server authorization
 * rules and derives outcome state from the scoreboard payload.
 */
export const useObjectiveCompletion = (
  eventId: string,
  event: EventResponse | undefined,
  currentUser: User | undefined,
  scoreboard: ScoreboardResponse | undefined,
) => {
  const [completionTarget, setCompletionTarget] = useState<string>('')
  const [completionError, setCompletionError] = useState<string | null>(null)

  const completeObjective = useCompleteObjective(eventId)
  const uncompleteObjective = useUncompleteObjective(eventId)
  const failObjective = useFailObjective(eventId)
  const resetFailedObjective = useResetFailedObjective(eventId)
  const failRemainingObjectives = useFailRemainingObjectives(eventId)

  const competitors = useMemo<EventCompetitor[]>(
    () => event?.competitors ?? [],
    [event?.competitors],
  )
  const selfIsCompetitor = !!(currentUser && competitors.some((c) => c.userId === currentUser.id))

  const completionOptions = useMemo(
    () => competitors.filter((c) => canToggleFor(event, currentUser, c.userId)),
    [competitors, currentUser, event],
  )

  const effectiveTarget = useMemo(() => {
    if (completionTarget && completionOptions.some((c) => c.userId === completionTarget))
      return completionTarget
    if (
      selfIsCompetitor &&
      currentUser &&
      completionOptions.some((c) => c.userId === currentUser.id)
    )
      return currentUser.id
    return completionOptions[0]?.userId ?? ''
  }, [completionTarget, completionOptions, selfIsCompetitor, currentUser])

  const completedObjectiveIds = useMemo(() => {
    const set = new Set<string>()
    if (!effectiveTarget || !scoreboard) return set
    const entry = scoreboard.entries.find((e) => e.userId === effectiveTarget)
    entry?.games.forEach((g) =>
      g.objectives.forEach((o) => o.isCompleted && set.add(o.objectiveId)),
    )
    return set
  }, [scoreboard, effectiveTarget])

  const completedObjectiveTimes = useMemo(() => {
    const map = new Map<string, string>()
    if (!effectiveTarget || !scoreboard) return map
    const entry = scoreboard.entries.find((e) => e.userId === effectiveTarget)
    entry?.games.forEach((g) =>
      g.objectives.forEach((o) => {
        if (o.isCompleted && o.completedAt) map.set(o.objectiveId, o.completedAt)
      }),
    )
    return map
  }, [scoreboard, effectiveTarget])

  const failedObjectiveIds = useMemo(() => {
    const set = new Set<string>()
    if (!effectiveTarget || !scoreboard) return set
    const entry = scoreboard.entries.find((e) => e.userId === effectiveTarget)
    entry?.games.forEach((g) => g.objectives.forEach((o) => o.isFailed && set.add(o.objectiveId)))
    return set
  }, [scoreboard, effectiveTarget])

  const failedObjectiveTimes = useMemo(() => {
    const map = new Map<string, string>()
    if (!effectiveTarget || !scoreboard) return map
    const entry = scoreboard.entries.find((e) => e.userId === effectiveTarget)
    entry?.games.forEach((g) =>
      g.objectives.forEach((o) => {
        if (o.isFailed && o.failedAt) map.set(o.objectiveId, o.failedAt)
      }),
    )
    return map
  }, [scoreboard, effectiveTarget])

  /**
   * Games where trial mode is on for the target competitor, in any state —
   * the same condition the server refuses official writes on. A recording run
   * would take the tick (which this official-only tab then filters straight
   * back out, leaving the checkbox unmoved and the next click 409-ing); a
   * dormant one takes nothing at all. Either way the controls are read-only
   * here until trial mode is turned off.
   */
  const trialBlockReasons = useMemo(() => {
    const reasons = new Map<string, string>()
    if (!effectiveTarget || !scoreboard) return reasons
    const entry = scoreboard.entries.find((e) => e.userId === effectiveTarget)
    entry?.games.forEach((game) => {
      const reason = trialBlockReason(game)
      if (reason) reasons.set(game.eventGameId, reason)
    })
    return reasons
  }, [scoreboard, effectiveTarget])

  const canToggleCompletion = useMemo(
    () => canToggleFor(event, currentUser, effectiveTarget),
    [event, currentUser, effectiveTarget],
  )

  const toggleDisabled =
    !event?.isStarted ||
    completeObjective.isPending ||
    uncompleteObjective.isPending ||
    failObjective.isPending ||
    resetFailedObjective.isPending ||
    failRemainingObjectives.isPending

  const handleToggleCompletion = useCallback(
    (eventGameId: string) => (objectiveId: string, currentlyCompleted: boolean) => {
      setCompletionError(null)
      const onBehalfOfUserId =
        effectiveTarget && currentUser && effectiveTarget !== currentUser.id
          ? effectiveTarget
          : undefined
      const onError = (err: unknown) =>
        setCompletionError(getErrorDetail(err, 'Failed to update completion.'))
      if (currentlyCompleted) {
        uncompleteObjective.mutate({ eventGameId, objectiveId, onBehalfOfUserId }, { onError })
      } else {
        completeObjective.mutate({ eventGameId, objectiveId, onBehalfOfUserId }, { onError })
      }
    },
    [effectiveTarget, currentUser, completeObjective, uncompleteObjective],
  )

  const handleToggleFailure = useCallback(
    (eventGameId: string) => (objectiveId: string, currentlyFailed: boolean) => {
      setCompletionError(null)
      const onBehalfOfUserId =
        effectiveTarget && currentUser && effectiveTarget !== currentUser.id
          ? effectiveTarget
          : undefined
      const onError = (err: unknown) =>
        setCompletionError(getErrorDetail(err, 'Failed to update failure status.'))
      if (currentlyFailed) {
        resetFailedObjective.mutate({ eventGameId, objectiveId, onBehalfOfUserId }, { onError })
      } else {
        failObjective.mutate({ eventGameId, objectiveId, onBehalfOfUserId }, { onError })
      }
    },
    [effectiveTarget, currentUser, failObjective, resetFailedObjective],
  )

  /**
   * Fails every objective of the game still pending for the target. Resolves
   * either way — the error, if any, is reported through `completionError`
   * like the single-objective writes — so the confirm dialog can close on it.
   */
  const handleFailRemaining = useCallback(
    (eventGameId: string) => {
      setCompletionError(null)
      const onBehalfOfUserId =
        effectiveTarget && currentUser && effectiveTarget !== currentUser.id
          ? effectiveTarget
          : undefined
      return failRemainingObjectives
        .mutateAsync({ eventGameId, onBehalfOfUserId })
        .catch((err: unknown) =>
          setCompletionError(getErrorDetail(err, 'Failed to fail the remaining objectives.')),
        )
    },
    [effectiveTarget, currentUser, failRemainingObjectives],
  )

  return {
    selfIsCompetitor,
    completionOptions,
    effectiveTarget,
    setCompletionTarget,
    completedObjectiveIds,
    completedObjectiveTimes,
    failedObjectiveIds,
    failedObjectiveTimes,
    trialBlockReasons,
    canToggleCompletion,
    toggleDisabled,
    completionError,
    setCompletionError,
    handleToggleCompletion,
    handleToggleFailure,
    handleFailRemaining,
    failRemainingPending: failRemainingObjectives.isPending,
  }
}
