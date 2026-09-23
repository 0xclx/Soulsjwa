import { useState } from 'react'
import { Link as RouterLink } from 'react-router-dom'
import Alert from '@mui/material/Alert'
import Button from '@mui/material/Button'
import Link from '@mui/material/Link'
import Stack from '@mui/material/Stack'
import Table from '@mui/material/Table'
import TableBody from '@mui/material/TableBody'
import TableCell from '@mui/material/TableCell'
import TableHead from '@mui/material/TableHead'
import TableRow from '@mui/material/TableRow'
import Typography from '@mui/material/Typography'
import { ConfirmDialog, Surface } from '../../../components/ui'
import { getEventPath } from '../../events/eventUrl'
import { useCreateSampleEvents } from '../hooks/useCreateSampleEvents'
import { SAMPLE_DATA_TEXT, SAMPLE_EVENT_SUMMARIES } from '../sampleEvents'

/**
 * Admin "Sample data" section: describes the fixed sample events and creates a
 * fresh set behind a confirmation, in any environment.
 */
export const SampleDataTab = () => {
  const [confirming, setConfirming] = useState(false)
  const create = useCreateSampleEvents()

  const confirm = () => {
    setConfirming(false)
    create.mutate()
  }

  return (
    <Surface>
      <Stack spacing={2}>
        <Typography variant="h6" component="h2">
          {SAMPLE_DATA_TEXT.title}
        </Typography>
        <Typography color="text.secondary">{SAMPLE_DATA_TEXT.description}</Typography>
        <Table size="small" aria-label={SAMPLE_DATA_TEXT.title}>
          <TableHead>
            <TableRow>
              <TableCell>Event</TableCell>
              <TableCell>State</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {SAMPLE_EVENT_SUMMARIES.map(({ name, state }) => (
              <TableRow key={name}>
                <TableCell>{name}</TableCell>
                <TableCell>{state}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
        <Button
          variant="contained"
          onClick={() => setConfirming(true)}
          disabled={create.isPending}
          sx={{ alignSelf: 'flex-start' }}
        >
          {SAMPLE_DATA_TEXT.create}
        </Button>
        {create.isError && <Alert severity="error">{SAMPLE_DATA_TEXT.failed}</Alert>}
        {create.isSuccess && (
          <Alert severity="success">
            <Typography component="h3" variant="subtitle2">
              {SAMPLE_DATA_TEXT.createdHeading}
            </Typography>
            <Stack
              component="ul"
              aria-label={SAMPLE_DATA_TEXT.createdListLabel}
              spacing={0.5}
              sx={{ m: 0, pl: 2 }}
            >
              {create.data.map((event) => (
                <li key={event.id}>
                  <Link component={RouterLink} to={getEventPath(event.id)}>
                    {event.name}
                  </Link>
                </li>
              ))}
            </Stack>
          </Alert>
        )}
      </Stack>
      <ConfirmDialog
        open={confirming}
        title={SAMPLE_DATA_TEXT.confirmTitle}
        description={SAMPLE_DATA_TEXT.confirmDescription}
        confirmLabel={SAMPLE_DATA_TEXT.confirmLabel}
        onCancel={() => setConfirming(false)}
        onConfirm={confirm}
      />
    </Surface>
  )
}
