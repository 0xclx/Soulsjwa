import Stack from '@mui/material/Stack'
import Typography from '@mui/material/Typography'
import { SiteThemeEditor } from '../features/theme/siteTheme/components/SiteThemeEditor'

/**
 * Admin editor for the site-wide theme: background image,
 * font, and the six named palette slots per light/dark mode. Reachable
 * only through /admin, which AdminLayout already gates to admins.
 */
export const AdminThemePage = () => (
  <Stack spacing={2}>
    <Typography variant="h5" component="h2">
      Site theme
    </Typography>
    <SiteThemeEditor />
  </Stack>
)
