import { describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { Table, TableBody } from '@mui/material'
import { ScoreboardGameBreakdown } from './ScoreboardGameBreakdown'
import type { CompetitorInfo, GameBreakdown, ObjectiveDetail } from '../../../../types'

const baseObjective: ObjectiveDetail = {
  objectiveId: 'obj-1',
  name: 'Placeholder',
  score: 1,
  category: null,
  isCompleted: false,
  completedAt: null,
  isFailed: false,
  failedAt: null,
  status: 'Pending',
  trial: null,
}

const eventId = '11111111-1111-1111-1111-111111111111'
const eventGameId = '22222222-2222-2222-2222-222222222222'
const competitorUserId = '33333333-3333-3333-3333-333333333333'

const deathClipInfo: CompetitorInfo = {
  id: 'aaaa1111-aaaa-1111-aaaa-111111111111',
  eventGameId,
  userId: competitorUserId,
  type: 'DeathClip',
  url: 'https://clips.twitch.tv/deathclip',
  text: null,
  createdById: competitorUserId,
  createdAt: '2025-01-01T00:00:00Z',
  updatedAt: '2025-01-01T00:00:00Z',
}

const baseGame: GameBreakdown = {
  eventGameId,
  gameName: 'Elden Ring',
  score: 100,
  completedCount: 1,
  totalObjectives: 2,
  objectives: [],
  infos: [],
  hasDeathClip: false,
  failedCount: 0,
  // Active games start expanded — set true so these content-presence
  // assertions don't have to fold the row open first.
  isEnabled: true,
  isTrialActive: false,
  hasTrialRun: false,
  trial: null,
}

function renderGame(game: GameBreakdown) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={client}>
      <Table>
        <TableBody>
          <ScoreboardGameBreakdown
            game={game}
            eventId={eventId}
            competitorUserId={competitorUserId}
            canEdit={false}
          />
        </TableBody>
      </Table>
    </QueryClientProvider>,
  )
}

describe('ScoreboardGameBreakdown death clip skull', () => {
  it('renders the skull as a link to the death clip URL when one is recorded', () => {
    renderGame({ ...baseGame, hasDeathClip: true, infos: [deathClipInfo] })

    const link = screen.getByRole('link', { name: 'Watch death clip' })
    expect(link).toHaveAttribute('href', deathClipInfo.url)
    expect(link).toHaveAttribute('target', '_blank')
    expect(link).toHaveAttribute('rel', expect.stringContaining('noopener'))
  })

  it('falls back to a plain (non-clickable) skull if no death clip URL is present', () => {
    renderGame({ ...baseGame, hasDeathClip: true, infos: [] })

    expect(screen.queryByRole('link', { name: 'Watch death clip' })).not.toBeInTheDocument()
    expect(screen.getByRole('img', { name: 'Death recorded' })).toBeInTheDocument()
  })

  it('renders no skull at all when there is no death clip', () => {
    renderGame({ ...baseGame, hasDeathClip: false, infos: [] })

    expect(screen.queryByRole('link', { name: 'Watch death clip' })).not.toBeInTheDocument()
    expect(screen.queryByRole('img', { name: 'Death recorded' })).not.toBeInTheDocument()
  })
})

describe('ScoreboardGameBreakdown category grouping', () => {
  it('shows a category header per group when objectives span more than one category', () => {
    renderGame({
      ...baseGame,
      objectives: [
        { ...baseObjective, objectiveId: 'a', name: 'Boss A', category: 'Altus Plateau' },
        { ...baseObjective, objectiveId: 'b', name: 'Boss B', category: 'Altus Plateau' },
        { ...baseObjective, objectiveId: 'c', name: 'Boss C', category: 'Limgrave' },
      ],
    })

    expect(screen.getByText('Altus Plateau')).toBeInTheDocument()
    expect(screen.getByText('Limgrave')).toBeInTheDocument()
    expect(screen.getByText('Boss A')).toBeInTheDocument()
    expect(screen.getByText('Boss B')).toBeInTheDocument()
    expect(screen.getByText('Boss C')).toBeInTheDocument()
  })

  it('falls back to "Other" for uncategorized objectives when categories are otherwise mixed', () => {
    renderGame({
      ...baseGame,
      objectives: [
        { ...baseObjective, objectiveId: 'a', name: 'Boss A', category: 'Altus Plateau' },
        { ...baseObjective, objectiveId: 'b', name: 'Boss B', category: null },
      ],
    })

    expect(screen.getByText('Altus Plateau')).toBeInTheDocument()
    expect(screen.getByText('Other')).toBeInTheDocument()
  })

  it('omits category headers entirely when every objective shares one category', () => {
    renderGame({
      ...baseGame,
      objectives: [
        { ...baseObjective, objectiveId: 'a', name: 'Boss A', category: 'Altus Plateau' },
        { ...baseObjective, objectiveId: 'b', name: 'Boss B', category: 'Altus Plateau' },
      ],
    })

    expect(screen.queryByText('Altus Plateau')).not.toBeInTheDocument()
    expect(screen.getByText('Boss A')).toBeInTheDocument()
    expect(screen.getByText('Boss B')).toBeInTheDocument()
  })
})
