import type { ReactElement } from 'react'
import CalendarMonthIcon from '@mui/icons-material/CalendarMonth'
import GavelIcon from '@mui/icons-material/Gavel'
import GroupsIcon from '@mui/icons-material/Groups'
import HistoryIcon from '@mui/icons-material/History'
import ScoreboardIcon from '@mui/icons-material/Leaderboard'
import SportsEsportsIcon from '@mui/icons-material/SportsEsports'
import VideocamIcon from '@mui/icons-material/Videocam'

export type EventSection =
  'overview' | 'competitors' | 'games' | 'scoreboard' | 'rules' | 'calendar' | 'tokens' | 'activity'

interface EventSectionNavItem {
  section: EventSection
  label: string
  to: (eventId: string) => string
}

export const EVENT_SECTION_NAV: readonly EventSectionNavItem[] = [
  { section: 'overview', label: 'Overview', to: (eventId) => `/events/${eventId}` },
  { section: 'rules', label: 'Rules', to: (eventId) => `/events/${eventId}/rules` },
  {
    section: 'competitors',
    label: 'Competitors',
    to: (eventId) => `/events/${eventId}/competitors`,
  },
  { section: 'games', label: 'Games & objectives', to: (eventId) => `/events/${eventId}/games` },
  {
    section: 'scoreboard',
    label: 'Scoreboard',
    to: (eventId) => `/events/${eventId}/scoreboard`,
  },
  { section: 'calendar', label: 'Calendar', to: (eventId) => `/events/${eventId}/calendar` },
  { section: 'tokens', label: 'Broadcast', to: (eventId) => `/events/${eventId}/tokens` },
  { section: 'activity', label: 'Activity', to: (eventId) => `/events/${eventId}/activity` },
]

export const getEventSection = (pathname: string): EventSection => {
  if (pathname.endsWith('/competitors')) return 'competitors'
  if (pathname.endsWith('/games')) return 'games'
  if (pathname.endsWith('/scoreboard')) return 'scoreboard'
  if (pathname.endsWith('/rules')) return 'rules'
  if (pathname.endsWith('/calendar')) return 'calendar'
  if (pathname.endsWith('/tokens')) return 'tokens'
  if (pathname.endsWith('/activity')) return 'activity'
  return 'overview'
}

export const getEventSectionIcon = (section: EventSection): ReactElement => {
  if (section === 'competitors') return <GroupsIcon fontSize="small" />
  if (section === 'games') return <SportsEsportsIcon fontSize="small" />
  if (section === 'scoreboard') return <ScoreboardIcon fontSize="small" />
  if (section === 'rules') return <GavelIcon fontSize="small" />
  if (section === 'calendar') return <CalendarMonthIcon fontSize="small" />
  if (section === 'tokens') return <VideocamIcon fontSize="small" />
  if (section === 'activity') return <HistoryIcon fontSize="small" />
  return <ScoreboardIcon fontSize="small" />
}
