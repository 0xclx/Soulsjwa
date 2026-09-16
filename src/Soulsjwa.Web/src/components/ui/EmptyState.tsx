import type { ReactNode } from 'react'
import InboxOutlinedIcon from '@mui/icons-material/InboxOutlined'
import Box from '@mui/material/Box'
import Stack from '@mui/material/Stack'
import Typography from '@mui/material/Typography'
import { alpha } from '@mui/material/styles'
import { Surface } from './Surface'

interface EmptyStateProps {
  title: string
  description?: ReactNode
  action?: ReactNode
}

export const EmptyState = ({ title, description, action }: EmptyStateProps) => (
  <Surface>
    <Stack spacing={2} sx={{ alignItems: 'center', textAlign: 'center', py: { xs: 2, md: 4 } }}>
      <Box
        sx={{
          width: 56,
          height: 56,
          borderRadius: '50%',
          display: 'grid',
          placeItems: 'center',
          color: 'secondary.main',
          bgcolor: (theme) => alpha(theme.palette.secondary.main, 0.12),
        }}
      >
        <InboxOutlinedIcon />
      </Box>
      <Box>
        <Typography variant="h6">{title}</Typography>
        {description && (
          <Typography color="text.secondary" sx={{ mt: 0.5 }}>
            {description}
          </Typography>
        )}
      </Box>
      {action}
    </Stack>
  </Surface>
)
