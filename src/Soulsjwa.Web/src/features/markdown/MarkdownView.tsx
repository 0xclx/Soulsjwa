import { useMemo } from 'react'
import Box from '@mui/material/Box'
import { renderMarkdown } from './renderMarkdown'

interface MarkdownViewProps {
  /** Markdown source. Rendered through the single shared renderMarkdown(). */
  source: string
}

/**
 * Trusted-rendered Markdown output, styled from the MUI theme. This is the
 * only place in the app that uses dangerouslySetInnerHTML for user-authored
 * content — safe because renderMarkdown() renders with `html: false`, so
 * there is no HTML path in the source to sanitise.
 */
export const MarkdownView = ({ source }: MarkdownViewProps) => {
  const html = useMemo(() => renderMarkdown(source), [source])

  return (
    <Box
      sx={{
        color: 'text.primary',
        lineHeight: 1.6,
        wordBreak: 'break-word',
        '& > :first-of-type': { mt: 0 },
        '& > :last-child': { mb: 0 },
        '& h1, & h2, & h3, & h4, & h5, & h6': {
          fontFamily: 'inherit',
          fontWeight: 700,
          lineHeight: 1.3,
          mt: 3,
          mb: 1.5,
        },
        '& h1': { typography: 'h4' },
        '& h2': { typography: 'h5' },
        '& h3': { typography: 'h6' },
        '& h4, & h5, & h6': { typography: 'subtitle1' },
        '& p': { my: 1.5 },
        '& ul, & ol': { my: 1.5, pl: 3 },
        '& li': { mb: 0.5 },
        '& li > p': { my: 0 },
        '& blockquote': {
          my: 2,
          py: 0.5,
          px: 2,
          borderLeft: 3,
          borderColor: 'divider',
          color: 'text.secondary',
          fontStyle: 'italic',
        },
        '& a': {
          color: 'primary.main',
          textDecorationColor: 'currentColor',
        },
        '& code': {
          fontFamily: 'monospace',
          fontSize: '0.875em',
          bgcolor: 'action.hover',
          borderRadius: 0.5,
          px: 0.5,
          py: 0.125,
        },
        '& pre': {
          my: 2,
          p: 1.5,
          borderRadius: 1,
          bgcolor: 'action.hover',
          overflowX: 'auto',
        },
        '& pre code': { bgcolor: 'transparent', p: 0 },
        '& hr': { my: 3, border: 0, borderTop: 1, borderColor: 'divider' },
        '& img': { maxWidth: '100%' },
      }}
      dangerouslySetInnerHTML={{ __html: html }}
    />
  )
}
