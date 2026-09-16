import type { ReactNode } from 'react'
import Box from '@mui/material/Box'
import Stack from '@mui/material/Stack'
import Typography from '@mui/material/Typography'
import { Surface } from './Surface'

interface StatCardProps {
  label: string
  value: ReactNode
  helper?: ReactNode
}

export const StatCard = ({ label, value, helper }: StatCardProps) => (
  <Surface>
    <Stack spacing={0.75}>
      <Typography variant="caption" color="text.secondary" sx={{ textTransform: 'uppercase' }}>
        {label}
      </Typography>
      <Box>
        <Typography variant="h4" component="p">
          {value}
        </Typography>
        {helper && (
          <Typography variant="body2" color="text.secondary">
            {helper}
          </Typography>
        )}
      </Box>
    </Stack>
  </Surface>
)
