import { useRef, useState, type ReactNode } from 'react'
import Box from '@mui/material/Box'
import IconButton from '@mui/material/IconButton'
import Stack from '@mui/material/Stack'
import Tab from '@mui/material/Tab'
import Tabs from '@mui/material/Tabs'
import TextField from '@mui/material/TextField'
import Tooltip from '@mui/material/Tooltip'
import Typography from '@mui/material/Typography'
import CodeIcon from '@mui/icons-material/Code'
import FormatBoldIcon from '@mui/icons-material/FormatBold'
import FormatItalicIcon from '@mui/icons-material/FormatItalic'
import FormatListBulletedIcon from '@mui/icons-material/FormatListBulleted'
import FormatListNumberedIcon from '@mui/icons-material/FormatListNumbered'
import LinkIcon from '@mui/icons-material/Link'
import { MarkdownView } from './MarkdownView'

/** Matches the 64 KiB per-document limit enforced server-side. */
export const MARKDOWN_MAX_BYTES = 64 * 1024

const byteLength = (s: string) => new TextEncoder().encode(s).byteLength

interface ToolbarAction {
  label: string
  icon: ReactNode
  /** Wraps the current selection (or inserts a placeholder) with before/after markers. */
  before: string
  after: string
  placeholder: string
}

const TOOLBAR_ACTIONS: ToolbarAction[] = [
  {
    label: 'Bold',
    icon: <FormatBoldIcon fontSize="small" />,
    before: '**',
    after: '**',
    placeholder: 'bold text',
  },
  {
    label: 'Italic',
    icon: <FormatItalicIcon fontSize="small" />,
    before: '*',
    after: '*',
    placeholder: 'italic text',
  },
  {
    label: 'Code',
    icon: <CodeIcon fontSize="small" />,
    before: '`',
    after: '`',
    placeholder: 'code',
  },
  {
    label: 'Link',
    icon: <LinkIcon fontSize="small" />,
    before: '[',
    after: '](https://)',
    placeholder: 'link text',
  },
  {
    label: 'Bulleted list',
    icon: <FormatListBulletedIcon fontSize="small" />,
    before: '- ',
    after: '',
    placeholder: 'list item',
  },
  {
    label: 'Numbered list',
    icon: <FormatListNumberedIcon fontSize="small" />,
    before: '1. ',
    after: '',
    placeholder: 'list item',
  },
]

interface MarkdownEditorProps {
  value: string
  onChange: (value: string) => void
  disabled?: boolean
  label?: string
  placeholder?: string
  /** Byte cap enforced on every change. Defaults to the server's 64 KiB limit. */
  maxBytes?: number
}

/**
 * Write/preview editor for Markdown content. Both the toolbar-driven write
 * tab and the preview tab render through the same renderMarkdown() as
 * MarkdownView, so what you see in preview is exactly what a reader gets.
 */
export const MarkdownEditor = ({
  value,
  onChange,
  disabled,
  label = 'Content',
  placeholder,
  maxBytes = MARKDOWN_MAX_BYTES,
}: MarkdownEditorProps) => {
  const [tab, setTab] = useState<'write' | 'preview'>('write')
  const textareaRef = useRef<HTMLTextAreaElement | null>(null)

  const handleChange = (next: string) => {
    if (byteLength(next) > maxBytes) return
    onChange(next)
  }

  const applyToolbarAction = (action: ToolbarAction) => {
    const textarea = textareaRef.current
    if (!textarea) return
    const start = textarea.selectionStart ?? value.length
    const end = textarea.selectionEnd ?? value.length
    const selected = value.slice(start, end) || action.placeholder
    const next = value.slice(0, start) + action.before + selected + action.after + value.slice(end)
    if (byteLength(next) > maxBytes) return

    onChange(next)
    const cursorStart = start + action.before.length
    const cursorEnd = cursorStart + selected.length
    requestAnimationFrame(() => {
      textarea.focus()
      textarea.setSelectionRange(cursorStart, cursorEnd)
    })
  }

  const bytes = byteLength(value)
  const overLimit = bytes > maxBytes

  return (
    <Box>
      <Tabs
        value={tab}
        onChange={(_, next) => setTab(next)}
        aria-label="Markdown editor mode"
        sx={{ minHeight: 40, mb: 1, '& .MuiTab-root': { minHeight: 40, py: 0 } }}
      >
        <Tab value="write" label="Write" />
        <Tab value="preview" label="Preview" />
      </Tabs>

      {tab === 'write' ? (
        <>
          <Stack direction="row" spacing={0.5} sx={{ mb: 1 }}>
            {TOOLBAR_ACTIONS.map((action) => (
              <Tooltip key={action.label} title={action.label}>
                <span>
                  <IconButton
                    size="small"
                    disabled={disabled}
                    aria-label={action.label}
                    onClick={() => applyToolbarAction(action)}
                  >
                    {action.icon}
                  </IconButton>
                </span>
              </Tooltip>
            ))}
          </Stack>
          <TextField
            inputRef={textareaRef}
            value={value}
            onChange={(e) => handleChange(e.target.value)}
            label={label}
            placeholder={placeholder}
            disabled={disabled}
            multiline
            minRows={6}
            fullWidth
          />
          <Typography
            variant="caption"
            color={overLimit ? 'error' : 'text.secondary'}
            sx={{ display: 'block', textAlign: 'right', mt: 0.5 }}
          >
            {bytes.toLocaleString()} / {maxBytes.toLocaleString()} bytes
          </Typography>
        </>
      ) : (
        <Box sx={{ p: 2, border: 1, borderColor: 'divider', borderRadius: 1 }}>
          <MarkdownView source={value} />
        </Box>
      )}
    </Box>
  )
}
