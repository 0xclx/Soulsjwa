import { describe, it, expect, vi } from 'vitest'
import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { CompetitorInfosEditor } from './CompetitorInfosEditor'
import { eventsApi } from '../api/eventsApi'
import type { CompetitorInfo } from '../../../types'

vi.mock('../api/eventsApi', async () => {
  const actual = await vi.importActual<typeof import('../api/eventsApi')>('../api/eventsApi')
  return {
    ...actual,
    eventsApi: {
      ...actual.eventsApi,
      listCompetitorInfos: vi.fn(),
      addCompetitorInfo: vi.fn(),
      removeCompetitorInfo: vi.fn(),
    },
  }
})

function renderWithClient(ui: React.ReactElement) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>)
}

const eventId = '11111111-1111-1111-1111-111111111111'
const eventGameId = '22222222-2222-2222-2222-222222222222'
const userId = '33333333-3333-3333-3333-333333333333'

const sampleInfo: CompetitorInfo = {
  id: 'aaaa1111-aaaa-1111-aaaa-111111111111',
  eventGameId,
  userId,
  type: 'DeathClip',
  url: 'https://clips.twitch.tv/abcd',
  text: null,
  createdById: userId,
  createdAt: '2025-01-01T00:00:00Z',
  updatedAt: '2025-01-01T00:00:00Z',
}

describe('<CompetitorInfosEditor />', () => {
  it('renders existing infos with link and 💀 label for DeathClip', () => {
    renderWithClient(
      <CompetitorInfosEditor
        eventId={eventId}
        eventGameId={eventGameId}
        userId={userId}
        canEdit={false}
        initialInfos={[sampleInfo]}
      />,
    )
    expect(screen.getByText(/Death clip/)).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /clips\.twitch\.tv/ })).toHaveAttribute(
      'href',
      sampleInfo.url,
    )
  })

  it('hides "Add info" when canEdit=false', () => {
    renderWithClient(
      <CompetitorInfosEditor
        eventId={eventId}
        eventGameId={eventGameId}
        userId={userId}
        canEdit={false}
        initialInfos={[]}
      />,
    )
    expect(screen.queryByRole('button', { name: /Add info/i })).not.toBeInTheDocument()
  })

  it('rejects a non-Twitch/YouTube death-clip URL client-side', async () => {
    renderWithClient(
      <CompetitorInfosEditor
        eventId={eventId}
        eventGameId={eventGameId}
        userId={userId}
        canEdit={true}
        initialInfos={[]}
      />,
    )
    fireEvent.click(screen.getByRole('button', { name: /Add info/i }))
    const urlField = await screen.findByLabelText('URL')
    fireEvent.change(urlField, { target: { value: 'https://example.com/not-a-clip' } })
    fireEvent.click(screen.getByRole('button', { name: 'Add' }))
    await waitFor(() => {
      expect(screen.getByText(/Twitch or YouTube/i)).toBeInTheDocument()
    })
    expect(eventsApi.addCompetitorInfo).not.toHaveBeenCalled()
  })

  it('calls addCompetitorInfo with a valid YouTube URL', async () => {
    vi.mocked(eventsApi.addCompetitorInfo).mockResolvedValue({ ...sampleInfo, id: 'new' })
    renderWithClient(
      <CompetitorInfosEditor
        eventId={eventId}
        eventGameId={eventGameId}
        userId={userId}
        canEdit={true}
        initialInfos={[]}
      />,
    )
    fireEvent.click(screen.getByRole('button', { name: /Add info/i }))
    const urlField = await screen.findByLabelText('URL')
    fireEvent.change(urlField, { target: { value: 'https://youtu.be/dQw4w9WgXcQ' } })
    fireEvent.click(screen.getByRole('button', { name: 'Add' }))
    await waitFor(() => {
      expect(eventsApi.addCompetitorInfo).toHaveBeenCalledWith(eventId, eventGameId, userId, {
        type: 'DeathClip',
        url: 'https://youtu.be/dQw4w9WgXcQ',
        text: undefined,
      })
    })
  })
})
