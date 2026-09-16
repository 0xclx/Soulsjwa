import { useState } from 'react'
import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { MarkdownEditor, MARKDOWN_MAX_BYTES } from './MarkdownEditor'
import { MarkdownView } from './MarkdownView'
import { renderMarkdown } from './renderMarkdown'

const Harness = ({ initial = '' }: { initial?: string }) => {
  const [value, setValue] = useState(initial)
  return <MarkdownEditor value={value} onChange={setValue} />
}

describe('MarkdownView and MarkdownEditor preview', () => {
  it('produce identical output for the same input', async () => {
    const source = '# Title\n\nSome **bold** text and a [link](https://example.com).'

    const { container: viewContainer } = render(<MarkdownView source={source} />)
    const viewBox = viewContainer.firstElementChild as HTMLElement

    const { container: editorContainer } = render(<Harness initial={source} />)
    await userEvent.click(within(editorContainer).getByRole('tab', { name: 'Preview' }))
    const editorPreviewBox = within(editorContainer).getByText('Title').closest('div')!

    expect(editorPreviewBox.innerHTML).toBe(viewBox.innerHTML)
    expect(viewBox.innerHTML).toBe(renderMarkdown(source))
  })
})

describe('MarkdownEditor', () => {
  it('renders a write tab with a textarea and a formatting toolbar', () => {
    render(<Harness />)
    expect(screen.getByRole('tab', { name: 'Write' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Bold' })).toBeInTheDocument()
    expect(screen.getByRole('textbox')).toBeInTheDocument()
  })

  it('inserts markdown syntax around a placeholder when a toolbar button is clicked', async () => {
    render(<Harness />)
    await userEvent.click(screen.getByRole('button', { name: 'Bold' }))
    expect(screen.getByRole('textbox')).toHaveValue('**bold text**')
  })

  it('shows a byte counter that updates as text is typed', async () => {
    render(<Harness />)
    await userEvent.type(screen.getByRole('textbox'), 'hello')
    expect(screen.getByText(`5 / ${MARKDOWN_MAX_BYTES.toLocaleString()} bytes`)).toBeInTheDocument()
  })

  it('blocks input past the byte cap', async () => {
    const onChange = vi.fn()
    render(<MarkdownEditor value="" onChange={onChange} maxBytes={5} />)

    await userEvent.type(screen.getByRole('textbox'), 'toolong')

    // Every accepted change stayed within the 5-byte cap.
    for (const call of onChange.mock.calls) {
      expect(new TextEncoder().encode(call[0] as string).byteLength).toBeLessThanOrEqual(5)
    }
    expect(onChange).not.toHaveBeenCalledWith(expect.stringMatching(/^.{6,}$/))
  })

  it('does not call onChange when a toolbar insertion would exceed the byte cap', async () => {
    const onChange = vi.fn()
    render(<MarkdownEditor value="1234" onChange={onChange} maxBytes={5} />)

    // Bold wraps with ** on each side; with no selection it falls back to a
    // placeholder far larger than the 1 byte of headroom left, so the click
    // must be a no-op.
    await userEvent.click(screen.getByRole('button', { name: 'Bold' }))
    expect(onChange).not.toHaveBeenCalled()
  })
})
