/**
 * What the admin "Create sample events" action creates, for the Sample data
 * page to describe. Mirrors the server's SampleEventCatalog; the server's list
 * is the source of truth for the events themselves.
 */
export const SAMPLE_EVENT_SUMMARIES = [
  { name: 'Sample: Not started', state: 'Not started; three games with objectives' },
  { name: 'Sample: Game in progress', state: 'Started; second of three games enabled; rules text' },
  { name: 'Sample: Between games', state: 'Started; no game enabled (whole event view only)' },
  { name: 'Sample: Many games', state: 'Started; first of seven games enabled' },
  { name: 'Sample: Empty setup', state: 'Not started; no games (empty states)' },
] as const

export const SAMPLE_DATA_TEXT = {
  title: 'Sample events',
  description:
    'Creates the same five events every time, each in a different state, for testing the ' +
    'scoreboard and event pages. They have games and objectives but no competitors: add ' +
    'competitors by hand. Archive the events to hide them again.',
  create: 'Create sample events',
  confirmTitle: `Create ${SAMPLE_EVENT_SUMMARIES.length} sample events?`,
  confirmDescription:
    "They are named 'Sample: …', owned by you and visible to everyone until you archive them.",
  confirmLabel: 'Create',
  createdHeading: 'Created',
  createdListLabel: 'Created sample events',
  failed: 'Failed to create the sample events.',
} as const
