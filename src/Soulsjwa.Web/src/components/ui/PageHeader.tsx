import type { ReactNode } from 'react'
import Box from '@mui/material/Box'
import Chip from '@mui/material/Chip'
import Stack from '@mui/material/Stack'
import Typography from '@mui/material/Typography'

interface PageHeaderProps {
  eyebrow?: string
  title: ReactNode
  description?: ReactNode
  actions?: ReactNode
  meta?: ReactNode
}

export const PageHeader = ({ eyebrow, title, description, actions, meta }: PageHeaderProps) => (
  <Stack
    direction={{ xs: 'column', md: 'row' }}
    spacing={2}
    sx={{ justifyContent: 'space-between', alignItems: { xs: 'stretch', md: 'flex-start' } }}
  >
    <Box sx={{ minWidth: 0, maxWidth: 820 }}>
      {eyebrow && (
        <Chip label={eyebrow} size="small" color="secondary" variant="outlined" sx={{ mb: 1.5 }} />
      )}
      <Typography variant="h1" component="h1">
        {title}
      </Typography>
      {description && (
        <Typography color="text.secondary" sx={{ mt: 1, maxWidth: 760 }}>
          {description}
        </Typography>
      )}
      {meta && <Box sx={{ mt: 1.5 }}>{meta}</Box>}
    </Box>
    {actions && (
      <Stack
        direction="row"
        spacing={1}
        sx={{ flexWrap: 'wrap', justifyContent: { xs: 'flex-start', md: 'flex-end' }, gap: 1 }}
      >
        {actions}
      </Stack>
    )}
  </Stack>
)
