import { lazy, type ComponentType } from 'react'

/**
 * Wraps a lazy page/component import + named-export pick in one line. Keep
 * the `import('…')` specifier a literal inline in each call below — never
 * parameterise it with a variable — or Vite loses static analysability and
 * the per-page chunks stop being split.
 */
// eslint-disable-next-line @typescript-eslint/no-explicit-any -- matches React.lazy's own `ComponentType<any>` loader signature
const page = <K extends string>(loader: () => Promise<Record<K, ComponentType<any>>>, name: K) =>
  lazy(async () => ({ default: (await loader())[name] }))

export const HomePage = page(() => import('../pages/HomePage'), 'HomePage')
export const AuthCallbackPage = page(() => import('../pages/AuthCallbackPage'), 'AuthCallbackPage')
export const ProfilePage = page(() => import('../pages/ProfilePage'), 'ProfilePage')
export const EventsPage = page(() => import('../pages/EventsPage'), 'EventsPage')
export const MyEventsPage = page(() => import('../pages/MyEventsPage'), 'MyEventsPage')
export const EventLayout = page(() => import('../components/EventLayout'), 'EventLayout')
export const EventOverviewPage = page(
  () => import('../pages/EventOverviewPage'),
  'EventOverviewPage',
)
export const EventCompetitorsPage = page(
  () => import('../pages/EventCompetitorsPage'),
  'EventCompetitorsPage',
)
export const EventGamesPage = page(() => import('../pages/EventGamesPage'), 'EventGamesPage')
export const EventTokensPage = page(() => import('../pages/EventTokensPage'), 'EventTokensPage')
export const EventActivityPage = page(
  () => import('../pages/EventActivityPage'),
  'EventActivityPage',
)
export const EventScoreboardPage = page(
  () => import('../pages/EventScoreboardPage'),
  'EventScoreboardPage',
)
export const EventRulesPage = page(() => import('../pages/EventRulesPage'), 'EventRulesPage')
export const RulesPage = page(() => import('../pages/RulesPage'), 'RulesPage')
export const EventCalendarPage = page(
  () => import('../pages/EventCalendarPage'),
  'EventCalendarPage',
)
export const PublicScoreboardPage = page(
  () => import('../pages/PublicScoreboardPage'),
  'PublicScoreboardPage',
)
export const OverlayPage = page(() => import('../pages/OverlayPage'), 'OverlayPage')
export const AdminLayout = page(() => import('../components/AdminLayout'), 'AdminLayout')
export const AdminOverviewPage = page(
  () => import('../pages/AdminOverviewPage'),
  'AdminOverviewPage',
)
export const AdminUsersPage = page(() => import('../pages/AdminUsersPage'), 'AdminUsersPage')
export const AdminCatalogPage = page(() => import('../pages/AdminCatalogPage'), 'AdminCatalogPage')
export const AdminAuditsPage = page(() => import('../pages/AdminAuditsPage'), 'AdminAuditsPage')
export const AdminFeatureFlagsPage = page(
  () => import('../pages/AdminFeatureFlagsPage'),
  'AdminFeatureFlagsPage',
)
export const AdminLegalPage = page(() => import('../pages/AdminLegalPage'), 'AdminLegalPage')
export const AdminThemePage = page(() => import('../pages/AdminThemePage'), 'AdminThemePage')
export const AdminTwitchExtensionPage = page(
  () => import('../pages/AdminTwitchExtensionPage'),
  'AdminTwitchExtensionPage',
)
export const ImpressumPage = page(() => import('../pages/ImpressumPage'), 'ImpressumPage')
export const DatenschutzPage = page(() => import('../pages/DatenschutzPage'), 'DatenschutzPage')
export const CalendarPage = page(() => import('../pages/CalendarPage'), 'CalendarPage')
