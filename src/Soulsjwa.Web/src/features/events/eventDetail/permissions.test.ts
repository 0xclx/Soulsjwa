import { describe, expect, it } from 'vitest'
import {
  canEditCompetitorInfo,
  canManageEvent,
  canToggleFor,
  isEventMember,
  isoToLocalDateTime,
} from './permissions'
import type { EventResponse, User } from '../../../types'

const makeUser = (over: Partial<User> = {}): User => ({
  id: 'u1',
  twitchLogin: 'user1',
  displayName: 'User One',
  createdAt: '2024-01-01T00:00:00Z',
  role: 'User',
  isAllowlisted: true,
  ...over,
})

const makeEvent = (over: Partial<EventResponse> = {}): EventResponse => ({
  id: 'e1',
  name: 'Event',
  urlAlias: null,
  description: '',
  createdById: 'owner',
  isArchived: false,
  isStarted: false,
  isFeatured: false,
  allowTrialRuns: true,
  tieBreakMode: 'ByTime',
  createdAt: '2024-01-01T00:00:00Z',
  updatedAt: '2024-01-01T00:00:00Z',
  competitors: [],
  games: [],
  ...over,
})

describe('canToggleFor', () => {
  it('returns false when event, user, or target is missing', () => {
    expect(canToggleFor(undefined, makeUser(), 'u1')).toBe(false)
    expect(canToggleFor(makeEvent(), undefined, 'u1')).toBe(false)
    expect(canToggleFor(makeEvent(), makeUser(), '')).toBe(false)
  })

  it('allows a user to toggle their own completion', () => {
    expect(canToggleFor(makeEvent(), makeUser({ id: 'u1' }), 'u1')).toBe(true)
  })

  it('allows a global admin to toggle for anyone', () => {
    expect(canToggleFor(makeEvent(), makeUser({ id: 'admin', role: 'Admin' }), 'someone')).toBe(
      true,
    )
  })

  it('allows a moderator to toggle for a delegated streamer competitor', () => {
    const event = makeEvent({
      competitors: [
        {
          userId: 'streamer',
          displayName: 'Streamer',
          joinedAt: '2024-01-01T00:00:00Z',
          isStreamer: true,
          isLive: false,
          moderators: [{ userId: 'mod', displayName: 'Mod', addedAt: '2024-01-01T00:00:00Z' }],
        },
      ],
    })
    expect(canToggleFor(event, makeUser({ id: 'mod' }), 'streamer')).toBe(true)
  })

  it('denies a moderator when the competitor is not a streamer', () => {
    const event = makeEvent({
      competitors: [
        {
          userId: 'comp',
          displayName: 'Comp',
          joinedAt: '2024-01-01T00:00:00Z',
          isStreamer: false,
          isLive: false,
          moderators: [{ userId: 'mod', displayName: 'Mod', addedAt: '2024-01-01T00:00:00Z' }],
        },
      ],
    })
    expect(canToggleFor(event, makeUser({ id: 'mod' }), 'comp')).toBe(false)
  })

  it('denies an unrelated user', () => {
    const event = makeEvent({
      competitors: [
        {
          userId: 'streamer',
          displayName: 'Streamer',
          joinedAt: '2024-01-01T00:00:00Z',
          isStreamer: true,
          isLive: false,
          moderators: [],
        },
      ],
    })
    expect(canToggleFor(event, makeUser({ id: 'stranger' }), 'streamer')).toBe(false)
  })
})

describe('canEditCompetitorInfo', () => {
  it('returns false without a user', () => {
    expect(canEditCompetitorInfo(makeEvent(), undefined, 'u1')).toBe(false)
  })

  it('allows global admins even without an event', () => {
    expect(canEditCompetitorInfo(undefined, makeUser({ role: 'Admin' }), 'someone')).toBe(true)
  })

  it('allows the event creator and the target themselves', () => {
    expect(
      canEditCompetitorInfo(
        makeEvent({ createdById: 'owner' }),
        makeUser({ id: 'owner' }),
        'other',
      ),
    ).toBe(true)
    expect(canEditCompetitorInfo(makeEvent(), makeUser({ id: 'u1' }), 'u1')).toBe(true)
  })

  it('allows a moderator delegated to a streamer competitor', () => {
    const event = makeEvent({
      competitors: [
        {
          userId: 'streamer',
          displayName: 'Streamer',
          joinedAt: '2024-01-01T00:00:00Z',
          isStreamer: true,
          isLive: false,
          moderators: [{ userId: 'mod', displayName: 'Mod', addedAt: '2024-01-01T00:00:00Z' }],
        },
      ],
    })
    expect(canEditCompetitorInfo(event, makeUser({ id: 'mod' }), 'streamer')).toBe(true)
  })

  it('denies an unrelated user', () => {
    expect(canEditCompetitorInfo(makeEvent(), makeUser({ id: 'stranger' }), 'target')).toBe(false)
  })
})

describe('canManageEvent', () => {
  it('returns false without an event or user', () => {
    expect(canManageEvent(undefined, makeUser())).toBe(false)
    expect(canManageEvent(makeEvent(), undefined)).toBe(false)
  })

  it('allows a non-creator admin', () => {
    expect(
      canManageEvent(makeEvent({ createdById: 'owner' }), makeUser({ id: 'admin', role: 'Admin' })),
    ).toBe(true)
  })

  it('allows the creator', () => {
    expect(canManageEvent(makeEvent({ createdById: 'owner' }), makeUser({ id: 'owner' }))).toBe(
      true,
    )
  })

  it('denies a plain competitor', () => {
    const event = makeEvent({
      createdById: 'owner',
      competitors: [
        {
          userId: 'comp',
          displayName: 'Comp',
          joinedAt: '2024-01-01T00:00:00Z',
          isStreamer: false,
          isLive: false,
          moderators: [],
        },
      ],
    })
    expect(canManageEvent(event, makeUser({ id: 'comp' }))).toBe(false)
  })

  it('denies an anonymous visitor', () => {
    expect(canManageEvent(makeEvent(), undefined)).toBe(false)
  })
})

describe('isEventMember', () => {
  it('returns false without event or user', () => {
    expect(isEventMember(undefined, makeUser())).toBe(false)
    expect(isEventMember(makeEvent(), undefined)).toBe(false)
  })

  it('treats admins, creators, competitors, and moderators as members', () => {
    expect(isEventMember(makeEvent(), makeUser({ id: 'x', role: 'Admin' }))).toBe(true)
    expect(isEventMember(makeEvent({ createdById: 'owner' }), makeUser({ id: 'owner' }))).toBe(true)

    const withCompetitor = makeEvent({
      competitors: [
        {
          userId: 'comp',
          displayName: 'Comp',
          joinedAt: '2024-01-01T00:00:00Z',
          isStreamer: false,
          isLive: false,
          moderators: [{ userId: 'mod', displayName: 'Mod', addedAt: '2024-01-01T00:00:00Z' }],
        },
      ],
    })
    expect(isEventMember(withCompetitor, makeUser({ id: 'comp' }))).toBe(true)
    expect(isEventMember(withCompetitor, makeUser({ id: 'mod' }))).toBe(true)
  })

  it('denies a stranger', () => {
    expect(isEventMember(makeEvent(), makeUser({ id: 'stranger' }))).toBe(false)
  })
})

describe('isoToLocalDateTime', () => {
  it('returns an empty string for invalid input', () => {
    expect(isoToLocalDateTime('not-a-date')).toBe('')
  })

  it('produces a datetime-local compatible string', () => {
    const result = isoToLocalDateTime('2024-03-04T05:06:00Z')
    expect(result).toMatch(/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}$/)
  })
})
