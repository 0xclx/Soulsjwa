import { useState } from 'react'
import Stack from '@mui/material/Stack'
import Tab from '@mui/material/Tab'
import Tabs from '@mui/material/Tabs'
import Typography from '@mui/material/Typography'
import { LegalDocumentEditor } from '../features/legal/components/LegalDocumentEditor'
import type { LegalDocumentKind } from '../types'

/**
 * Admin editor for the Impressum and Datenschutz documents. Legal
 * completeness is the operator's responsibility: no field validation, no
 * compliance checklist, no warnings here.
 */
export const AdminLegalPage = () => {
  const [kind, setKind] = useState<LegalDocumentKind>('Impressum')

  return (
    <Stack spacing={2}>
      <Typography variant="h5" component="h2">
        Legal documents
      </Typography>
      <Tabs value={kind} onChange={(_, next) => setKind(next)} aria-label="Legal document">
        <Tab value="Impressum" label="Impressum" />
        <Tab value="Datenschutz" label="Datenschutz" />
      </Tabs>
      <LegalDocumentEditor key={kind} kind={kind} />
    </Stack>
  )
}
