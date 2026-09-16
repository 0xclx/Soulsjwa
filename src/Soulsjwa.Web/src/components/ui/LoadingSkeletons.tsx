import Box from '@mui/material/Box'
import Skeleton from '@mui/material/Skeleton'
import Stack from '@mui/material/Stack'
import { Surface } from './Surface'

/**
 * Shared skeleton loading placeholders. Prefer these over a bare spinner for
 * content that has a predictable shape (lists, cards, tables) so the layout
 * does not shift when data arrives. Each placeholder announces itself with
 * `role="status"` / `aria-busy` for assistive technologies.
 */

interface CardListSkeletonProps {
  count?: number
  /** Layout: single-column stack (default) or responsive two-column grid. */
  columns?: 1 | 2
  label?: string
}

/**
 * A grid/stack of card placeholders that mirrors the Events list and
 * mobile scoreboard layouts.
 */
export const CardListSkeleton = ({
  count = 4,
  columns = 1,
  label = 'Loading content…',
}: CardListSkeletonProps) => (
  <Box
    role="status"
    aria-busy="true"
    aria-label={label}
    sx={{
      display: 'grid',
      gridTemplateColumns: columns === 2 ? { xs: '1fr', lg: 'repeat(2, minmax(0, 1fr))' } : '1fr',
      gap: 2,
    }}
  >
    {Array.from({ length: count }).map((_, index) => (
      <Surface key={index}>
        <Stack spacing={1.5}>
          <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center' }}>
            <Skeleton variant="circular" width={40} height={40} />
            <Box sx={{ flex: 1 }}>
              <Skeleton variant="text" width="55%" sx={{ fontSize: '1.25rem' }} />
              <Skeleton variant="text" width="35%" />
            </Box>
          </Stack>
          <Skeleton variant="text" width="90%" />
          <Skeleton variant="text" width="70%" />
          <Skeleton variant="rounded" height={36} width={120} />
        </Stack>
      </Surface>
    ))}
  </Box>
)

interface TableSkeletonProps {
  rows?: number
  columns?: number
  label?: string
}

/**
 * A table-shaped placeholder used for dense desktop tables such as the
 * scoreboard while the first payload loads.
 */
export const TableSkeleton = ({
  rows = 6,
  columns = 5,
  label = 'Loading table…',
}: TableSkeletonProps) => (
  <Surface>
    <Box role="status" aria-busy="true" aria-label={label}>
      <Stack spacing={1.5}>
        {Array.from({ length: rows }).map((_, rowIndex) => (
          <Stack key={rowIndex} direction="row" spacing={2} sx={{ alignItems: 'center' }}>
            {Array.from({ length: columns }).map((__, colIndex) => (
              <Skeleton
                key={colIndex}
                variant="text"
                sx={{ fontSize: '1.25rem', flex: colIndex === 1 ? 3 : 1 }}
              />
            ))}
          </Stack>
        ))}
      </Stack>
    </Box>
  </Surface>
)
