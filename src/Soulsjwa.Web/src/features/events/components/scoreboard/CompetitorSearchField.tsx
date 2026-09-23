import Box from '@mui/material/Box'
import TextField from '@mui/material/TextField'
import { visuallyHidden } from '@mui/utils'

interface CompetitorSearchFieldProps {
  query: string
  onChange: (query: string) => void
  matchCount: number
  totalCount: number
}

/**
 * Visibly labelled search over the scoreboard's competitors. The match count
 * sits in a polite live region that is always rendered, so screen readers
 * hear it change as the query is typed.
 */
export const CompetitorSearchField = ({
  query,
  onChange,
  matchCount,
  totalCount,
}: CompetitorSearchFieldProps) => (
  <Box sx={{ width: { xs: '100%', md: 280 }, ml: { md: 'auto' } }}>
    <TextField
      label="Search competitors"
      type="search"
      size="small"
      fullWidth
      value={query}
      onChange={(e) => onChange(e.target.value)}
      slotProps={{ htmlInput: { autoComplete: 'off' } }}
    />
    <Box role="status" aria-live="polite" sx={visuallyHidden}>
      {query.trim() ? `${matchCount} of ${totalCount} competitors match` : ''}
    </Box>
  </Box>
)
