import { useState } from 'react'
import Alert from '@mui/material/Alert'
import AlertTitle from '@mui/material/AlertTitle'
import Button from '@mui/material/Button'
import Link from '@mui/material/Link'
import Snackbar from '@mui/material/Snackbar'
import Stack from '@mui/material/Stack'
import Typography from '@mui/material/Typography'
import { ErrorMessage, LoadingState } from '../../../components/ui'
import { MarkdownEditor } from '../../markdown/MarkdownEditor'
import { getErrorDetail } from '../../../lib/getErrorDetail'
import { useLegalDocument } from '../hooks/useLegalDocument'
import { useUpdateLegalDocument } from '../hooks/useUpdateLegalDocument'
import {
  LEGAL_TEMPLATES_README_URL,
  LEGAL_TEMPLATE_HOSTING_NOTE,
  LEGAL_TEMPLATE_LINKS,
} from '../legalTemplates'
import type { LegalDocumentKind } from '../../../types'

interface LegalDocumentEditorProps {
  kind: LegalDocumentKind
}

/**
 * Admin editor for a single legal document. Unlike EventRulesPage, this
 * has no separate read-only view: the admin route is the only place an
 * empty document can be reached, since the public /impressum and
 * /datenschutz routes 404 on empty content.
 *
 * The template callout above the editor stays advisory — legal completeness
 * is the operator's responsibility, so nothing here validates the document
 * or blocks a save.
 */
export const LegalDocumentEditor = ({ kind }: LegalDocumentEditorProps) => {
  const { data, isLoading, isError } = useLegalDocument(kind)
  const updateDocument = useUpdateLegalDocument(kind)
  // null until the user edits — the field then shows the loaded content
  // without needing an effect to seed it (AdminLegalPage remounts this
  // component via `key={kind}` on tab switch, so state doesn't linger).
  const [draft, setDraft] = useState<string | null>(null)
  const [saveError, setSaveError] = useState<string | null>(null)
  const [successMessage, setSuccessMessage] = useState<string | null>(null)

  if (isLoading) return <LoadingState label={`Loading ${kind}…`} />
  if (isError || !data) return <ErrorMessage message={`Failed to load ${kind}.`} />

  const value = draft ?? data.content ?? ''

  const handleSave = () => {
    setSaveError(null)
    updateDocument.mutate(value.trim() === '' ? null : value, {
      onSuccess: () => setSuccessMessage(`${kind} saved.`),
      onError: (err) => setSaveError(getErrorDetail(err, `Failed to save ${kind}.`)),
    })
  }

  return (
    <Stack spacing={2}>
      <Alert severity="info">
        <AlertTitle>Start from a template</AlertTitle>
        <Typography variant="body2">
          The repository ships a baseline {kind} in German and English, written against the features
          this instance actually has. Copy one in, then replace every{' '}
          <Typography component="code" variant="body2" sx={{ fontFamily: 'monospace' }}>
            &lt;placeholder&gt;
          </Typography>{' '}
          and delete the sections that do not apply to you.
        </Typography>
        <Stack direction="row" spacing={2} sx={{ mt: 1, flexWrap: 'wrap', gap: 1 }}>
          {LEGAL_TEMPLATE_LINKS[kind].map((template) => (
            <Link
              key={template.href}
              href={template.href}
              target="_blank"
              rel="noopener noreferrer"
              variant="body2"
            >
              {kind} — {template.language}
            </Link>
          ))}
          <Link
            href={LEGAL_TEMPLATES_README_URL}
            target="_blank"
            rel="noopener noreferrer"
            variant="body2"
            color="text.secondary"
          >
            How to use these templates
          </Link>
        </Stack>
        <Typography variant="body2" sx={{ mt: 1.5 }}>
          {LEGAL_TEMPLATE_HOSTING_NOTE}
        </Typography>
      </Alert>
      <MarkdownEditor value={value} onChange={setDraft} label={kind} />
      {saveError && <Alert severity="error">{saveError}</Alert>}
      <Button
        variant="contained"
        onClick={handleSave}
        disabled={updateDocument.isPending}
        sx={{ alignSelf: 'flex-start' }}
      >
        {updateDocument.isPending ? 'Saving…' : 'Save'}
      </Button>
      <Snackbar
        open={!!successMessage}
        autoHideDuration={3000}
        onClose={() => setSuccessMessage(null)}
      >
        <Alert severity="success" onClose={() => setSuccessMessage(null)}>
          {successMessage}
        </Alert>
      </Snackbar>
    </Stack>
  )
}
