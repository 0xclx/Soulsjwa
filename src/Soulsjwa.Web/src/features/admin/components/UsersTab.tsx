import { useState } from 'react'
import Alert from '@mui/material/Alert'
import Button from '@mui/material/Button'
import Chip from '@mui/material/Chip'
import Paper from '@mui/material/Paper'
import Stack from '@mui/material/Stack'
import Table from '@mui/material/Table'
import TableBody from '@mui/material/TableBody'
import TableCell from '@mui/material/TableCell'
import TableContainer from '@mui/material/TableContainer'
import TableHead from '@mui/material/TableHead'
import TableRow from '@mui/material/TableRow'
import TextField from '@mui/material/TextField'
import { EmptyState, ErrorMessage, LoadingState, PaginationControls } from '../../../components/ui'
import { useAdminUsers } from '../hooks/useAdminUsers'
import { useSetUserRole } from '../hooks/useSetUserRole'

/**
 * Paginated user directory with search and role promotion/demotion. The current
 * user cannot demote themselves and the server enforces "at least one admin".
 */
export const UsersTab = ({ currentUserId }: { currentUserId: string }) => {
  const [page, setPage] = useState(1)
  const [search, setSearch] = useState('')
  const { data, isLoading, isError } = useAdminUsers(page, search || undefined)
  const setRole = useSetUserRole()
  const [roleError, setRoleError] = useState<string | null>(null)

  const handleSetRole = (id: string, role: 'User' | 'Admin') => {
    setRoleError(null)
    setRole.mutate(
      { id, role },
      {
        onError: (err: unknown) => {
          const e = err as { response?: { status?: number } }
          if (e?.response?.status === 409) {
            setRoleError('Cannot demote the last remaining admin.')
          } else {
            setRoleError('Failed to update the user role.')
          }
        },
      },
    )
  }

  return (
    <Stack spacing={2}>
      {roleError && (
        <Alert severity="error" onClose={() => setRoleError(null)}>
          {roleError}
        </Alert>
      )}
      <TextField
        label="Search by login or display name"
        size="small"
        value={search}
        onChange={(e) => {
          setSearch(e.target.value)
          setPage(1)
        }}
      />
      {isLoading && <LoadingState label="Loading users…" />}
      {isError && <ErrorMessage message="Failed to load users." />}
      {data?.items.length === 0 && (
        <EmptyState
          title="No users found"
          description="Try another login or display-name search."
        />
      )}
      {data && data.items.length > 0 && (
        <TableContainer component={Paper} variant="outlined">
          <Table size="small" sx={{ minWidth: 560 }}>
            <TableHead>
              <TableRow>
                <TableCell>Login</TableCell>
                <TableCell>Display name</TableCell>
                <TableCell>Role</TableCell>
                <TableCell>Allowlisted</TableCell>
                <TableCell align="right">Actions</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {data.items.map((u) => {
                const isSelf = u.id === currentUserId
                return (
                  <TableRow key={u.id}>
                    <TableCell sx={{ fontFamily: 'monospace' }}>{u.twitchLogin}</TableCell>
                    <TableCell>{u.displayName}</TableCell>
                    <TableCell>
                      <Chip
                        label={u.role}
                        size="small"
                        color={u.role === 'Admin' ? 'primary' : 'default'}
                      />
                    </TableCell>
                    <TableCell>
                      {u.isAllowlisted ? (
                        <Chip label="Yes" size="small" color="success" variant="outlined" />
                      ) : (
                        <Chip label="No" size="small" color="warning" variant="outlined" />
                      )}
                    </TableCell>
                    <TableCell align="right">
                      {u.role === 'Admin' ? (
                        <Button
                          size="small"
                          color="warning"
                          disabled={isSelf || setRole.isPending}
                          onClick={() => handleSetRole(u.id, 'User')}
                        >
                          Demote
                        </Button>
                      ) : (
                        <Button
                          size="small"
                          variant="outlined"
                          disabled={setRole.isPending}
                          onClick={() => handleSetRole(u.id, 'Admin')}
                        >
                          Promote
                        </Button>
                      )}
                    </TableCell>
                  </TableRow>
                )
              })}
            </TableBody>
          </Table>
        </TableContainer>
      )}
      {data && (data.totalCount ?? 0) > data.pageSize && (
        <PaginationControls
          page={data.page}
          totalPages={Math.ceil((data.totalCount ?? 0) / data.pageSize)}
          hasPreviousPage={data.hasPreviousPage}
          hasNextPage={data.hasNextPage}
          onPrevious={() => setPage((p) => Math.max(1, p - 1))}
          onNext={() => setPage((p) => p + 1)}
          label="users"
        />
      )}
    </Stack>
  )
}
