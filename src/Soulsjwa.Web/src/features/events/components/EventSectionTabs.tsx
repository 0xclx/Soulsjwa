import { Link as RouterLink } from 'react-router-dom'
import Tab from '@mui/material/Tab'
import Tabs from '@mui/material/Tabs'
import { EVENT_SECTION_NAV, getEventSectionIcon, type EventSection } from '../eventDetail/sections'

interface EventSectionTabsProps {
  eventId: string
  activeSection: EventSection
  /** Activity is members-only; the tab is disabled for non-members. */
  canViewActivity: boolean
  /** OBS tokens are only relevant to the owner/admin and competitors. */
  canViewTokens: boolean
  /** Rules are hidden from everyone else until non-empty; managers keep access to write the first draft. */
  canViewRules: boolean
}

/** Router-driven tab bar switching between the event's detail sections. */
export const EventSectionTabs = ({
  eventId,
  activeSection,
  canViewActivity,
  canViewTokens,
  canViewRules,
}: EventSectionTabsProps) => (
  <Tabs
    value={activeSection}
    variant="scrollable"
    allowScrollButtonsMobile
    aria-label="Event sections"
    sx={{
      borderBottom: 1,
      borderColor: 'divider',
      minHeight: 48,
      '& .MuiTab-root': { minHeight: 48, px: 2 },
    }}
  >
    {EVENT_SECTION_NAV.filter(
      (item) =>
        (item.section !== 'tokens' || canViewTokens) && (item.section !== 'rules' || canViewRules),
    ).map((item) => (
      <Tab
        key={item.section}
        value={item.section}
        label={item.label}
        icon={getEventSectionIcon(item.section)}
        iconPosition="start"
        component={RouterLink}
        to={item.to(eventId)}
        disabled={item.section === 'activity' && !canViewActivity}
        aria-current={activeSection === item.section ? 'page' : undefined}
      />
    ))}
  </Tabs>
)
