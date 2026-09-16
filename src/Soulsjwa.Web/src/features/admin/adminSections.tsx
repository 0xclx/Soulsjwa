import type { ReactElement } from 'react'
import ArticleIcon from '@mui/icons-material/Article'
import GavelIcon from '@mui/icons-material/Gavel'
import PaletteIcon from '@mui/icons-material/Palette'
import SportsEsportsIcon from '@mui/icons-material/SportsEsports'
import GroupIcon from '@mui/icons-material/Group'
import PlaylistAddCheckIcon from '@mui/icons-material/PlaylistAddCheck'
import ToggleOnIcon from '@mui/icons-material/ToggleOn'
import ExtensionIcon from '@mui/icons-material/Extension'

export type AdminSection =
  | 'allowlist'
  | 'users'
  | 'catalog'
  | 'audits'
  | 'feature-flags'
  | 'legal'
  | 'theme'
  | 'twitch-extension'

interface AdminSectionNavItem {
  section: AdminSection
  label: string
  to: string
}

export const ADMIN_SECTION_NAV: readonly AdminSectionNavItem[] = [
  { section: 'allowlist', label: 'Allowlist', to: '/admin' },
  { section: 'users', label: 'Users', to: '/admin/users' },
  { section: 'catalog', label: 'Catalog', to: '/admin/catalog' },
  { section: 'audits', label: 'Audits', to: '/admin/audits' },
  { section: 'feature-flags', label: 'Feature flags', to: '/admin/feature-flags' },
  { section: 'legal', label: 'Legal', to: '/admin/legal' },
  { section: 'theme', label: 'Theme', to: '/admin/theme' },
  { section: 'twitch-extension', label: 'Twitch extension', to: '/admin/twitch-extension' },
]

export const getAdminSection = (pathname: string): AdminSection => {
  if (pathname.endsWith('/users')) return 'users'
  if (pathname.endsWith('/catalog')) return 'catalog'
  if (pathname.endsWith('/audits')) return 'audits'
  if (pathname.endsWith('/feature-flags')) return 'feature-flags'
  if (pathname.endsWith('/legal')) return 'legal'
  if (pathname.endsWith('/theme')) return 'theme'
  if (pathname.endsWith('/twitch-extension')) return 'twitch-extension'
  return 'allowlist'
}

export const getAdminSectionIcon = (section: AdminSection): ReactElement => {
  if (section === 'users') return <GroupIcon fontSize="small" />
  if (section === 'catalog') return <SportsEsportsIcon fontSize="small" />
  if (section === 'audits') return <GavelIcon fontSize="small" />
  if (section === 'feature-flags') return <ToggleOnIcon fontSize="small" />
  if (section === 'legal') return <ArticleIcon fontSize="small" />
  if (section === 'theme') return <PaletteIcon fontSize="small" />
  if (section === 'twitch-extension') return <ExtensionIcon fontSize="small" />
  return <PlaylistAddCheckIcon fontSize="small" />
}
