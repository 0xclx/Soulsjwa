import { useCallback, useState } from 'react'
import { Link as RouterLink, Outlet, useLocation, useNavigate, useParams } from 'react-router-dom'
import Button from '@mui/material/Button'
import Stack from '@mui/material/Stack'
import ArrowBackIcon from '@mui/icons-material/ArrowBack'
import { useArchiveEvent } from '../features/events/hooks/useArchiveEvent'
import { useDuplicateEvent } from '../features/events/hooks/useDuplicateEvent'
import { useEvent } from '../features/events/hooks/useEvent'
import { useFeatureEvent } from '../features/events/hooks/useFeatureEvent'
import { useStartEvent } from '../features/events/hooks/useStartEvent'
import { useStopEvent } from '../features/events/hooks/useStopEvent'
import { useUnarchiveEvent } from '../features/events/hooks/useUnarchiveEvent'
import { useUnfeatureEvent } from '../features/events/hooks/useUnfeatureEvent'
import { useEventRules } from '../features/events/hooks/useEventRules'
import { useUpdateEvent } from '../features/events/hooks/useUpdateEvent'
import { useCurrentUser } from '../features/users/hooks/useCurrentUser'
import { EditEventDialog } from '../features/events/components/EditEventDialog'
import { EventDetailHeader } from '../features/events/components/EventDetailHeader'
import { EventSectionTabs } from '../features/events/components/EventSectionTabs'
import { getEventSection } from '../features/events/eventDetail/sections'
import { canManageEvent, isEventMember } from '../features/events/eventDetail/permissions'
import type { EventRouteContext } from '../features/events/hooks/useEventRoute'
import { getEventUrlIdentifier } from '../features/events/eventUrl'
import type { EventResponse } from '../types'
import { ConfirmDialog, ErrorMessage, LoadingState } from './ui'

export const EventLayout = () => {
  const { id } = useParams<{ id: string }>()
  const eventIdentifier = id!
  const location = useLocation()
  const navigate = useNavigate()
  const { data: event, isLoading, isError } = useEvent(eventIdentifier)
  const eventId = event?.id ?? eventIdentifier
  const { data: currentUser } = useCurrentUser()
  const { data: rules } = useEventRules(eventId)
  const startEvent = useStartEvent(eventId)
  const stopEvent = useStopEvent(eventId)
  const updateEvent = useUpdateEvent(eventId)
  const archiveEvent = useArchiveEvent()
  const unarchiveEvent = useUnarchiveEvent()
  const featureEvent = useFeatureEvent(eventId)
  const unfeatureEvent = useUnfeatureEvent(eventId)
  const duplicateEvent = useDuplicateEvent()
  const [isEditing, setIsEditing] = useState(false)
  const [archiveConfirmationOpen, setArchiveConfirmationOpen] = useState(false)

  const isOwner = !!(currentUser && event && currentUser.id === event.createdById)
  const isAdmin = currentUser?.role === 'Admin'

  const handleStartStop = useCallback(() => {
    if (!event) return
    if (event.isStarted) void stopEvent.mutateAsync()
    else void startEvent.mutateAsync()
  }, [event, startEvent, stopEvent])

  const handleArchive = useCallback(() => {
    if (!event) return
    setArchiveConfirmationOpen(false)
    archiveEvent.mutate(event.id, { onSuccess: () => navigate('/events') })
  }, [archiveEvent, event, navigate])

  const handleUnarchive = useCallback(() => {
    if (event) unarchiveEvent.mutate(event.id)
  }, [event, unarchiveEvent])

  const handleFeatureToggle = useCallback(() => {
    if (!event) return
    if (event.isFeatured) void unfeatureEvent.mutateAsync()
    else void featureEvent.mutateAsync()
  }, [event, featureEvent, unfeatureEvent])

  const handleDuplicate = useCallback(() => {
    if (!event) return
    duplicateEvent.mutate(event.id, {
      onSuccess: (copy) => navigate(`/events/${getEventUrlIdentifier(copy.id, copy.urlAlias)}`),
    })
  }, [duplicateEvent, event, navigate])

  const handleEventSaved = useCallback(
    (savedEvent: EventResponse) => {
      setIsEditing(false)
      const suffix = location.pathname.slice(`/events/${eventIdentifier}`.length)
      const nextIdentifier = getEventUrlIdentifier(savedEvent.id, savedEvent.urlAlias)
      navigate(`/events/${nextIdentifier}${suffix}${location.search}`, { replace: true })
    },
    [eventIdentifier, location.pathname, location.search, navigate],
  )

  if (isLoading) return <LoadingState label="Loading event…" />

  if (isError || !event) {
    return (
      <Stack spacing={2}>
        <ErrorMessage message="Failed to load event." />
        <Button component={RouterLink} to="/events" startIcon={<ArrowBackIcon />}>
          Back to events
        </Button>
      </Stack>
    )
  }

  const canViewActivity = isEventMember(event, currentUser)
  const canManage = canManageEvent(event, currentUser)
  const context: EventRouteContext = {
    eventId: event.id,
    eventUrlIdentifier: getEventUrlIdentifier(event.id, event.urlAlias),
    event,
    currentUser,
    isOwner,
    isAdmin,
    canManage,
    canViewActivity,
  }
  const selfIsCompetitor = event.competitors.some(
    (competitor) => competitor.userId === currentUser?.id,
  )

  return (
    <Stack spacing={4}>
      <EventDetailHeader
        event={event}
        canManage={canManage}
        isAdmin={isAdmin}
        isEditing={isEditing}
        startStopPending={startEvent.isPending || stopEvent.isPending}
        archivePending={archiveEvent.isPending}
        unarchivePending={unarchiveEvent.isPending}
        featurePending={featureEvent.isPending || unfeatureEvent.isPending}
        duplicatePending={duplicateEvent.isPending}
        onStartStop={handleStartStop}
        onEdit={() => setIsEditing(true)}
        onArchive={() => setArchiveConfirmationOpen(true)}
        onUnarchive={handleUnarchive}
        onFeatureToggle={handleFeatureToggle}
        onDuplicate={handleDuplicate}
      />
      <EventSectionTabs
        eventId={getEventUrlIdentifier(event.id, event.urlAlias)}
        activeSection={getEventSection(location.pathname)}
        canViewActivity={canViewActivity}
        canViewTokens={canManage || selfIsCompetitor}
        canViewRules={canManage || !!rules?.content?.trim()}
      />
      <Outlet context={context} />
      {isEditing && (
        <EditEventDialog
          open
          event={event}
          mutation={updateEvent}
          onClose={() => setIsEditing(false)}
          onSaved={handleEventSaved}
        />
      )}
      <ConfirmDialog
        open={archiveConfirmationOpen}
        title={`Archive ${event.name}?`}
        description="This event will be hidden from event listings. You can restore it later."
        confirmLabel="Archive event"
        pending={archiveEvent.isPending}
        onCancel={() => setArchiveConfirmationOpen(false)}
        onConfirm={handleArchive}
      />
    </Stack>
  )
}
