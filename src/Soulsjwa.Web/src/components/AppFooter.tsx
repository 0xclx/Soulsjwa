import { Link as RouterLink } from 'react-router-dom'
import Box from '@mui/material/Box'
import Link from '@mui/material/Link'
import Stack from '@mui/material/Stack'
import { useLegalDocument } from '../features/legal/hooks/useLegalDocument'

const LEGAL_LINKS = [
  { kind: 'Impressum', to: '/impressum', label: 'Impressum' },
  { kind: 'Datenschutz', to: '/datenschutz', label: 'Datenschutz' },
] as const

/**
 * Renders a link per non-empty legal document. Renders nothing
 * at all — no footer element — when both Impressum and Datenschutz are
 * empty, so an operator who hasn't set either sees no dead links.
 */
export const AppFooter = () => {
  const impressum = useLegalDocument('Impressum')
  const datenschutz = useLegalDocument('Datenschutz')
  const contentByKind = { Impressum: impressum.data, Datenschutz: datenschutz.data }

  const links = LEGAL_LINKS.filter((item) => !!contentByKind[item.kind]?.content?.trim())

  if (links.length === 0) return null

  return (
    <Box component="footer" sx={{ borderTop: 1, borderColor: 'divider', py: 2, px: 2, mt: 'auto' }}>
      <Stack direction="row" spacing={2} sx={{ justifyContent: 'center' }}>
        {links.map((link) => (
          <Link
            key={link.kind}
            component={RouterLink}
            to={link.to}
            variant="body2"
            color="text.secondary"
            underline="hover"
          >
            {link.label}
          </Link>
        ))}
      </Stack>
    </Box>
  )
}
