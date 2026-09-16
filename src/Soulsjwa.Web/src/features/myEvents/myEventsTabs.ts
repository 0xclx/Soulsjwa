import { TRIAL_TAB_LABEL } from '../events/scoreboard/trialPresentation'

export type MyEventsTab = 'competing' | 'delegated' | 'owned' | 'trial'

interface MyEventsTabNavItem {
  tab: MyEventsTab
  label: string
  to: string
}

/**
 * Ordered navigation model driving the My Events tabs. Each tab has its own
 * path so the chosen tab survives a refresh or a shared link, rather than
 * resetting to the first one; the router derives its routes from this list.
 *
 * Kept free of JSX and of the tab icons on purpose — the router imports it
 * eagerly, while the tab bar itself is lazy-loaded behind the page.
 */
export const MY_EVENTS_TAB_NAV: readonly MyEventsTabNavItem[] = [
  { tab: 'competing', label: 'Competing', to: '/my-events' },
  { tab: 'delegated', label: 'Delegated', to: '/my-events/delegated' },
  { tab: 'trial', label: TRIAL_TAB_LABEL, to: '/my-events/trial' },
  { tab: 'owned', label: 'Owned', to: '/my-events/owned' },
]

export const getMyEventsTab = (pathname: string): MyEventsTab => {
  if (pathname.endsWith('/delegated')) return 'delegated'
  if (pathname.endsWith('/owned')) return 'owned'
  if (pathname.endsWith('/trial')) return 'trial'
  return 'competing'
}
