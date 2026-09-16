import { useState } from 'react'
import Alert from '@mui/material/Alert'
import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Snackbar from '@mui/material/Snackbar'
import Stack from '@mui/material/Stack'
import Typography from '@mui/material/Typography'
import EditIcon from '@mui/icons-material/Edit'
import { EmptyState, ErrorMessage, LoadingState } from '../../../components/ui'
import { MarkdownEditor } from '../../markdown/MarkdownEditor'
import { MarkdownView } from '../../markdown/MarkdownView'
import { useEventRules } from '../hooks/useEventRules'
import { useUpdateEventRules } from '../hooks/useUpdateEventRules'
import { getErrorDetail } from '../../../lib/getErrorDetail'

interface RulesPanelProps {
  eventId: string
  /** Whether the current viewer may edit the rules document. */
  canManage: boolean
}

/**
 * An event's rules document: public read via MarkdownView, owner/admin
 * write via MarkdownEditor. Reachable even when rules are empty for
 * canManage viewers (so there's a way to write the first draft) — the
 * "hidden when empty" rule only hides the nav tab/entry from
 * everyone else.
 *
 * Purely presentational: the caller supplies `eventId`/`canManage`, so this
 * renders the same regardless of whether it sits inside the event layout's
 * tabs or on its own standalone page (see `RulesPage`).
 */
export const RulesPanel = ({ eventId, canManage }: RulesPanelProps) => {
  const { data: rules, isLoading, isError } = useEventRules(eventId)
  const updateRules = useUpdateEventRules(eventId)
  const [isEditing, setIsEditing] = useState(false)
  const [draft, setDraft] = useState('')
  const [saveError, setSaveError] = useState<string | null>(null)
  const [successMessage, setSuccessMessage] = useState<string | null>(null)

  if (isLoading) return <LoadingState label="Loading rules…" />
  if (isError || !rules) return <ErrorMessage message="Failed to load rules." />

  const hasContent = !!rules.content?.trim()

  const startEditing = () => {
    setDraft(rules.content ?? '')
    setSaveError(null)
    setIsEditing(true)
  }

  const handleSave = () => {
    setSaveError(null)
    updateRules.mutate(draft, {
      onSuccess: () => {
        setIsEditing(false)
        setSuccessMessage('Rules saved.')
      },
      onError: (err) => setSaveError(getErrorDetail(err, 'Failed to save rules.')),
    })
  }

  return (
    <Box component="section" aria-labelledby="rules-heading">
      <Stack
        direction="row"
        spacing={2}
        sx={{ alignItems: 'center', flexWrap: 'wrap', mb: 2, gap: 1 }}
      >
        <Typography variant="h5" component="h2" id="rules-heading">
          Rules
        </Typography>
        {canManage && !isEditing && (
          <Button size="small" variant="outlined" startIcon={<EditIcon />} onClick={startEditing}>
            {hasContent ? 'Edit' : 'Add rules'}
          </Button>
        )}
      </Stack>

      {isEditing ? (
        <Stack spacing={2}>
          <MarkdownEditor value={draft} onChange={setDraft} label="Rules" />
          {saveError && <Alert severity="error">{saveError}</Alert>}
          <Stack direction="row" spacing={1}>
            <Button variant="contained" onClick={handleSave} disabled={updateRules.isPending}>
              {updateRules.isPending ? 'Saving…' : 'Save'}
            </Button>
            <Button
              color="inherit"
              onClick={() => setIsEditing(false)}
              disabled={updateRules.isPending}
            >
              Cancel
            </Button>
          </Stack>
        </Stack>
      ) : hasContent ? (
        <MarkdownView source={rules.content!} />
      ) : (
        <EmptyState
          title="No rules yet"
          description={
            canManage
              ? 'Add rules for this event so competitors know what to expect.'
              : 'This event has no published rules.'
          }
        />
      )}

      <Snackbar
        open={!!successMessage}
        autoHideDuration={3000}
        onClose={() => setSuccessMessage(null)}
      >
        <Alert severity="success" onClose={() => setSuccessMessage(null)}>
          {successMessage}
        </Alert>
      </Snackbar>
    </Box>
  )
}
