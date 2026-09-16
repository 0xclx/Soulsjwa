import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { TwitchExtensionAdminInfo } from '../../../types/twitchExtension'
import {
  BUNDLE_MISSING_LABEL,
  BUNDLE_READY_LABEL,
  CONFIGURED_LABEL,
  DOWNLOAD_LABEL,
  NOT_CONFIGURED_LABEL,
  SAVE_LABEL,
  SAVED_LABEL,
  TwitchExtensionAdminTab,
} from './TwitchExtensionAdminTab'

const info: TwitchExtensionAdminInfo = {
  configured: true,
  clientId: 'abc123',
  canPush: false,
  extensionOrigin: 'https://abc123.ext-twitch.tv',
  localTestOrigin: null,
  bundle: { available: true, fileCount: 12, apiUrl: 'https://events.example.com' },
  settings: {
    allowChannelEventChoice: true,
    allowViewerScopeSwitch: true,
    defaultScope: 'AllGames',
    defaultHighlightChannelCompetitor: true,
    defaultShowTrialProgress: true,
  },
  updatedAt: null,
  updatedById: null,
}

const query = vi.fn()
const mutateAsync = vi.fn()
const downloadMutate = vi.fn()

vi.mock('../hooks/useTwitchExtensionAdmin', () => ({
  useTwitchExtensionAdmin: () => query(),
}))
vi.mock('../hooks/useUpdateTwitchExtensionSettings', () => ({
  useUpdateTwitchExtensionSettings: () => ({ mutateAsync, isPending: false, isError: false }),
}))
vi.mock('../hooks/useDownloadTwitchExtensionBundle', () => ({
  useDownloadTwitchExtensionBundle: () => ({
    mutate: downloadMutate,
    isPending: false,
    isError: false,
  }),
}))

beforeEach(() => {
  query.mockReset()
  mutateAsync.mockReset()
  downloadMutate.mockReset()
  query.mockReturnValue({ data: info, isLoading: false, isError: false })
  mutateAsync.mockResolvedValue(info)
})

describe('TwitchExtensionAdminTab', () => {
  it('shows the status, the API host in the instructions, and offers the download', async () => {
    render(<TwitchExtensionAdminTab />)

    expect(screen.getByText(CONFIGURED_LABEL)).toBeInTheDocument()
    expect(screen.getByText(BUNDLE_READY_LABEL)).toBeInTheDocument()
    expect(screen.getAllByText('abc123').length).toBeGreaterThan(0)
    expect(screen.getByText('events.example.com')).toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: DOWNLOAD_LABEL }))

    expect(downloadMutate).toHaveBeenCalledTimes(1)
  })

  it('saves changed rules', async () => {
    render(<TwitchExtensionAdminTab />)

    expect(screen.getByRole('button', { name: SAVE_LABEL })).toBeDisabled()
    await userEvent.click(screen.getByRole('switch', { name: /Streamers may show an event/ }))
    await userEvent.click(screen.getByRole('button', { name: SAVE_LABEL }))

    expect(mutateAsync).toHaveBeenCalledWith({ ...info.settings, allowChannelEventChoice: false })
    expect(await screen.findByText(SAVED_LABEL)).toBeInTheDocument()
  })

  it('explains what to set when the server is not configured and the bundle is missing', () => {
    query.mockReturnValue({
      data: {
        ...info,
        configured: false,
        clientId: null,
        extensionOrigin: null,
        bundle: { available: false, fileCount: 0, apiUrl: 'https://events.example.com' },
      },
      isLoading: false,
      isError: false,
    })

    render(<TwitchExtensionAdminTab />)

    expect(screen.getByText(NOT_CONFIGURED_LABEL)).toBeInTheDocument()
    expect(screen.getByText(BUNDLE_MISSING_LABEL)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: DOWNLOAD_LABEL })).toBeDisabled()
    expect(screen.getAllByText(/TwitchExtension__ClientId/).length).toBeGreaterThan(0)
  })
})
