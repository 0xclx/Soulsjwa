import type { EventResponse, User } from '../../../types'

/**
 * Mirrors `EventOwnership.RequireCanCompleteForStreamerAsync`. The non-obvious
 * arm: a delegated moderator may tick for a competitor, but only one flagged
 * as a streamer in this event.
 */
export function canToggleFor(
  event: EventResponse | undefined,
  user: User | undefined,
  targetUserId: string,
): boolean {
  if (!event || !user || !targetUserId) return false
  if (user.id === targetUserId) return true
  if (user.role === 'Admin') return true
  const competitor = event.competitors.find((c) => c.userId === targetUserId && c.isStreamer)
  if (!competitor) return false
  return competitor.moderators.some((m) => m.userId === user.id)
}

/**
 * Mirrors `EventOwnership.RequireCanEditCompetitorInfoAsync` — who may
 * add/remove competitor metadata (death clips, links, notes).
 */
export function canEditCompetitorInfo(
  event: EventResponse | undefined,
  user: User | undefined,
  targetUserId: string,
): boolean {
  if (!user) return false
  if (user.role === 'Admin') return true
  if (event && event.createdById === user.id) return true
  if (user.id === targetUserId) return true
  if (!event) return false
  const competitor = event.competitors.find((c) => c.userId === targetUserId && c.isStreamer)
  return !!competitor?.moderators.some((m) => m.userId === user.id)
}

/** Gates members-only surfaces such as the activity log. */
export function isEventMember(event: EventResponse | undefined, user: User | undefined): boolean {
  if (!event || !user) return false
  if (user.role === 'Admin') return true
  if (event.createdById === user.id) return true
  if (event.competitors.some((c) => c.userId === user.id)) return true
  return event.competitors.some((c) => c.moderators.some((m) => m.userId === user.id))
}

/**
 * Mirrors `EventOwnership.RequireOwner`. Unlike `isOwner` (creator-only),
 * this is the check every management surface should gate on, so a
 * non-creator admin can fully manage the event.
 */
export function canManageEvent(event: EventResponse | undefined, user: User | undefined): boolean {
  if (!event || !user) return false
  if (user.role === 'Admin') return true
  return event.createdById === user.id
}

/**
 * Objectives are editable only while the event is stopped (which includes one
 * never started). Starting or archiving locks them so live scoring and
 * historical results stay stable.
 */
export function canManageObjectives(event: EventResponse | undefined): boolean {
  if (!event) return false
  return !event.isStarted && !event.isArchived
}

/**
 * Convert an ISO timestamp into the `YYYY-MM-DDTHH:mm` shape expected by
 * `<input type="datetime-local">`, in the viewer's local timezone. Returns an
 * empty string for unparseable input.
 */
export function isoToLocalDateTime(value: string): string {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return ''
  const local = new Date(date.getTime() - date.getTimezoneOffset() * 60000)
  return local.toISOString().slice(0, 16)
}
