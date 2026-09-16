import { useState } from 'react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ImageUploadField } from './ImageUploadField'
import type { UploadMediaResponse } from '../../../types'

const mocks = vi.hoisted(() => ({
  upload: vi.fn(),
}))

vi.mock('../api/mediaApi', () => ({
  mediaApi: { upload: mocks.upload },
}))

const makeFile = (name: string, type: string, size: number) => {
  const file = new File([new Uint8Array(size)], name, { type })
  return file
}

// userEvent.upload respects the input's `accept` attribute and silently
// drops non-matching files (mirroring a real file picker) — but browsers
// don't strictly enforce `accept` either (an "All Files" option is always
// available), so the component's own client-side check is real defense in
// depth. fireEvent bypasses that filtering to exercise it directly.
const selectFile = (input: HTMLInputElement, file: File) => {
  Object.defineProperty(input, 'files', { value: [file], configurable: true })
  fireEvent.change(input)
}

const Harness = () => {
  const [value, setValue] = useState<UploadMediaResponse | null>(null)
  return <ImageUploadField value={value} onChange={setValue} />
}

const renderField = () => {
  const client = new QueryClient()
  return render(
    <QueryClientProvider client={client}>
      <Harness />
    </QueryClientProvider>,
  )
}

describe('ImageUploadField', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('shows the upload button when empty', () => {
    renderField()
    expect(screen.getByRole('button', { name: /upload image/i })).toBeInTheDocument()
  })

  it('rejects an unsupported file type client-side without calling upload', () => {
    renderField()
    const input = document.querySelector('input[type="file"]') as HTMLInputElement

    selectFile(input, makeFile('a.gif', 'image/gif', 10))

    expect(screen.getByText(/only png, jpeg, and webp/i)).toBeInTheDocument()
    expect(mocks.upload).not.toHaveBeenCalled()
  })

  it('rejects an oversized file client-side without calling upload', () => {
    renderField()
    const input = document.querySelector('input[type="file"]') as HTMLInputElement

    selectFile(input, makeFile('big.png', 'image/png', 6 * 1024 * 1024))

    expect(screen.getByText(/5 mib or smaller/i)).toBeInTheDocument()
    expect(mocks.upload).not.toHaveBeenCalled()
  })

  it('uploads a valid file and shows the result', async () => {
    const result: UploadMediaResponse = {
      assetId: 'asset-1',
      url: '/api/v1/media/asset-1',
      width: 10,
      height: 10,
      byteSize: 100,
      contentType: 'image/png',
    }
    mocks.upload.mockResolvedValue(result)
    renderField()
    const input = document.querySelector('input[type="file"]') as HTMLInputElement
    const file = makeFile('a.png', 'image/png', 100)

    await userEvent.upload(input, file)

    await waitFor(() => expect(screen.getByAltText('')).toHaveAttribute('src', result.url))
    expect(screen.getByRole('button', { name: /replace/i })).toBeInTheDocument()
  })

  it('a rejected upload shows the server detail and does not leave the field uploaded', async () => {
    mocks.upload.mockRejectedValue({ response: { data: { detail: 'Unsupported file type.' } } })
    renderField()
    const input = document.querySelector('input[type="file"]') as HTMLInputElement
    const file = makeFile('a.png', 'image/png', 100)

    await userEvent.upload(input, file)

    await waitFor(() => expect(screen.getByText('Unsupported file type.')).toBeInTheDocument())
    expect(screen.getByRole('button', { name: /upload image/i })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /replace/i })).not.toBeInTheDocument()
    expect(screen.queryByAltText('')).not.toBeInTheDocument()
  })
})
