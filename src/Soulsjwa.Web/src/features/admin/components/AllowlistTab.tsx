import { useState } from 'react'
import Alert from '@mui/material/Alert'
import Button from '@mui/material/Button'
import IconButton from '@mui/material/IconButton'
import Paper from '@mui/material/Paper'
import Stack from '@mui/material/Stack'
import Table from '@mui/material/Table'
import TableBody from '@mui/material/TableBody'
import TableCell from '@mui/material/TableCell'
import TableContainer from '@mui/material/TableContainer'
import TableHead from '@mui/material/TableHead'
import TableRow from '@mui/material/TableRow'
import TextField from '@mui/material/TextField'
import Typography from '@mui/material/Typography'
import DeleteIcon from '@mui/icons-material/Delete'
import { ConfirmDialog, ErrorMessage, LoadingState, Surface } from '../../../components/ui'
import { useAllowlist } from '../hooks/useAllowlist'
import { useAddAllowlistEntry } from '../hooks/useAddAllowlistEntry'
import { useRemoveAllowlistEntry } from '../hooks/useRemoveAllowlistEntry'

/**
 * Twitch login allowlist management: gate sign-ups by handle, add/remove
 * entries, and surface the "removing revokes refresh tokens" behavior.
 */
export const AllowlistTab = () => {
  const { data, isLoading, isError } = useAllowlist()
  const add = useAddAllowlistEntry()
  const remove = useRemoveAllowlistEntry()

  const [login, setLogin] = useState('')
  const [note, setNote] = useState('')
  const [errorMsg, setErrorMsg] = useState<string | null>(null)
  const [entryToRemove, setEntryToRemove] = useState<{
    id: string
    twitchLogin: string
  } | null>(null)

  const handleAdd = () => {
    setErrorMsg(null)
    add.mutate(
      { twitchLogin: login.trim(), note: note.trim() || undefined },
      {
        onSuccess: () => {
          setLogin('')
          setNote('')
        },
        onError: (err: unknown) => {
          const e = err as { response?: { status?: number; data?: { detail?: string } } }
          if (e?.response?.status === 409) setErrorMsg('That login is already on the allowlist.')
          else setErrorMsg(e?.response?.data?.detail ?? 'Failed to add.')
        },
      },
    )
  }

  return (
    <Stack spacing={2}>
      <Alert severity="info">
        <strong>Registration is gated.</strong> Twitch OAuth sign-in is rejected unless the login
        appears on this allowlist (or the user has already been pre-invited as an event competitor
        by handle). Nobody can self-register without being added here first. Removing an entry also
        revokes the matching user&rsquo;s refresh tokens.
      </Alert>
      <Surface>
        <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1}>
          <TextField
            label="Twitch login"
            value={login}
            onChange={(e) => setLogin(e.target.value)}
            size="small"
            sx={{ flex: 1 }}
          />
          <TextField
            label="Note (optional)"
            value={note}
            onChange={(e) => setNote(e.target.value)}
            size="small"
            sx={{ flex: 2 }}
          />
          <Button variant="contained" disabled={!login.trim() || add.isPending} onClick={handleAdd}>
            Add
          </Button>
        </Stack>
        {errorMsg && (
          <Alert severity="error" sx={{ mt: 1 }}>
            {errorMsg}
          </Alert>
        )}
      </Surface>

      {isLoading && <LoadingState label="Loading allowlist…" />}
      {isError && <ErrorMessage message="Failed to load allowlist." />}
      {data && (
        <TableContainer component={Paper} variant="outlined">
          <Table size="small" sx={{ minWidth: 640 }}>
            <TableHead>
              <TableRow>
                <TableCell>Twitch login</TableCell>
                <TableCell>Linked user</TableCell>
                <TableCell>Note</TableCell>
                <TableCell>Added</TableCell>
                <TableCell align="right">Actions</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {data.length === 0 && (
                <TableRow>
                  <TableCell colSpan={5} align="center">
                    <Typography color="text.secondary">No allowlist entries yet.</Typography>
                  </TableCell>
                </TableRow>
              )}
              {data.map((e) => (
                <TableRow key={e.id}>
                  <TableCell sx={{ fontFamily: 'monospace' }}>{e.twitchLogin}</TableCell>
                  <TableCell>{e.linkedDisplayName ?? '—'}</TableCell>
                  <TableCell>{e.note ?? ''}</TableCell>
                  <TableCell>{new Date(e.createdAt).toLocaleDateString()}</TableCell>
                  <TableCell align="right">
                    <IconButton
                      size="small"
                      aria-label={`Remove ${e.twitchLogin}`}
                      disabled={remove.isPending}
                      onClick={() => setEntryToRemove({ id: e.id, twitchLogin: e.twitchLogin })}
                    >
                      <DeleteIcon fontSize="small" />
                    </IconButton>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}
      <ConfirmDialog
        open={!!entryToRemove}
        title={`Remove ${entryToRemove?.twitchLogin ?? 'login'}?`}
        description="This login will no longer be allowed to register, and any matching user's refresh tokens will be revoked."
        confirmLabel="Remove login"
        pending={remove.isPending}
        onCancel={() => setEntryToRemove(null)}
        onConfirm={() => {
          if (!entryToRemove) return
          remove.mutate(entryToRemove.id, { onSuccess: () => setEntryToRemove(null) })
        }}
      />
    </Stack>
  )
}
