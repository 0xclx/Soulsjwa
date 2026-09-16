import type { ReactElement } from 'react'
import { Link as RouterLink } from 'react-router-dom'
import Tab from '@mui/material/Tab'
import Tabs from '@mui/material/Tabs'
import GroupsIcon from '@mui/icons-material/Groups'
import ScienceIcon from '@mui/icons-material/Science'
import ShieldIcon from '@mui/icons-material/Shield'
import SportsEsportsIcon from '@mui/icons-material/SportsEsports'
import { MY_EVENTS_TAB_NAV, type MyEventsTab } from '../myEventsTabs'

const tabIcon = (tab: MyEventsTab): ReactElement => {
  if (tab === 'delegated') return <ShieldIcon fontSize="small" />
  if (tab === 'owned') return <GroupsIcon fontSize="small" />
  if (tab === 'trial') return <ScienceIcon fontSize="small" />
  return <SportsEsportsIcon fontSize="small" />
}

interface MyEventsTabsProps {
  activeTab: MyEventsTab
  /**
   * Per-tab badge counts. A tab whose count is not known until it loads — the
   * trial list has its own query — passes `undefined` rather than a `0` that
   * would claim there is nothing there.
   */
  counts: Partial<Record<MyEventsTab, number>>
}

/** Router-driven tab bar switching between the My Events dashboard's sections. */
export const MyEventsTabs = ({ activeTab, counts }: MyEventsTabsProps) => (
  <Tabs
    value={activeTab}
    variant="scrollable"
    allowScrollButtonsMobile
    aria-label="My events sections"
    sx={{
      borderBottom: 1,
      borderColor: 'divider',
      minHeight: 48,
      '& .MuiTab-root': { minHeight: 48, px: 2 },
    }}
  >
    {MY_EVENTS_TAB_NAV.map((item) => (
      <Tab
        key={item.tab}
        value={item.tab}
        label={
          counts[item.tab] !== undefined && counts[item.tab]! > 0
            ? `${item.label} (${counts[item.tab]})`
            : item.label
        }
        icon={tabIcon(item.tab)}
        iconPosition="start"
        component={RouterLink}
        to={item.to}
        aria-current={activeTab === item.tab ? 'page' : undefined}
      />
    ))}
  </Tabs>
)
