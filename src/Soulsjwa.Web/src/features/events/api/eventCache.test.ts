import { describe, it, expect, beforeEach } from 'vitest'
import { QueryClient } from '@tanstack/react-query'
import { invalidateEventScope } from './eventCache'
import { EVENTS_QUERY_KEYS } from './eventsApi'
import { OVERLAY_TOKENS_QUERY_KEYS } from './overlayTokensApi'

const EVENT_ID = 'event-1'

const seedQueries = (client: QueryClient) => {
  const keys = {
    detail: EVENTS_QUERY_KEYS.detail(EVENT_ID),
    scores: EVENTS_QUERY_KEYS.scores(EVENT_ID),
    scoreboard: EVENTS_QUERY_KEYS.scoreboard(EVENT_ID),
    audits: EVENTS_QUERY_KEYS.audits(EVENT_ID, { page: 1 }),
    overlayTokens: OVERLAY_TOKENS_QUERY_KEYS.list(EVENT_ID),
    list: EVENTS_QUERY_KEYS.list({ page: 1 }),
    featured: EVENTS_QUERY_KEYS.featured,
  }
  for (const key of Object.values(keys)) {
    client.setQueryData(key, { seeded: true })
  }
  return keys
}

const isStale = (client: QueryClient, key: readonly unknown[]): boolean =>
  client.getQueryState(key as unknown[])?.isInvalidated ?? false

describe('invalidateEventScope', () => {
  let client: QueryClient

  beforeEach(() => {
    client = new QueryClient()
  })

  it("'progress' marks the scoreboard and scores stale, and nothing else", () => {
    const keys = seedQueries(client)
    invalidateEventScope(client, EVENT_ID, 'progress')

    expect(isStale(client, keys.scoreboard)).toBe(true)
    expect(isStale(client, keys.scores)).toBe(true)
    expect(isStale(client, keys.detail)).toBe(false)
    expect(isStale(client, keys.audits)).toBe(false)
    expect(isStale(client, keys.overlayTokens)).toBe(false)
    expect(isStale(client, keys.list)).toBe(false)
    expect(isStale(client, keys.featured)).toBe(false)
  })

  it("'structure' marks detail, scoreboard, and scores stale, and nothing else", () => {
    const keys = seedQueries(client)
    invalidateEventScope(client, EVENT_ID, 'structure')

    expect(isStale(client, keys.detail)).toBe(true)
    expect(isStale(client, keys.scoreboard)).toBe(true)
    expect(isStale(client, keys.scores)).toBe(true)
    expect(isStale(client, keys.audits)).toBe(false)
    expect(isStale(client, keys.overlayTokens)).toBe(false)
    expect(isStale(client, keys.list)).toBe(false)
    expect(isStale(client, keys.featured)).toBe(false)
  })

  it("'listing' marks detail, the event list, and featured stale, and nothing else", () => {
    const keys = seedQueries(client)
    invalidateEventScope(client, EVENT_ID, 'listing')

    expect(isStale(client, keys.detail)).toBe(true)
    expect(isStale(client, keys.list)).toBe(true)
    expect(isStale(client, keys.featured)).toBe(true)
    expect(isStale(client, keys.scoreboard)).toBe(false)
    expect(isStale(client, keys.scores)).toBe(false)
    expect(isStale(client, keys.audits)).toBe(false)
    expect(isStale(client, keys.overlayTokens)).toBe(false)
  })
})
