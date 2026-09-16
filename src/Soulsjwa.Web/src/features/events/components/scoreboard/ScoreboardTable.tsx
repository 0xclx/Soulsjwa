import Paper from '@mui/material/Paper'
import Table from '@mui/material/Table'
import TableBody from '@mui/material/TableBody'
import TableCell from '@mui/material/TableCell'
import TableContainer from '@mui/material/TableContainer'
import TableHead from '@mui/material/TableHead'
import TableRow from '@mui/material/TableRow'
import { ScoreboardRow } from './ScoreboardRow'
import type { EventResponse, ScoreboardEntry, User } from '../../../../types'

interface ScoreboardTableProps {
  entries: readonly ScoreboardEntry[]
  eventId: string
  event: EventResponse | undefined
  currentUser: User | undefined
}

/** Dense desktop scoreboard table (hidden on mobile in favor of cards). */
export const ScoreboardTable = ({ entries, eventId, event, currentUser }: ScoreboardTableProps) => (
  <TableContainer component={Paper} variant="outlined">
    <Table aria-label="Scoreboard">
      <TableHead>
        <TableRow>
          <TableCell sx={{ width: 40 }}>#</TableCell>
          <TableCell>Player</TableCell>
          <TableCell align="right">Total Completed</TableCell>
          <TableCell align="right">Total Failed</TableCell>
          <TableCell align="right">Total Score</TableCell>
          <TableCell sx={{ width: 40 }} />
        </TableRow>
      </TableHead>
      <TableBody>
        {entries.map((entry) => (
          <ScoreboardRow
            key={entry.userId}
            entry={entry}
            rank={entry.rank}
            eventId={eventId}
            event={event}
            currentUser={currentUser}
          />
        ))}
      </TableBody>
    </Table>
  </TableContainer>
)
