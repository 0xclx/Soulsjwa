import IconButton from '@mui/material/IconButton'
import Stack from '@mui/material/Stack'
import Tooltip from '@mui/material/Tooltip'
import Typography from '@mui/material/Typography'
import ChevronLeftIcon from '@mui/icons-material/ChevronLeft'
import ChevronRightIcon from '@mui/icons-material/ChevronRight'

interface PaginationControlsProps {
  page: number
  totalPages: number
  hasPreviousPage: boolean
  hasNextPage: boolean
  onPrevious: () => void
  onNext: () => void
  /** Accessible label describing the paged collection (e.g. "events"). */
  label?: string
}

/**
 * Shared previous/next pager with a "Page X of Y" indicator. Centralizes the
 * pagination pattern that Events, Admin users, and other paginated lists share
 * so spacing, icons, and a11y labels stay consistent.
 */
export const PaginationControls = ({
  page,
  totalPages,
  hasPreviousPage,
  hasNextPage,
  onPrevious,
  onNext,
  label = 'results',
}: PaginationControlsProps) => (
  <Stack
    direction="row"
    spacing={1.5}
    sx={{ justifyContent: 'center', alignItems: 'center' }}
    aria-label={`${label} pagination`}
  >
    <Tooltip title="Previous page">
      <span>
        <IconButton
          onClick={onPrevious}
          disabled={!hasPreviousPage}
          aria-label={`Previous page of ${label}`}
        >
          <ChevronLeftIcon />
        </IconButton>
      </span>
    </Tooltip>
    <Typography color="text.secondary" variant="body2">
      Page {page} of {totalPages}
    </Typography>
    <Tooltip title="Next page">
      <span>
        <IconButton onClick={onNext} disabled={!hasNextPage} aria-label={`Next page of ${label}`}>
          <ChevronRightIcon />
        </IconButton>
      </span>
    </Tooltip>
  </Stack>
)
