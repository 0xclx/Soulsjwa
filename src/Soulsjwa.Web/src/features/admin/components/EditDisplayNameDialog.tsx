import { useId } from 'react'
import Dialog from '@mui/material/Dialog'
import DialogContent from '@mui/material/DialogContent'
import DialogTitle from '@mui/material/DialogTitle'
import { DisplayNameField } from '../../users/components/DisplayNameField'
import { useAdminUpdateDisplayName } from '../hooks/useAdminUpdateDisplayName'
import { getErrorDetail } from '../../../lib/getErrorDetail'
import type { AdminUserSummary } from '../../../types'

const FAILED = 'Failed to save the display name.'

interface EditDisplayNameDialogProps {
  user: AdminUserSummary
  onClose: () => void
}

/** Admin rename of one user; closes on success, keeps errors on the field. */
export const EditDisplayNameDialog = ({ user, onClose }: EditDisplayNameDialogProps) => {
  const update = useAdminUpdateDisplayName()
  const titleId = useId()
  const save = (displayName: string | null) =>
    update.mutate({ id: user.id, displayName }, { onSuccess: onClose })

  return (
    <Dialog
      open
      onClose={update.isPending ? undefined : onClose}
      aria-labelledby={titleId}
      fullWidth
      maxWidth="xs"
    >
      <DialogTitle id={titleId}>Display name for {user.twitchLogin}</DialogTitle>
      <DialogContent sx={{ pt: 1 }}>
        <DisplayNameField
          currentName={user.displayName}
          twitchDisplayName={user.twitchDisplayName}
          hasOverride={user.displayNameOverride !== null}
          pending={update.isPending}
          error={update.isError ? getErrorDetail(update.error, FAILED) : null}
          onSave={save}
          onReset={() => save(null)}
        />
      </DialogContent>
    </Dialog>
  )
}
