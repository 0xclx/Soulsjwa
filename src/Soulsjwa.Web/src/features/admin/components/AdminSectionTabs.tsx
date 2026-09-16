import { Link as RouterLink } from 'react-router-dom'
import Tab from '@mui/material/Tab'
import Tabs from '@mui/material/Tabs'
import { ADMIN_SECTION_NAV, getAdminSectionIcon, type AdminSection } from '../adminSections'

interface AdminSectionTabsProps {
  activeSection: AdminSection
}

/** Router-driven tab bar switching between the admin console's sections. */
export const AdminSectionTabs = ({ activeSection }: AdminSectionTabsProps) => (
  <Tabs
    value={activeSection}
    variant="scrollable"
    allowScrollButtonsMobile
    aria-label="Admin sections"
    sx={{
      borderBottom: 1,
      borderColor: 'divider',
      minHeight: 48,
      '& .MuiTab-root': { minHeight: 48, px: 2 },
    }}
  >
    {ADMIN_SECTION_NAV.map((item) => (
      <Tab
        key={item.section}
        value={item.section}
        label={item.label}
        icon={getAdminSectionIcon(item.section)}
        iconPosition="start"
        component={RouterLink}
        to={item.to}
        aria-current={activeSection === item.section ? 'page' : undefined}
      />
    ))}
  </Tabs>
)
