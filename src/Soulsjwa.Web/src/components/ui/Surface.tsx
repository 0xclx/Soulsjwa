import type { ReactNode } from 'react'
import Paper from '@mui/material/Paper'

interface SurfaceProps {
  children: ReactNode
}

/** Stock MUI surface with the shared responsive content padding. */
export const Surface = ({ children }: SurfaceProps) => (
  <Paper variant="outlined" sx={{ p: { xs: 2, md: 3 } }}>
    {children}
  </Paper>
)
